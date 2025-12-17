
using System;

namespace KSASM.Assembly
{
  public abstract class Statement
  {
    public abstract void FirstPass(Context ctx);
    public abstract void SecondPass(Context ctx);
  }

  public class LabelStatement : Statement
  {
    public Token Token;
    public string Label;

    public override void FirstPass(Context ctx) => ctx.EmitLabel(Label);

    public override void SecondPass(Context ctx)
    {
      if (ctx.Addr != ctx.Labels[Label])
        throw new InvalidOperationException(
          $"Label address mismatch for '{Label}': {ctx.Addr} != {ctx.Labels[Label]}");
    }
  }

  public class PositionStatement : Statement
  {
    public Token Token;
    public int Addr;

    public override void FirstPass(Context ctx) =>
      ctx.Addr = Addr;

    public override void SecondPass(Context ctx) =>
      ctx.Addr = Addr;
  }

  public class DataStatement(ParsedData parsedData) : Statement
  {
    public readonly ParsedData Data = parsedData;
    private int width = 1;
    private int typeSize;
    private FixedRange stringRange;

    public override void FirstPass(Context ctx)
    {
      typeSize = Data.Type.SizeBytes();
      if (Data.Width is Token wtoken)
      {
        if (!Token.TryParseValue(ctx.Buffer[wtoken], out var wval, out var wmode))
          throw ctx.Invalid(wtoken);
        wval.Convert(wmode, ValueMode.Unsigned);
        if (wval.Unsigned > 1024)
          throw ctx.Invalid(wtoken);
        width = (int)wval.Unsigned;
      }

      if (Data.Value is Token { Type: TokenType.String } strTok)
      {
        if (!ctx.TryAddString(strTok, out stringRange))
          throw ctx.Invalid(strTok);
      }

      ctx.Addr += Data switch
      {
        { ExprVal: FixedRange expr } => typeSize * expr.Length,
        { Value: Token { Type: TokenType.String }, Type: DataType.U8 } => stringRange.Length * typeSize,
        { Value: Token { Type: TokenType.String }, Type: DataType.S48 } =>
          (stringRange.Length * DataType.U8.SizeBytes()) + typeSize, // N S48 pointers + N copies of string
        { Value: Token { Type: TokenType.String } tok } => throw ctx.Invalid(tok),
        _ => typeSize,
      } * width; ;
    }

    public override void SecondPass(Context ctx)
    {
      var mode = Data.Type.VMode();

      switch (Data)
      {
        case { ExprVal: FixedRange expr }:
          {
            Span<Value> vals = stackalloc Value[expr.Length];
            for (var i = 0; i < expr.Length; i++)
              vals[i] = ctx.EvalExpr(expr.Start + i, mode);
            using var emitter = ctx.Emitter(Data.Type);
            for (var w = 0; w < width; w++)
              emitter.EmitRange(vals);
          }
          break;
        case { Value: Token { Type: TokenType.String } tok, Type: DataType.U8 }:
          {
            using var emitter = ctx.Emitter(Data.Type);
            for (var w = 0; w < width; w++)
              emitter.EmitString(stringRange);
          }
          break;
        case { Value: Token { Type: TokenType.String } tok, Type: DataType.S48 }:
          {
            var strStart = (ulong)(ctx.Addr + typeSize * width);
            var strLen = (ulong)stringRange.Length;
            Span<Value> ptrVals = stackalloc Value[width];
            for (var w = 0; w < width; w++)
            {
              ptrVals[w].Unsigned = strStart | (strLen << 24);
              strStart += strLen;
            }

            using (var emitter = ctx.Emitter(Data.Type))
              emitter.EmitRange(ptrVals);

            using (var emitter = ctx.Emitter(DataType.U8))
              for (var w = 0; w < width; w++)
                emitter.EmitString(stringRange);
          }
          break;
        case { Value: Token { Type: TokenType.Word } tok }:
          {
            if (!ctx.Labels.TryGetValue(new(ctx.Buffer[tok]), out var addr))
              throw ctx.Invalid(tok);

            var val = new Value { Unsigned = (ulong)addr };
            val.Convert(ValueMode.Unsigned, mode);

            using var emitter = ctx.Emitter(Data.Type);
            for (var w = 0; w < width; w++)
              emitter.Emit(val);
          }
          break;
        case { Value: Token { Type: TokenType.Number } tok }:
          {
            if (!Token.TryParseValue(ctx.Buffer[tok], out var val, out var vmode))
              throw ctx.Invalid(tok);
            val.Convert(vmode, mode);

            using var emitter = ctx.Emitter(Data.Type);
            for (var w = 0; w < width; w++)
              emitter.Emit(val);
          }
          break;
        default:
          throw ctx.Invalid(Data.Value);
      }
    }
  }

  public class InstructionStatement(ParsedInstruction parsedInst) : Statement
  {
    public readonly ParsedInstruction Inst = parsedInst;
    private OpCode opCode;
    private OpCodeInfo info;

    private ResolvedOperand A;
    private ResolvedOperand B;
    private ResolvedOperand C;
    private int immCount = 0;
    private int width = 0;

    public override void FirstPass(Context ctx)
    {
      if (!Token.TryParseOpCode(ctx.Buffer[Inst.OpCode], out opCode))
        throw ctx.Invalid(Inst.OpCode);
      info = OpCodeInfo.For(opCode);

      DataType? defType = null;
      if (Inst.DefaultType is Token dttoken)
      {
        if (!Token.TryParseType(ctx.Buffer[dttoken], out var type))
          throw ctx.Invalid(dttoken);
        defType = type;
      }
      if (Inst.Width is Token wtoken)
      {
        if (!Token.TryParseValue(ctx.Buffer[wtoken], out var val, out var mode))
          throw ctx.Invalid(wtoken);
        val.Convert(mode, ValueMode.Unsigned);
        width = (int)val.Unsigned;
        if (width < 1 || width > 8)
          throw ctx.Invalid(wtoken);
      }

      var pin = Inst.ResultIndex != -1 ? Inst.ResultIndex : Inst.OperandCount;
      var pout = Inst.OperandCount - pin;

      if (pin > info.InOps)
        throw ctx.Invalid(Inst.OpCode, $"invalid input count ({pin} > {info.InOps}):");
      if (pout > info.OutOps)
        throw ctx.Invalid(Inst.OpCode, $"invalid output count ({pout > info.OutOps}):");

      for (var idx = 0; idx < info.TotalOps; idx++)
      {
        ref var rop = ref Op(idx);
        var opInfo = info[idx];

        var pindex = -1;
        if (idx < info.InOps && idx < pin)
          pindex = idx;
        else if (idx >= info.InOps && (idx - pin) < pout)
          pindex = idx - info.InOps;

        DataType? ptype = null;
        if (pindex != -1)
        {
          ref var pop = ref Inst.Op(idx);
          rop.Value = pop.Val;
          rop.ExprVal = pop.ExprVal;
          if (pop.Type is Token ttoken)
          {
            if (!Token.TryParseType(ctx.Buffer[ttoken], out var type))
              throw ctx.Invalid(ttoken);
            if (opInfo.Type != null && opInfo.Type != type)
              throw ctx.Invalid(ttoken);
            ptype = type;
          }
        }
        rop.Type = opInfo.Type ?? ptype ?? defType
          ?? throw ctx.Invalid(Inst.OpCode, $"missing type for operand {idx}");

        // if no width is specified and this is not a fixed-width operand, use const width as the instruction width
        if (width == 0 && opInfo.Width == null && rop.ExprVal is FixedRange range)
          width = Math.Clamp(range.Length, 0, 8);
      }

      if (width == 0)
        width = 1;

      // advance by instruction count
      ctx.Addr += DataType.P24.SizeBytes();
      // advance by immediate sizes, and register strings
      for (var idx = 0; idx < info.TotalOps; idx++)
      {
        ref var rop = ref Op(idx);
        var opInfo = info[idx];

        rop.Width = opInfo.Width ?? width;
        if (rop.Value?.Type is not (null or TokenType.Placeholder))
        {
          immCount++;
          ctx.Addr += rop.Type.SizeBytes() * rop.Width;
        }
        if (rop.Value is Token { Type: TokenType.String } stok)
        {
          if (rop.Type == DataType.U8)
          {
            // u8 strings are included inline
            if (!ctx.TryAddString(stok, out rop.String))
              throw ctx.Invalid(stok);
            // truncate to width if its too wide
            if (rop.String.Length > rop.Width)
              rop.String = new(rop.String.Start, rop.Width);
          }
          else if (rop.Type == DataType.S48)
          {
            // s48 strings are allocated separately and a pointer is stored inline
            if (!ctx.TryAddString(stok, out var str))
              throw ctx.Invalid(stok);
            rop.String = ctx.AddInlineString(str);
            // duplicate the string by width so each s48 val points to a different copy
            for (var i = 1; i < rop.Width; i++)
              ctx.AddInlineString(str);
          }
          else
            throw ctx.Invalid(stok);
        }
      }
    }

    public override void SecondPass(Context ctx)
    {
      var inst = new Instruction
      {
        OpCode = opCode,
        Width = (byte)width,
        ImmCount = (byte)immCount,
        AType = A.Type,
        BType = B.Type,
        CType = C.Type,
      };
      ctx.EmitInst(Inst.OpCode, inst.Encode());


      Span<Value> vals = stackalloc Value[8];
      for (var idx = 0; idx < immCount; idx++)
      {
        ref var rop = ref Op(idx);
        var mode = rop.Type.VMode();

        using var emitter = ctx.Emitter(rop.Type);

        if (rop.ExprVal is FixedRange expr)
        {
          for (var i = 0; i < rop.Width && i < expr.Length; i++)
            vals[i] = ctx.EvalExpr(expr.Start + i, mode);
          // duplicate values to fill width
          for (var i = expr.Length; i < rop.Width; i++)
            vals[i] = vals[i % expr.Length];
        }
        else if (rop.Value is Token { Type: TokenType.Number } ntok)
        {
          if (!Token.TryParseValue(ctx.Buffer[ntok], out vals[0], out var vmode))
            throw ctx.Invalid(ntok);
          vals[0].Convert(vmode, mode);
          for (var i = 1; i < rop.Width; i++)
            vals[i] = vals[0];
        }
        else if (rop.Value is Token { Type: TokenType.Word } ltok)
        {
          if (!ctx.Labels.TryGetValue(new(ctx.Buffer[ltok]), out var addr))
            throw ctx.Invalid(ltok);

          vals[0].Unsigned = (ulong)addr;
          vals[0].Convert(ValueMode.Unsigned, mode);
          for (var i = 1; i < rop.Width; i++)
            vals[i] = vals[0];
        }
        else if (rop.Value is Token { Type: TokenType.String } stok)
        {
          if (rop.Type == DataType.U8)
          {
            var svals = ctx.StringChars[rop.String];
            for (var i = 0; i < rop.Width; i++)
              vals[i] = svals[i % svals.Length];
          }
          else if (rop.Type == DataType.S48)
          {
            for (var i = 0; i < rop.Width; i++)
            {
              var start = (ulong)(ctx.InlineStringStart + i * rop.String.Length);
              var len = (ulong)rop.String.Length;
              vals[i].Unsigned = start | (len << 24);
            }
          }
          else
            throw ctx.Invalid(stok);
        }
        else
          throw ctx.Invalid(rop.Value ?? Inst.OpCode);

        emitter.EmitRange(vals[..rop.Width]);
      }
    }

    private ref ResolvedOperand Op(int idx)
    {
      switch (idx)
      {
        case 0: return ref A;
        case 1: return ref B;
        case 2: return ref C;
        default: throw new IndexOutOfRangeException($"{idx}");
      }
    }

    private struct ResolvedOperand
    {
      public int Width;
      public DataType Type;
      public Token? Value;
      public FixedRange? ExprVal;
      public FixedRange String;
    }
  }
}