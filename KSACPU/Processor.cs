
using System;

namespace KSACPU
{
  public partial class Processor
  {
    public readonly Memory Memory = new();
    public int PC = 0;

    private readonly Value A = new();
    private readonly Value B = new();
    private readonly Value C = new();

    public void Step()
    {
      var inst = Instruction.Decode(Memory.ReadU64(PC));
      PC += 8;

      var (opA, opB) = OperandPointers(inst);
      Exec(inst.OpCode, opA, opB);
    }

    private (ValuePointer, ValuePointer) OperandPointers(Instruction inst)
    {
      int addrA, addrB;

      if (inst.OperandMode.HasFlag(OperandMode.AddrBaseOffAB))
      {
        var addrBase = inst.AddrBase;
        if (inst.BaseIndirect)
          addrBase = (int)Memory.ReadP24(addrBase);
        addrA = addrBase + inst.OffsetA;
        addrB = addrBase + inst.OffsetB;
      }
      else
      {
        addrA = inst.AddrBase;
        addrB = inst.AddrBase + inst.OffsetB;
      }

      if (inst.OperandMode.HasFlag(OperandMode.IndirectA))
        addrA = (int)Memory.ReadP24(addrA);
      if (inst.OperandMode.HasFlag(OperandMode.IndirectB))
        addrB = (int)Memory.ReadP24(addrB);

      return (
        new ValuePointer { Address = addrA, Type = inst.AType, Width = inst.DataWidth },
        new ValuePointer { Address = addrB, Type = inst.BType, Width = inst.DataWidth }
      );
    }

    private void Exec(OpCode op, ValuePointer opA, ValuePointer opB)
    {
      Console.WriteLine($"{op} {opA.Address},{opA.Type} {opB.Address},{opB.Type}");
      switch (op)
      {
        case OpCode.Copy: OpCopy(opA, opB); break;
        case OpCode.Reorder: OpReorder(opA, opB); break;
        case OpCode.BitNot: goto default;
        case OpCode.Negate: goto default;
        case OpCode.Conjugate: goto default;
        case OpCode.Sign: goto default;
        case OpCode.Abs: goto default;
        case OpCode.BitAnd: goto default;
        case OpCode.BitOr: goto default;
        case OpCode.BitXor: goto default;
        case OpCode.ShiftLeft: goto default;
        case OpCode.ShiftRight: goto default;
        case OpCode.Add: OpAdd(opA, opB); break;
        case OpCode.Subtract: goto default;
        case OpCode.Multiply: goto default;
        case OpCode.Divide: goto default;
        case OpCode.Remainder: goto default;
        case OpCode.Modulus: goto default;
        case OpCode.Power: goto default;
        case OpCode.Max: goto default;
        case OpCode.Min: goto default;
        case OpCode.UFpu: goto default;
        case OpCode.All: goto default;
        case OpCode.Any: goto default;
        case OpCode.Parity: goto default;
        case OpCode.Sum: goto default;
        case OpCode.Product: goto default;
        case OpCode.MinAll: goto default;
        case OpCode.MaxAll: goto default;
        case OpCode.Jump: goto default;
        case OpCode.Call: goto default;
        case OpCode.BranchIfZero: goto default;
        case OpCode.BranchIfPos: goto default;
        case OpCode.BranchIfNeg: goto default;
        case OpCode.Switch: goto default;
        case OpCode.Sleep: goto default;
        case OpCode.DevID: goto default;
        case OpCode.DevType: goto default;
        case OpCode.DevRead: goto default;
        case OpCode.DevWrite: goto default;
        case OpCode.IHandler: goto default;
        case OpCode.IData: goto default;
        case OpCode.IReturn: goto default;
        default:
          throw new NotImplementedException($"{op}");
      }
    }
  }
}