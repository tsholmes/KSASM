
using System;

namespace KSASM
{
  public partial class Assembler
  {
    public abstract class Statement
    {
      public abstract void FirstPass(State state);
      public abstract void SecondPass(State state);
    }

    public class LabelStatement : Statement
    {
      public string Label;

      public override void FirstPass(State state) =>
        state.Labels[Label] = state.Addr;

      public override void SecondPass(State state)
      {
        if (state.Addr != state.Labels[Label])
          throw new InvalidOperationException(
            $"Label address mismatch for '{Label}': {state.Addr} != {state.Labels[Label]}");
      }
    }

    public class PositionStatement : Statement
    {
      public int Addr;

      public override void FirstPass(State state) =>
        state.Addr = Addr;

      public override void SecondPass(State state) =>
        state.Addr = Addr;
    }

    public class ValueStatement : Statement
    {
      public DataType Type;
      public string StrValue;
      public Value Value;
      public int Width = 1;

      public override void FirstPass(State state) =>
        state.Addr += Type.SizeBytes() * Width;

      public override void SecondPass(State state)
      {
        if (StrValue != null)
        {
          if (!state.Labels.TryGetValue(StrValue, out var addr))
            throw new InvalidOperationException($"Unknown name {StrValue}");
          Value.Unsigned = (ulong)addr;
          Value.Convert(ValueMode.Unsigned, Type.VMode());
        }
        for (var i = 0; i < Width; i++)
          state.Emit(Type, Value);
      }
    }

    public class ExprValueStatement : Statement
    {
      public DataType Type;
      public int Width = 1;
      public ConstExpr Expr;

      public override void FirstPass(State state) =>
        state.Addr += Type.SizeBytes() * Width;

      public override void SecondPass(State state)
      {
        var val = state.EvalExpr(Expr, Type.VMode());
        for (var i = 0; i < Width; i++)
          state.Emit(Type, val);
      }
    }

    public class InstructionStatement : Statement
    {
      public Token OpToken;
      public OpCode Op;
      public int Width = 1;
      public DataType? Type;
      public ParsedOperand OperandA;
      public ParsedOperand OperandB;

      public override void FirstPass(State state)
      {
        if (OperandA.Mode == ParsedOpMode.Const)
          RegisterConst(state, OperandA.Const, AType);
        if (OperandB.Mode == ParsedOpMode.Const)
          RegisterConst(state, OperandB.Const, BType);
        state.Addr += DataType.U64.SizeBytes();
      }

      private void RegisterConst(State state, ConstExpr cval, DataType type)
      {
        state.RegisterConst(type, cval, Width);
      }

      private Exception Invalid(string message) =>
        throw new InvalidOperationException($"{message} at {OpToken.PosStr()}");

      private DataType AType => OperandA.Mode != ParsedOpMode.Placeholder
        ? Type ?? OperandA.Type ?? throw Invalid($"Missing A Type")
        : Type ?? OperandB.Type ?? throw Invalid($"Missing A Type");
      private DataType BType =>
        Type ?? OperandB.Type ?? OperandA.Type ?? throw Invalid($"Missing B Type");

      public override void SecondPass(State state)
      {
        var inst = new Instruction { OpCode = Op, DataWidth = (byte)Width };

        if (Type != null && (OperandA.Type ?? OperandB.Type) != null)
          throw Invalid($"Cannot have instruction-level and operand-level types");

        inst.AType = AType;
        inst.BType = BType;

        switch ((OperandA.Mode, OperandB.Mode))
        {
          case (ParsedOpMode.Placeholder, ParsedOpMode.Addr):
            ParseBSimple(state, ref inst);
            CopyBToA(state, ref inst);
            break;
          case (ParsedOpMode.Placeholder, ParsedOpMode.Const):
            ParseBConst(state, ref inst);
            CopyBToA(state, ref inst);
            break;
          case (ParsedOpMode.Addr, ParsedOpMode.Addr):
            ParseASimple(state, ref inst);
            ParseBSimple(state, ref inst);
            inst.OffsetB -= inst.AddrBase;
            break;
          case (ParsedOpMode.Addr, ParsedOpMode.Offset):
            ParseASimple(state, ref inst);
            ParseBSimple(state, ref inst);
            break;
          case (ParsedOpMode.Addr, ParsedOpMode.Const):
            ParseASimple(state, ref inst);
            ParseBConst(state, ref inst);
            inst.OffsetB -= inst.AddrBase;
            break;
          case (ParsedOpMode.Addr, ParsedOpMode.Placeholder):
            ParseASimple(state, ref inst);
            CopyAToB(state, ref inst);
            break;
          case (ParsedOpMode.BaseOffset, ParsedOpMode.Offset):
            ParseABaseOffset(state, ref inst);
            ParseBSimple(state, ref inst);
            break;
          case (ParsedOpMode.Const, ParsedOpMode.Placeholder):
            ParseASimple(state, ref inst);
            CopyAToB(state, ref inst);
            break;
          case (ParsedOpMode.Const, ParsedOpMode.Addr):
            ParseAConst(state, ref inst);
            ParseBSimple(state, ref inst);
            inst.OffsetB -= inst.AddrBase;
            break;
          case (ParsedOpMode.Const, ParsedOpMode.Const):
            ParseAConst(state, ref inst);
            ParseBConst(state, ref inst);
            inst.OffsetB -= inst.AddrBase;
            break;
          default:
            throw Invalid($"Invalid operand modes ({OperandA.Mode},{OperandB.Mode})");
        }

        state.Emit(DataType.U64, new() { Unsigned = inst.Encode() });
      }

      private int LookupAddr(State state, AddrRef addr)
      {
        var val = addr.IntAddr;
        if (addr.StrAddr != null)
        {
          if (!state.Labels.TryGetValue(addr.StrAddr, out val))
            throw Invalid($"Unknown name {addr.StrAddr}");
        }
        if (addr.Offset == "-")
          val = -val;
        return val;
      }

      private void CopyAToB(State state, ref Instruction inst)
      {
        inst.OffsetB = 0;
        if (inst.OperandMode.HasFlag(OperandMode.IndirectA))
          inst.OperandMode |= OperandMode.IndirectB;
      }

      private void CopyBToA(State state, ref Instruction inst)
      {
        inst.AddrBase = inst.OffsetB;
        inst.OffsetB = 0;
        if (inst.OperandMode.HasFlag(OperandMode.IndirectB))
          inst.OperandMode |= OperandMode.IndirectA;
      }

      private void ParseASimple(State state, ref Instruction inst)
      {
        inst.AddrBase = LookupAddr(state, OperandA.Addr);
        if (OperandA.Addr.Indirect)
          inst.OperandMode |= OperandMode.IndirectA;
      }

      private void ParseBSimple(State state, ref Instruction inst)
      {
        inst.OffsetB = LookupAddr(state, OperandB.Addr);
        if (OperandB.Addr.Indirect)
          inst.OperandMode |= OperandMode.IndirectB;
      }

      private void ParseABaseOffset(State state, ref Instruction inst)
      {
        inst.OperandMode |= OperandMode.AddrBaseOffAB;

        if (OperandA.Base.Offset != null)
          throw Invalid($"Cannot have offset base");

        inst.AddrBase = LookupAddr(state, OperandA.Base);
        if (OperandA.Base.Indirect)
          inst.BaseIndirect = true;

        if (OperandA.Addr.Offset == null)
          throw Invalid($"Offset addr must have offset direction");

        inst.OffsetA = LookupAddr(state, OperandA.Addr);
        if (OperandA.Addr.Indirect)
          inst.OperandMode |= OperandMode.IndirectA;
      }

      private void ParseAConst(State state, ref Instruction inst)
      {
        inst.AddrBase = LookupConst(state, OperandA.Const, inst.AType);
      }

      private void ParseBConst(State state, ref Instruction inst)
      {
        inst.OffsetB = LookupConst(state, OperandB.Const, inst.BType);
      }

      private int LookupConst(State state, ConstExpr cval, DataType type)
      {
        return state.ConstExprAddrs[(type, cval)];
      }
    }
  }
}