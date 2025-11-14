
using System;

namespace KSACPU
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

    public class InstructionStatement : Statement
    {
      public OpCode Op;
      public int Width = 1;
      public DataType? Type;
      public ParsedOperand OperandA;
      public ParsedOperand OperandB;

      public override void FirstPass(State state) =>
        state.Addr += DataType.U64.SizeBytes();

      public override void SecondPass(State state)
      {
        var inst = new Instruction { OpCode = Op, DataWidth = (byte)Width };

        if (Type != null && (OperandA?.Type ?? OperandB?.Type) != null)
          throw new InvalidOperationException($"Cannot have instruction-level and operand-level types");

        if (OperandA != null)
          inst.AType = Type ?? OperandA?.Type ?? throw new InvalidOperationException($"Missing A Type");
        else
          inst.AType = Type ?? OperandB?.Type ?? throw new InvalidOperationException($"Missing A Type");

        inst.BType = Type ?? OperandB?.Type ?? OperandA?.Type
          ?? throw new InvalidOperationException($"Missing B Type");

        if (OperandA == null && OperandB == null)
          throw new InvalidOperationException($"Cannot have both operands as placeholders");

        if (OperandB?.Base != null)
          throw new InvalidOperationException($"Cannot have base+offset on operand B");

        if (OperandA != null)
        {
          if (OperandA.Base != null)
          {
            inst.OperandMode = OperandMode.AddrBaseOffAB;

            if (OperandA.Base.Offset != null)
              throw new InvalidOperationException($"Cannot have offset base");

            inst.AddrBase = LookupAddr(state, OperandA.Base);
            if (OperandA.Base.Indirect)
              inst.BaseIndirect = true;

            if (OperandA.Addr.Offset == null)
              throw new InvalidOperationException($"Offset addr must have offset direction");

            inst.OffsetA = LookupAddr(state, OperandA.Addr);
            if (OperandA.Addr.Indirect)
              inst.OperandMode |= OperandMode.IndirectA;
          }
          else
          {
            inst.OperandMode = OperandMode.AddrAOffB;

            if (OperandA.Addr.Offset != null)
              throw new InvalidOperationException($"Cannot have offset without base");

            inst.AddrBase = LookupAddr(state, OperandA.Addr);
            if (OperandA.Addr.Indirect)
              inst.OperandMode |= OperandMode.IndirectA;
          }

          if (OperandB == null)
          {
            if (OperandA.Base != null)
              throw new InvalidOperationException($"Cannot have placeholder B with base+offset A");

            inst.OffsetB = 0;
            if (inst.OperandMode.HasFlag(OperandMode.IndirectA))
              inst.OperandMode |= OperandMode.IndirectB;
          }
          else
          {
            if (OperandA.Base != null && OperandB.Addr.Offset == null)
              throw new InvalidOperationException($"B must have offset in base+offset mode");
            inst.OffsetB = LookupAddr(state, OperandB.Addr);
            if (OperandB.Addr.Indirect)
              inst.OperandMode |= OperandMode.IndirectB;

            if (OperandB.Addr.Offset == null)
              inst.OffsetB -= inst.AddrBase;
          }
        }
        else
        {
          // placeholder A
          if (OperandB.Addr.Offset != null)
            throw new InvalidOperationException($"Cannot have offset B with placeholder A");

          inst.OperandMode = OperandMode.AddrAOffB;

          inst.AddrBase = LookupAddr(state, OperandB.Addr);
          inst.OffsetB = 0;
          if (OperandB.Addr.Indirect)
            inst.OperandMode |= OperandMode.IndirectA | OperandMode.IndirectB;
        }

        state.Emit(DataType.U64, new() { Unsigned = inst.Encode() });
      }

      private int LookupAddr(State state, AddrRef addr)
      {
        var val = addr.IntAddr;
        if (addr.StrAddr != null)
        {
          if (!state.Labels.TryGetValue(addr.StrAddr, out val))
            throw new InvalidOperationException($"Unknown name {addr.StrAddr}");
        }
        if (addr.Offset == "-")
          val = -val;
        return val;
      }
    }
  }
}