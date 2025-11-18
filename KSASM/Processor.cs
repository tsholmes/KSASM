
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
        case OpCode.Subtract: OpSubtract(opA, opB); break;
        case OpCode.Multiply: OpMultiply(opA, opB); break;
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
        case OpCode.Jump: OpJump(opA, opB); break;
        case OpCode.Call: OpCall(opA, opB); break;
        case OpCode.BranchIfZero: OpBranchIfSign(opA, opB, 0); break;
        case OpCode.BranchIfPos: OpBranchIfSign(opA, opB, 1); break;
        case OpCode.BranchIfNeg: OpBranchIfSign(opA, opB, -1); break;
        case OpCode.Switch: goto default;
        case OpCode.Sleep: OpSleep(opA, opB); break;
        case OpCode.DevID: goto default;
        case OpCode.DevType: goto default;
        case OpCode.DevMap: OpDevMap(opA, opB); break;
        case OpCode.IHandler: goto default;
        case OpCode.IData: goto default;
        case OpCode.IReturn: goto default;
        case OpCode.Debug: OpDebug(opA, opB); break;
        default:
          throw new NotImplementedException($"{op}");
      }
    }
  }
}