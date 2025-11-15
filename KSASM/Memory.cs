
using System;

namespace KSASM
{
  public class Memory
  {
    public static bool DebugRead = false;
    public static bool DebugWrite = false;
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

    public Value Read(int addr, DataType type)
    {
      var val = default(Value);
      switch (type)
      {
        case DataType.U8:
          val.Unsigned = this.ReadU8(addr);
          break;
        case DataType.I16:
          val.Signed = this.ReadI16(addr);
          break;
        case DataType.I32:
          val.Signed = this.ReadI32(addr);
          break;
        case DataType.I64:
          val.Signed = this.ReadI64(addr);
          break;
        case DataType.U64:
          val.Unsigned = this.ReadU64(addr);
          break;
        case DataType.F64:
          val.Float = this.ReadF64(addr);
          break;
        case DataType.P24:
          val.Unsigned = this.ReadP24(addr);
          break;
        case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
      if (DebugRead)
        Console.WriteLine($"READ {addr} {type} = {val.As(type)}");
      return val;
    }

    public void Write(int addr, DataType type, Value val)
    {
      switch (type)
      {
        case DataType.U8:
          this.WriteU8(addr, val.Unsigned);
          break;
        case DataType.I16:
          this.WriteI16(addr, val.Signed);
          break;
        case DataType.I32:
          this.WriteI32(addr, val.Signed);
          break;
        case DataType.I64:
          this.WriteI64(addr, val.Signed);
          break;
        case DataType.U64:
          this.WriteU64(addr, val.Unsigned);
          break;
        case DataType.F64:
          this.WriteF64(addr, val.Float);
          break;
        case DataType.P24:
          this.WriteP24(addr, val.Unsigned);
          break;
        case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
      if (DebugWrite)
        Console.WriteLine($"WRITE {addr} {type} = {val.As(type)}");
    }
  }
}