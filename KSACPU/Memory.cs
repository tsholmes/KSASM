
namespace KSACPU
{
  public class Memory
  {
    public const int SIZE = 1 << 24;
    public const int ADDR_MASK = SIZE - 1;

    private readonly byte[] data = new byte[SIZE];

    public ulong ReadU8(int address) => Encoding.U8.Decode(data, address & ADDR_MASK);
    public long ReadI16(int address) => Encoding.I16.Decode(data, address & ADDR_MASK);
    public long ReadI32(int address) => Encoding.I32.Decode(data, address & ADDR_MASK);
    public long ReadI64(int address) => Encoding.I64.Decode(data, address & ADDR_MASK);
    public ulong ReadU64(int address) => Encoding.U64.Decode(data, address & ADDR_MASK);
    public double ReadF64(int address) => Encoding.F64.Decode(data, address & ADDR_MASK);
    public ulong ReadP24(int address) => Encoding.P24.Decode(data, address & ADDR_MASK);
    // public ulong ReadC128(int address) => Encoding.C128.Decode(data, address & ADDR_MASK);

    public void WriteU8(int address, ulong val) => Encoding.U8.Encode(val, data, address & ADDR_MASK);
    public void WriteI16(int address, long val) => Encoding.I16.Encode(val, data, address & ADDR_MASK);
    public void WriteI32(int address, long val) => Encoding.I32.Encode(val, data, address & ADDR_MASK);
    public void WriteI64(int address, long val) => Encoding.I64.Encode(val, data, address & ADDR_MASK);
    public void WriteU64(int address, ulong val) => Encoding.U64.Encode(val, data, address & ADDR_MASK);
    public void WriteF64(int address, double val) => Encoding.F64.Encode(val, data, address & ADDR_MASK);
    public void WriteP24(int address, ulong val) => Encoding.P24.Encode(val, data, address & ADDR_MASK);
    // public void WriteC128(int address, double val) => Encoding.C128.Encode(val, data, address & ADDR_MASK);
  }
}