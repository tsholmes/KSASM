
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

  public class ValueStatement : Statement
  {
    public Token Token;
    public DataType Type;
    public string StrValue;
    public Value Value;
    public int Width = 1;

    public override void FirstPass(Context ctx) =>
      ctx.Addr += Type.SizeBytes() * Width;

    public override void SecondPass(Context ctx)
    {
      if (StrValue != null)
      {
        if (!ctx.Labels.TryGetValue(StrValue, out var addr))
          throw new InvalidOperationException($"Unknown name {StrValue}");
        Value.Unsigned = (ulong)addr;
        Value.Convert(ValueMode.Unsigned, Type.VMode());
      }
      Span<Value> vals = Width <= 256 ? stackalloc Value[Width] : new Value[Width];
      for (var i = 0; i < Width; i++)
        vals[i] = Value;
      ctx.Emit(Type, vals);
    }
  }

  public class ExprValueStatement : Statement
  {
    public DataType Type;
    public int Width = 1;
    public ConstExprList Exprs;

    public override void FirstPass(Context ctx) =>
      ctx.Addr += Type.SizeBytes() * Width;

    public override void SecondPass(Context ctx)
    {
      var totalCount = Exprs.Count * Width;
      Span<Value> evals = totalCount <= 256 ? stackalloc Value[totalCount] : new Value[totalCount];
      for (var i = 0; i < Exprs.Count; i++)
        evals[i] = ctx.EvalExpr(Exprs[i], Type.VMode());
      for (var i = Exprs.Count; i < totalCount; i++)
        evals[i] = evals[i % Exprs.Count];

      ctx.Emit(Type, evals);
    }
  }

  public class ValueListStatement : Statement
  {
    public Token Token;
    public DataType Type;
    public List<Value> Values;

    public override void FirstPass(Context ctx) =>
      ctx.Addr += Type.SizeBytes() * Values.Count;

    public override void SecondPass(Context ctx) =>
      ctx.Emit(Type, Values);
  }

  public class InstructionStatement : Statement
  {
    public Context Context;
    public Token OpToken;
    public OpCode Op;
    public int Width = 1;
    public DataType? Type;
    public ParsedOperand OperandA;
    public ParsedOperand OperandB;

    public override void FirstPass(Context ctx)
    {
      if (OperandA.Mode == ParsedOpMode.Const)
        RegisterConst(ctx, OperandA.Consts, AType);
      if (OperandB.Mode == ParsedOpMode.Const)
        RegisterConst(ctx, OperandB.Consts, BType);
      ctx.Addr += DataType.U64.SizeBytes();
    }

    private void RegisterConst(Context ctx, ConstExprList consts, DataType type)
    {
      if (consts.Indirect || consts.Addr) type = DataType.P24;
      ctx.RegisterConst(type, consts, Width);
    }

    private Exception Invalid(string message) =>
      throw new InvalidOperationException($"{message} at {Context.StackPos(OpToken)}");

    private DataType AType => OperandA.Mode != ParsedOpMode.Placeholder
      ? Type ?? OperandA.Type ?? throw Invalid($"Missing A Type")
      : Type ?? OperandB.Type ?? throw Invalid($"Missing A Type");
    private DataType BType =>
      Type ?? OperandB.Type ?? OperandA.Type ?? throw Invalid($"Missing B Type");

    public override void SecondPass(Context ctx)
    {
      var inst = new Instruction { OpCode = Op, DataWidth = (byte)Width };

      if (Type != null && (OperandA.Type ?? OperandB.Type) != null)
        throw Invalid($"Cannot have instruction-level and operand-level types");

      inst.AType = AType;
      inst.BType = BType;

      switch ((OperandA.Mode, OperandB.Mode))
      {
        case (ParsedOpMode.Placeholder, ParsedOpMode.Addr):
          ParseBSimple(ctx, ref inst);
          CopyBToA(ctx, ref inst);
          break;
        case (ParsedOpMode.Placeholder, ParsedOpMode.Const):
          ParseBConst(ctx, ref inst);
          CopyBToA(ctx, ref inst);
          break;
        case (ParsedOpMode.Addr, ParsedOpMode.Addr):
          ParseASimple(ctx, ref inst);
          ParseBSimple(ctx, ref inst);
          inst.OffsetB -= inst.AddrBase;
          break;
        case (ParsedOpMode.Addr, ParsedOpMode.Offset):
          ParseASimple(ctx, ref inst);
          ParseBSimple(ctx, ref inst);
          break;
        case (ParsedOpMode.Addr, ParsedOpMode.Const):
          ParseASimple(ctx, ref inst);
          ParseBConst(ctx, ref inst);
          inst.OffsetB -= inst.AddrBase;
          break;
        case (ParsedOpMode.Addr, ParsedOpMode.Placeholder):
          ParseASimple(ctx, ref inst);
          CopyAToB(ctx, ref inst);
          break;
        case (ParsedOpMode.BaseOffset, ParsedOpMode.Offset):
          ParseABaseOffset(ctx, ref inst);
          ParseBSimple(ctx, ref inst);
          break;
        case (ParsedOpMode.Const, ParsedOpMode.Placeholder):
          ParseASimple(ctx, ref inst);
          CopyAToB(ctx, ref inst);
          break;
        case (ParsedOpMode.Const, ParsedOpMode.Addr):
          ParseAConst(ctx, ref inst);
          ParseBSimple(ctx, ref inst);
          inst.OffsetB -= inst.AddrBase;
          break;
        case (ParsedOpMode.Const, ParsedOpMode.Const):
          ParseAConst(ctx, ref inst);
          ParseBConst(ctx, ref inst);
          inst.OffsetB -= inst.AddrBase;
          break;
        default:
          throw Invalid($"Invalid operand modes ({OperandA.Mode},{OperandB.Mode})");
      }

      ctx.EmitInst(OpToken, inst.Encode());
    }

    private int LookupAddr(Context ctx, AddrRef addr)
    {
      var val = addr.IntAddr;
      if (addr.StrAddr != null)
      {
        if (!ctx.Labels.TryGetValue(addr.StrAddr, out val))
          throw Invalid($"Unknown name {addr.StrAddr}");
      }
      if (addr.Offset == "-")
        val = -val;
      return val;
    }

    private void CopyAToB(Context ctx, ref Instruction inst)
    {
      inst.OffsetB = 0;
      if (inst.OperandMode.HasFlag(OperandMode.IndirectA))
        inst.OperandMode |= OperandMode.IndirectB;
    }

    private void CopyBToA(Context ctx, ref Instruction inst)
    {
      inst.AddrBase = inst.OffsetB;
      inst.OffsetB = 0;
      if (inst.OperandMode.HasFlag(OperandMode.IndirectB))
        inst.OperandMode |= OperandMode.IndirectA;
    }

    private void ParseASimple(Context ctx, ref Instruction inst)
    {
      inst.AddrBase = LookupAddr(ctx, OperandA.Addr);
      if (OperandA.Addr.Indirect)
        inst.OperandMode |= OperandMode.IndirectA;
    }

    private void ParseBSimple(Context ctx, ref Instruction inst)
    {
      inst.OffsetB = LookupAddr(ctx, OperandB.Addr);
      if (OperandB.Addr.Indirect)
        inst.OperandMode |= OperandMode.IndirectB;
    }

    private void ParseABaseOffset(Context ctx, ref Instruction inst)
    {
      inst.OperandMode |= OperandMode.AddrBaseOffAB;

      if (OperandA.Base.Offset != null)
        throw Invalid($"Cannot have offset base");

      inst.AddrBase = LookupAddr(ctx, OperandA.Base);
      if (OperandA.Base.Indirect)
        inst.BaseIndirect = true;

      if (OperandA.Addr.Offset == null)
        throw Invalid($"Offset addr must have offset direction");

      inst.OffsetA = LookupAddr(ctx, OperandA.Addr);
      if (OperandA.Addr.Indirect)
        inst.OperandMode |= OperandMode.IndirectA;
    }

    private void ParseAConst(Context ctx, ref Instruction inst)
    {
      inst.AddrBase = LookupConst(ctx, OperandA.Consts, inst.AType);
      if (OperandA.Consts.Indirect)
        inst.OperandMode |= OperandMode.IndirectA;
    }

    private void ParseBConst(Context ctx, ref Instruction inst)
    {
      inst.OffsetB = LookupConst(ctx, OperandB.Consts, inst.BType);
      if (OperandB.Consts.Indirect)
        inst.OperandMode |= OperandMode.IndirectB;
    }

    private static int LookupConst(Context ctx, ConstExprList consts, DataType type)
    {
      if (consts.Indirect || consts.Addr) type = DataType.P24;
      return ctx.ConstExprAddrs[(type, consts)];
    }
  }
}