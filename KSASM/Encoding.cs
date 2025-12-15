
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KSASM
{
  public static class Encoding
  {
    public static int SizeBytes(this DataType type) => type switch
    {
      DataType.U8 => 1,
      DataType.I16 => 2,
      DataType.I32 => 4,
      DataType.I64 => 8,
      DataType.U64 => 8,
      DataType.F64 => 8,
      DataType.P24 => 3,
      // DataType.C128 => 16,
      _ => throw new InvalidOperationException($"Invalid DataType {type}"),
    };

    public static ValueMode VMode(this DataType type) => type switch
    {
      DataType.U8 => ValueMode.Unsigned,
      DataType.I16 => ValueMode.Signed,
      DataType.I32 => ValueMode.Signed,
      DataType.I64 => ValueMode.Signed,
      DataType.U64 => ValueMode.Unsigned,
      DataType.F64 => ValueMode.Float,
      DataType.P24 => ValueMode.Unsigned,
      // DataType.C128 => ValueMode.Complex,
      _ => throw new InvalidOperationException($"Invalid DataType {type}"),
    };

    [StructLayout(LayoutKind.Explicit)]
    private ref struct EVal
    {
      [FieldOffset(0)]
      public Byte8 Bytes;
      [FieldOffset(0)]
      public byte U8;
      [FieldOffset(0)]
      public short I16;
      [FieldOffset(0)]
      public int I32;
      [FieldOffset(0)]
      public long I64;
      [FieldOffset(0)]
      public ulong U64;
      [FieldOffset(0)]
      public double F64;
      [FieldOffset(0)]
      public uint P24;
      // TODO: C128
    }
    private static Span<byte> BytesOf(this ref EVal val, DataType type) => val.Bytes[..type.SizeBytes()];

    [InlineArray(8)]
    public struct Byte8 { private byte element; }

    public static Value Decode(Span<byte> data, DataType type)
    {
      var eval = default(EVal);
      data[..type.SizeBytes()].CopyTo(eval.Bytes);

      var val = default(Value);
      switch (type)
      {
        case DataType.U8: val.Unsigned = eval.U8; break;
        case DataType.I16: val.Signed = eval.I16; break;
        case DataType.I32: val.Signed = eval.I32; break;
        case DataType.I64: val.Signed = eval.I64; break;
        case DataType.U64: val.Unsigned = eval.U64; break;
        case DataType.F64: val.Float = eval.F64; break;
        case DataType.P24: val.Unsigned = eval.P24; break;
        // case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
      return val;
    }

    public static void Encode(Span<byte> buffer, DataType type, Value val)
    {
      var eval = default(EVal);
      switch (type)
      {
        case DataType.U8: eval.U8 = (byte)val.Unsigned; break;
        case DataType.I16: eval.I16 = (short)val.Signed; break;
        case DataType.I32: eval.I32 = (int)val.Signed; break;
        case DataType.I64: eval.I64 = val.Signed; break;
        case DataType.U64: eval.U64 = val.Unsigned; break;
        case DataType.F64: eval.F64 = val.Float; break;
        case DataType.P24: eval.P24 = (uint)val.Unsigned; break;
        // case DataType.C128:
        default:
          throw new InvalidOperationException($"Invalid DataType {type}");
      }
      eval.BytesOf(type).CopyTo(buffer);
    }
  }

  public struct ShiftMask
  {
    public int Shift;
    public int Bits;

    public ulong Decode(ulong encoded) => (encoded >> Shift) & ((1u << Bits) - 1);
    public ulong Encode(ulong decoded) => (decoded & ((1u << Bits) - 1)) << Shift;

    public int DecodeSignExtend(ulong encoded)
    {
      var decoded = (int)Decode(encoded);
      if ((decoded & (1 << (Bits - 1))) != 0)
        decoded |= ~((1 << Bits) - 1);
      return decoded;
    }
  }

}