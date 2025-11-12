
using System;

namespace KSACPU
{
  public static class KSACPUMain
  {
    public static void Main(string[] args)
    {
      var proc = new Processor();

      var val = new Value();

      proc.Memory.WriteU8(100, 3);
      proc.Memory.WriteU8(101, 5);
      proc.Memory.WriteU64(102, 0x0100010001000100);

      proc.Memory.WriteU64(0, new Instruction
      {
        OpCode = OpCode.Copy,
        DataWidth = 2,
        AType = DataType.F64,
        BType = DataType.U8,
        OperandMode = OperandMode.DirectA | OperandMode.DirectB | OperandMode.AddrBaseOffAB,
        AddrBase = 0,
        OffsetA = 200,
        OffsetB = 100,
      }.Encode());
      proc.Memory.WriteU64(8, new Instruction
      {
        OpCode = OpCode.Reorder,
        DataWidth = 8,
        AType = DataType.F64,
        BType = DataType.U8,
        OperandMode = OperandMode.DirectA | OperandMode.DirectB | OperandMode.AddrBaseOffAB,
        AddrBase = 0,
        OffsetA = 200,
        OffsetB = 102,
      }.Encode());
      proc.Memory.WriteU64(16, new Instruction
      {
        OpCode = OpCode.Add,
        DataWidth = 8,
        AType = DataType.F64,
        BType = DataType.F64,
        OperandMode = OperandMode.DirectA | OperandMode.DirectB | OperandMode.AddrBaseOffAB,
        AddrBase = 200,
        OffsetA = 0,
        OffsetB = 0,
      }.Encode());

      proc.Step();
      val.Load(proc.Memory, new() { Address = 200, Width = 8, Type = DataType.F64 });
      Console.WriteLine(val.ToString());

      proc.Step();
      val.Load(proc.Memory, new() { Address = 200, Width = 8, Type = DataType.F64 });
      Console.WriteLine(val.ToString());

      proc.Step();
      val.Load(proc.Memory, new() { Address = 200, Width = 8, Type = DataType.F64 });
      Console.WriteLine(val.ToString());
    }
  }
}