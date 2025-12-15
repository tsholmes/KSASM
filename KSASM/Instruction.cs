
using KSASM.Assembly;

namespace KSASM
{
  public struct Instruction
  {
    public OpCode OpCode;
    public byte Width;
    public byte ImmCount;
    public DataType AType;
    public DataType BType;
    public DataType CType;

    public static class Encoding
    {
      public static readonly ShiftMask OpCode = new() { Shift = 0, Bits = 7 };
      public static readonly ShiftMask Width = new() { Shift = 7, Bits = 3 };
      public static readonly ShiftMask ImmCount = new() { Shift = 10, Bits = 2 };
      public static readonly ShiftMask AType = new() { Shift = 12, Bits = 4 };
      public static readonly ShiftMask BType = new() { Shift = 16, Bits = 4 };
      public static readonly ShiftMask CType = new() { Shift = 20, Bits = 4 };
    }

    public static Instruction Decode(ulong encoded) => new()
    {
      OpCode = (OpCode)Encoding.OpCode.Decode(encoded),
      Width = (byte)Encoding.Width.Decode(encoded),
      ImmCount = (byte)Encoding.ImmCount.Decode(encoded),
      AType = (DataType)Encoding.AType.Decode(encoded),
      BType = (DataType)Encoding.BType.Decode(encoded),
      CType = (DataType)Encoding.CType.Decode(encoded),
    };

    public readonly ulong Encode() =>
      Encoding.OpCode.Encode((ulong)OpCode) |
      Encoding.Width.Encode(Width) |
      Encoding.ImmCount.Encode(ImmCount) |
      Encoding.AType.Encode((ulong)AType) |
      Encoding.BType.Encode((ulong)BType) |
      Encoding.CType.Encode((ulong)CType);

    public void Format(ref LineBuilder line, DebugSymbols debug = null)
    {
      // TODO

      // var sameTypes = AType == BType;

      // line.Add(OpCode, "g");

      // if (DataWidth > 1)
      // {
      //   line.Add('*');
      //   line.Add(DataWidth, "g");
      // }

      // if (sameTypes)
      // {
      //   line.Add(':');
      //   line.Add(AType, "g");
      // }

      // line.Sp();

      // var ia = OperandMode.HasFlag(OperandMode.IndirectA);
      // var ib = OperandMode.HasFlag(OperandMode.IndirectB);

      // if (OperandMode.HasFlag(OperandMode.AddrBaseOffAB))
      // {
      //   if (BaseIndirect) line.Add('[');
      //   line.AddAddr(AddrBase, debug);
      //   if (BaseIndirect) line.Add(']');

      //   if (ia) line.Add('[');
      //   if (OffsetA >= 0) line.Add('+');
      //   line.Add(OffsetA, "g");
      //   if (ia) line.Add(']');
      //   if (!sameTypes)
      //   {
      //     line.Add(':');
      //     line.Add(AType, "g");
      //   }

      //   line.Add(',');
      //   line.Sp();

      //   if (ib) line.Add('[');
      //   if (OffsetB >= 0) line.Add('+');
      //   line.Add(OffsetB, "g");
      //   if (ib) line.Add(']');
      //   if (!sameTypes)
      //   {
      //     line.Add(':');
      //     line.Add(BType, "g");
      //   }
      // }
      // else
      // {
      //   if (ia) line.Add('[');
      //   line.AddAddr(AddrBase, debug);
      //   if (ia) line.Add(']');
      //   if (!sameTypes)
      //   {
      //     line.Add(':');
      //     line.Add(AType, "g");
      //   }

      //   line.Add(',');
      //   line.Sp();

      //   if (ib) line.Add('[');
      //   line.AddAddr(AddrBase + OffsetB, debug);
      //   if (ib) line.Add(']');
      //   if (!sameTypes)
      //   {
      //     line.Add(':');
      //     line.Add(BType, "g");
      //   }
      // }
    }
  }
}