
using System;
using System.Collections.Generic;

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

    public override void FirstPass(Context ctx)
    {
      typeSize = Data.Type.SizeBytes();
      if (Data.Width is Token wtoken)
      {
        if (!Token.TryParseValue(ctx.Buffer[wtoken], out var wval, out var wmode))
          throw Invalid(wtoken);
        wval.Convert(wmode, ValueMode.Unsigned);
        if (wval.Unsigned > 1024)
          throw Invalid(wtoken);
        width = (int)wval.Unsigned;
      }

      ctx.Addr += CalcSize(ctx);
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
            Span<Value> vals = stackalloc Value[StringLength(ctx, tok)];
            if (!Token.TryParseString(ctx.Buffer[tok], vals))
              throw Invalid(tok);
            using var emitter = ctx.Emitter(Data.Type);
            for (var w = 0; w < width; w++)
              emitter.EmitRange(vals);
          }
          break;
        case { Value: Token { Type: TokenType.String } tok, Type: DataType.S48 }:
          {
            Span<Value> strVals = stackalloc Value[StringLength(ctx, tok)];
            if (!Token.TryParseString(ctx.Buffer[tok], strVals))
              throw Invalid(tok);

            var strStart = (ulong)(ctx.Addr + typeSize * width);
            var strLen = (ulong)strVals.Length;
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
                emitter.EmitRange(strVals);
          }
          break;
        case { Value: Token { Type: TokenType.Word } tok }:
          {
            if (!ctx.Labels.TryGetValue(new(ctx.Buffer[tok]), out var addr))
              throw Invalid(tok);

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
              throw Invalid(tok);
            val.Convert(vmode, mode);

            using var emitter = ctx.Emitter(Data.Type);
            for (var w = 0; w < width; w++)
              emitter.Emit(val);
          }
          break;
        default:
          throw Invalid(Data.Value);
      }
      throw new NotImplementedException();
    }

    private int StringLength(Context ctx, Token token) =>
      Token.TryCalcStringLength(ctx.Buffer[token], out var length) ? length : throw Invalid(token);

    private int CalcSize(Context ctx) => Data switch
    {
      { ExprVal: FixedRange expr } => typeSize * expr.Length,
      { Value: Token { Type: TokenType.String } tok, Type: DataType.U8 } =>
        StringLength(ctx, tok) * typeSize,
      { Value: Token { Type: TokenType.String } tok, Type: DataType.S48 } =>
        (StringLength(ctx, tok) * DataType.U8.SizeBytes()) + typeSize, // N S48 pointers + N copies of string
      { Value: Token { Type: TokenType.String } tok } => throw Invalid(tok),
      _ => typeSize,
    } * width;

    // TODO
    private Exception Invalid(Token tok) => throw new InvalidOperationException($"Invalid token {tok.Type}");
  }

  public class InstructionStatement(ParsedInstruction parsedInst) : Statement
  {
    public readonly ParsedInstruction Inst = parsedInst;

    public override void FirstPass(Context ctx)
    {
      throw new NotImplementedException();
    }

    public override void SecondPass(Context ctx)
    {
      throw new NotImplementedException();
    }
  }

  // public class InstructionStatement : Statement
  // {
  //   public Context Context;
  //   public Token OpToken;
  //   public OpCode Op;
  //   public int Width = 1;
  //   public DataType? Type;
  //   public ParsedOperand OperandA;
  //   public ParsedOperand OperandB;

  //   public override void FirstPass(Context ctx)
  //   {
  //     Context = ctx;
  //     if (OperandA.Mode == ParsedOpMode.Const)
  //       RegisterConst(ctx, OperandA.Consts, AType);
  //     if (OperandB.Mode == ParsedOpMode.Const)
  //       RegisterConst(ctx, OperandB.Consts, BType);
  //     ctx.Addr += DataType.U64.SizeBytes();
  //   }

  //   private void RegisterConst(Context ctx, ConstExprList consts, DataType type)
  //   {
  //     if (consts.Indirect || consts.Addr) type = DataType.P24;
  //     ctx.RegisterConst(type, consts, Width);
  //   }

  //   private Exception Invalid(string message) =>
  //     throw new InvalidOperationException($"{message} at {TokenProcessor.StackPos(Context.Buffer, OpToken)}");

  //   private DataType AType => OperandA.Mode != ParsedOpMode.Placeholder
  //     ? Type ?? OperandA.Type ?? throw Invalid($"Missing A Type")
  //     : Type ?? OperandB.Type ?? throw Invalid($"Missing A Type");
  //   private DataType BType =>
  //     Type ?? OperandB.Type ?? OperandA.Type ?? throw Invalid($"Missing B Type");

  //   public override void SecondPass(Context ctx)
  //   {
  //     var inst = new Instruction { OpCode = Op, DataWidth = (byte)Width };

  //     if (Type != null && (OperandA.Type ?? OperandB.Type) != null)
  //       throw Invalid($"Cannot have instruction-level and operand-level types");

  //     inst.AType = AType;
  //     inst.BType = BType;

  //     switch ((OperandA.Mode, OperandB.Mode))
  //     {
  //       case (ParsedOpMode.Placeholder, ParsedOpMode.Addr):
  //         ParseBSimple(ctx, ref inst);
  //         CopyBToA(ctx, ref inst);
  //         break;
  //       case (ParsedOpMode.Placeholder, ParsedOpMode.Const):
  //         ParseBConst(ctx, ref inst);
  //         CopyBToA(ctx, ref inst);
  //         break;
  //       case (ParsedOpMode.Addr, ParsedOpMode.Addr):
  //         ParseASimple(ctx, ref inst);
  //         ParseBSimple(ctx, ref inst);
  //         inst.OffsetB -= inst.AddrBase;
  //         break;
  //       case (ParsedOpMode.Addr, ParsedOpMode.Offset):
  //         ParseASimple(ctx, ref inst);
  //         ParseBSimple(ctx, ref inst);
  //         break;
  //       case (ParsedOpMode.Addr, ParsedOpMode.Const):
  //         ParseASimple(ctx, ref inst);
  //         ParseBConst(ctx, ref inst);
  //         inst.OffsetB -= inst.AddrBase;
  //         break;
  //       case (ParsedOpMode.Addr, ParsedOpMode.Placeholder):
  //         ParseASimple(ctx, ref inst);
  //         CopyAToB(ctx, ref inst);
  //         break;
  //       case (ParsedOpMode.BaseOffset, ParsedOpMode.Offset):
  //         ParseABaseOffset(ctx, ref inst);
  //         ParseBSimple(ctx, ref inst);
  //         break;
  //       case (ParsedOpMode.Const, ParsedOpMode.Placeholder):
  //         ParseASimple(ctx, ref inst);
  //         CopyAToB(ctx, ref inst);
  //         break;
  //       case (ParsedOpMode.Const, ParsedOpMode.Addr):
  //         ParseAConst(ctx, ref inst);
  //         ParseBSimple(ctx, ref inst);
  //         inst.OffsetB -= inst.AddrBase;
  //         break;
  //       case (ParsedOpMode.Const, ParsedOpMode.Const):
  //         ParseAConst(ctx, ref inst);
  //         ParseBConst(ctx, ref inst);
  //         inst.OffsetB -= inst.AddrBase;
  //         break;
  //       default:
  //         throw Invalid($"Invalid operand modes ({OperandA.Mode},{OperandB.Mode})");
  //     }

  //     ctx.EmitInst(OpToken, inst.Encode());
  //   }

  //   private int LookupAddr(Context ctx, AddrRef addr)
  //   {
  //     var val = addr.IntAddr;
  //     if (addr.StrAddr != null)
  //     {
  //       if (!ctx.Labels.TryGetValue(addr.StrAddr, out val))
  //         throw Invalid($"Unknown name {addr.StrAddr}");
  //     }
  //     if (addr.Offset == "-")
  //       val = -val;
  //     return val;
  //   }

  //   private void CopyAToB(Context ctx, ref Instruction inst)
  //   {
  //     inst.OffsetB = 0;
  //     if (inst.OperandMode.HasFlag(OperandMode.IndirectA))
  //       inst.OperandMode |= OperandMode.IndirectB;
  //   }

  //   private void CopyBToA(Context ctx, ref Instruction inst)
  //   {
  //     inst.AddrBase = inst.OffsetB;
  //     inst.OffsetB = 0;
  //     if (inst.OperandMode.HasFlag(OperandMode.IndirectB))
  //       inst.OperandMode |= OperandMode.IndirectA;
  //   }

  //   private void ParseASimple(Context ctx, ref Instruction inst)
  //   {
  //     inst.AddrBase = LookupAddr(ctx, OperandA.Addr);
  //     if (OperandA.Addr.Indirect)
  //       inst.OperandMode |= OperandMode.IndirectA;
  //   }

  //   private void ParseBSimple(Context ctx, ref Instruction inst)
  //   {
  //     inst.OffsetB = LookupAddr(ctx, OperandB.Addr);
  //     if (OperandB.Addr.Indirect)
  //       inst.OperandMode |= OperandMode.IndirectB;
  //   }

  //   private void ParseABaseOffset(Context ctx, ref Instruction inst)
  //   {
  //     inst.OperandMode |= OperandMode.AddrBaseOffAB;

  //     if (OperandA.Base.Offset != null)
  //       throw Invalid($"Cannot have offset base");

  //     inst.AddrBase = LookupAddr(ctx, OperandA.Base);
  //     if (OperandA.Base.Indirect)
  //       inst.BaseIndirect = true;

  //     if (OperandA.Addr.Offset == null)
  //       throw Invalid($"Offset addr must have offset direction");

  //     inst.OffsetA = LookupAddr(ctx, OperandA.Addr);
  //     if (OperandA.Addr.Indirect)
  //       inst.OperandMode |= OperandMode.IndirectA;
  //   }

  //   private void ParseAConst(Context ctx, ref Instruction inst)
  //   {
  //     inst.AddrBase = LookupConst(ctx, OperandA.Consts, inst.AType);
  //     if (OperandA.Consts.Indirect)
  //       inst.OperandMode |= OperandMode.IndirectA;
  //   }

  //   private void ParseBConst(Context ctx, ref Instruction inst)
  //   {
  //     inst.OffsetB = LookupConst(ctx, OperandB.Consts, inst.BType);
  //     if (OperandB.Consts.Indirect)
  //       inst.OperandMode |= OperandMode.IndirectB;
  //   }

  //   private static int LookupConst(Context ctx, ConstExprList consts, DataType type)
  //   {
  //     if (consts.Indirect || consts.Addr) type = DataType.P24;
  //     return ctx.ConstExprAddrs[(type, consts)];
  //   }
  // }
}