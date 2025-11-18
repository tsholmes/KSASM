
using System;
using System.Collections.Generic;

namespace KSASM
{
  public partial class Processor
  {
    public const int MAIN_MEM_SIZE = 1 << 24;

    public static bool DebugOps = false;

    public readonly ByteArrayMemory MainMemory;
    public readonly MappedMemory MappedMemory;
    public readonly MemoryAccessor Memory;
    public int PC = 0;
    public ulong SleepTime = 0;

    private readonly ValArray A = new();
    private readonly ValArray B = new();
    private readonly ValArray C = new();

    public Action<ValArray, ValArray> OnDebug;

    private readonly Dictionary<ulong, IDevice> deviceMap = [];
    private readonly IDevice defaultDevice = new NullDevice();

    public Processor(params IDevice[] devices)
    {
      MainMemory = new(MAIN_MEM_SIZE);
      MappedMemory = new();
      Memory = new(MappedMemory);

      MappedMemory.MapRange(0, MainMemory, 0, MAIN_MEM_SIZE);

      foreach (var device in devices)
        deviceMap.Add(device.Id, device);
    }

    public void Step()
    {
      var inst = Instruction.Decode(Memory.Read(PC, DataType.U64).Unsigned);
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
          addrBase = (int)Memory.Read(addrBase, DataType.P24).Unsigned;
        addrA = addrBase + inst.OffsetA;
        addrB = addrBase + inst.OffsetB;
      }
      else
      {
        addrA = inst.AddrBase;
        addrB = inst.AddrBase + inst.OffsetB;
      }

      if (inst.OperandMode.HasFlag(OperandMode.IndirectA))
        addrA = (int)Memory.Read(addrA, DataType.P24).Unsigned;
      if (inst.OperandMode.HasFlag(OperandMode.IndirectB))
        addrB = (int)Memory.Read(addrB, DataType.P24).Unsigned;

      return (
        new ValuePointer { Address = addrA, Type = inst.AType, Width = inst.DataWidth },
        new ValuePointer { Address = addrB, Type = inst.BType, Width = inst.DataWidth }
      );
    }

    private void Exec(OpCode op, ValuePointer opA, ValuePointer opB)
    {
      if (DebugOps)
        Console.WriteLine($"{PC - 8}: {op}*{opA.Width} {opA.Address},{opA.Type} {opB.Address},{opB.Type}");
      switch (op)
      {
        case OpCode.Copy: OpCopy(opA, opB); break;
        case OpCode.Reorder: OpReorder(opA, opB); break;
        case OpCode.BitNot: OpBitNot(opA, opB); break;
        case OpCode.Negate: OpNegate(opA, opB); break;
        case OpCode.Conjugate: OpConjugate(opA, opB); break;
        case OpCode.Sign: OpSign(opA, opB); break;
        case OpCode.Abs: OpAbs(opA, opB); break;
        case OpCode.BitAnd: OpBitAnd(opA, opB); break;
        case OpCode.BitOr: OpBitOr(opA, opB); break;
        case OpCode.BitXor: OpBitXor(opA, opB); break;
        case OpCode.ShiftLeft: OpShiftLeft(opA, opB); break;
        case OpCode.ShiftRight: OpShiftRight(opA, opB); break;
        case OpCode.Add: OpAdd(opA, opB); break;
        case OpCode.Subtract: OpSubtract(opA, opB); break;
        case OpCode.Multiply: OpMultiply(opA, opB); break;
        case OpCode.Divide: OpDivide(opA, opB); break;
        case OpCode.Remainder: OpRemainder(opA, opB); break;
        case OpCode.Modulus: OpModulus(opA, opB); break;
        case OpCode.Power: OpPower(opA, opB); break;
        case OpCode.Max: OpMax(opA, opB); break;
        case OpCode.Min: OpMin(opA, opB); break;
        case OpCode.UFpu: OpUFpu(opA, opB); break;
        case OpCode.All: OpAll(opA, opB); break;
        case OpCode.Any: OpAny(opA, opB); break;
        case OpCode.Parity: OpParity(opA, opB); break;
        case OpCode.Sum: OpSum(opA, opB); break;
        case OpCode.Product: OpProduct(opA, opB); break;
        case OpCode.MinAll: OpMinAll(opA, opB); break;
        case OpCode.MaxAll: OpMaxAll(opA, opB); break;
        case OpCode.Jump: OpJump(opA, opB); break;
        case OpCode.Call: OpCall(opA, opB); break;
        case OpCode.BranchIfZero: OpBranchIfSign(opA, opB, 0); break;
        case OpCode.BranchIfPos: OpBranchIfSign(opA, opB, 1); break;
        case OpCode.BranchIfNeg: OpBranchIfSign(opA, opB, -1); break;
        case OpCode.Switch: OpSwitch(opA, opB); break;
        case OpCode.Sleep: OpSleep(opA, opB); break;
        case OpCode.DevID: OpDevID(opA, opB); break;
        case OpCode.DevType: OpDevType(opA, opB); break;
        case OpCode.DevMap: OpDevMap(opA, opB); break;
        case OpCode.IHandler: OpIHandler(opA, opB); break;
        case OpCode.IData: OpIData(opA, opB); break;
        case OpCode.IReturn: OpIReturn(opA, opB); break;
        case OpCode.Debug: OpDebug(opA, opB); break;
        default:
          throw new InvalidOperationException($"{op}");
      }
    }
  }
}