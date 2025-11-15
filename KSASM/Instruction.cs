
namespace KSASM
{
  public struct Instruction
  {
    public OpCode OpCode;
    public byte DataWidth;
    public DataType AType;
    public DataType BType;
    public OperandMode OperandMode;

    // A when OperandMode is AddrAOffB
    // Base when OperandMode is AddrBaseOffAB
    public int AddrBase;

    // Wether base is indirect when OperandMode is AddrBaseOffAB
    // ignored otherwise
    public bool BaseIndirect;

    // OffA when OperandMode is AddrBaseOffAB
    // ignored otherwise
    public int OffsetA;

    // OffB in both OperandMods
    public int OffsetB;

    // OpCode: 63-57
    private static readonly Encoding.ShiftMask OpCodeEncoding = new() { Shift = 57, Bits = 7 };
    // DataWidth: 56-54
    private static readonly Encoding.ShiftMask DataWidthEncoding = new() { Shift = 54, Bits = 3 };
    // AType: 53-51
    private static readonly Encoding.ShiftMask ATypeEncoding = new() { Shift = 51, Bits = 3 };
    // BType: 50-48
    private static readonly Encoding.ShiftMask BTypeEncoding = new() { Shift = 48, Bits = 3 };
    // OperandMode: 47-45
    private static readonly Encoding.ShiftMask OperandModeEncoding = new() { Shift = 45, Bits = 3 };

    // AddrAOffB OffB: 44-24
    private static readonly Encoding.ShiftMask WideOffBEncoding = new() { Shift = 24, Bits = 21 };

    // AddrBaseOffAB BaseIndirect 44
    private static readonly Encoding.ShiftMask BaseIndirectEncoding = new() { Shift = 44, Bits = 1 };
    // AddrBaseOffAB OffB: 43-34
    private static readonly Encoding.ShiftMask NarrowOffBEncoding = new() { Shift = 34, Bits = 10 };
    // AddrBaseOffAB OffA: 33-24
    private static readonly Encoding.ShiftMask NarrowOffAEncoding = new() { Shift = 24, Bits = 10 };

    // AddrBase/AddrA: 23-0
    private static readonly Encoding.ShiftMask BaseEncoding = new() { Shift = 0, Bits = 24 };

    public static Instruction Decode(ulong encoded)
    {
      var inst = new Instruction
      {
        OpCode = (OpCode)OpCodeEncoding.Decode(encoded),
        DataWidth = (byte)(DataWidthEncoding.Decode(encoded) + 1u),
        BType = (DataType)BTypeEncoding.Decode(encoded),
        AType = (DataType)ATypeEncoding.Decode(encoded),
        OperandMode = (OperandMode)OperandModeEncoding.Decode(encoded),
      };

      if (inst.OperandMode.HasFlag(OperandMode.AddrBaseOffAB))
      {
        inst.BaseIndirect = BaseIndirectEncoding.Decode(encoded) != 0;
        inst.OffsetB = NarrowOffBEncoding.DecodeSignExtend(encoded);
        inst.OffsetA = NarrowOffAEncoding.DecodeSignExtend(encoded);
      }
      else
      {
        inst.OffsetB = WideOffBEncoding.DecodeSignExtend(encoded);
      }

      inst.AddrBase = (int)BaseEncoding.Decode(encoded);

      return inst;
    }

    public ulong Encode()
    {
      var encoded = OpCodeEncoding.Encode((ulong)OpCode)
        | DataWidthEncoding.Encode(DataWidth - 1u)
        | BTypeEncoding.Encode((ulong)BType)
        | ATypeEncoding.Encode((ulong)AType)
        | OperandModeEncoding.Encode((ulong)OperandMode);

      if (OperandMode.HasFlag(OperandMode.AddrBaseOffAB))
      {
        encoded |= BaseIndirectEncoding.Encode(BaseIndirect ? 1u : 0)
          | NarrowOffBEncoding.Encode((ulong)OffsetB)
          | NarrowOffAEncoding.Encode((ulong)OffsetA);
      }
      else
      {
        encoded |= WideOffBEncoding.Encode((ulong)OffsetB);
      }

      encoded |= BaseEncoding.Encode((ulong)AddrBase);

      return encoded;
    }
  }
}