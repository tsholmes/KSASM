
using System;

namespace KSASM
{
  public static class Encoding
  {
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

    public static int SizeBytes(this DataType type) => type switch
    {
      DataType.U8 => 1,
      DataType.I16 => 2,
      DataType.I32 => 4,
      DataType.I64 => 8,
      DataType.U64 => 8,
      DataType.F64 => 8,
      DataType.P24 => 3,
      DataType.C128 => 16,
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
      DataType.C128 => ValueMode.Complex,
      _ => throw new InvalidOperationException($"Invalid DataType {type}"),
    };

    public static class U8
    {
      public static ulong Decode(Span<byte> data) => Read1(data);
      public static void Encode(ulong val, Span<byte> data) => data[0] = (byte)val;
    }

    public static class I16
    {
      public static long Decode(Span<byte> data) => (short)Read2(data);

      public static void Encode(long val, Span<byte> data)
      {
        data[0] = (byte)(val >> 0);
        data[1] = (byte)(val >> 8);
      }
    }

    public static class I32
    {
      public static long Decode(Span<byte> data) => (int)Read4(data);

      public static void Encode(long val, Span<byte> data)
      {
        data[0] = (byte)(val >> 0);
        data[1] = (byte)(val >> 8);
        data[2] = (byte)(val >> 16);
        data[3] = (byte)(val >> 24);
      }
    }

    public static class I64
    {
      public static long Decode(Span<byte> data) => (long)Read8(data);

      public static void Encode(long val, Span<byte> data)
      {
        data[0] = (byte)(val >> 0);
        data[1] = (byte)(val >> 8);
        data[2] = (byte)(val >> 16);
        data[3] = (byte)(val >> 24);
        data[4] = (byte)(val >> 32);
        data[5] = (byte)(val >> 40);
        data[6] = (byte)(val >> 48);
        data[7] = (byte)(val >> 56);
      }
    }

    public static class U64
    {
      public static ulong Decode(Span<byte> data) => Read8(data);

      public static void Encode(ulong val, Span<byte> data)
      {
        data[0] = (byte)(val >> 0);
        data[1] = (byte)(val >> 8);
        data[2] = (byte)(val >> 16);
        data[3] = (byte)(val >> 24);
        data[4] = (byte)(val >> 32);
        data[5] = (byte)(val >> 40);
        data[6] = (byte)(val >> 48);
        data[7] = (byte)(val >> 56);
      }
    }

    public static class F64
    {
      public static double Decode(Span<byte> data) =>
        BitConverter.UInt64BitsToDouble(Read8(data));

      public static void Encode(double value, Span<byte> data) =>
        U64.Encode(BitConverter.DoubleToUInt64Bits(value), data);
    }

    public static class P24
    {
      public static ulong Decode(Span<byte> data) => Read3(data);

      public static void Encode(ulong val, Span<byte> data)
      {
        data[0] = (byte)(val >> 0);
        data[1] = (byte)(val >> 8);
        data[2] = (byte)(val >> 16);
      }
    }

    // TODO: use complex type for this
    public static class C128
    {
      public static double Decode(Span<byte> data) =>
        throw new NotImplementedException();

      public static void Encode(double value, Span<byte> data) =>
        throw new NotImplementedException();
    }

    private static ulong Read1(Span<byte> data) => data[0];

    private static ulong Read2(Span<byte> data) =>
      ((ulong)data[0] << 0) | ((ulong)data[1] << 8);

    private static ulong Read3(Span<byte> data) =>
        ((ulong)data[0] << 0) | ((ulong)data[1] << 8) | ((ulong)data[2] << 16);

    private static ulong Read4(Span<byte> data) =>
        ((ulong)data[0] << 0) | ((ulong)data[1] << 8) |
        ((ulong)data[2] << 16) | ((ulong)data[3] << 24);

    private static ulong Read8(Span<byte> data) =>
        ((ulong)data[0] << 0) | ((ulong)data[1] << 8) |
        ((ulong)data[2] << 16) | ((ulong)data[3] << 24) |
        ((ulong)data[4] << 32) | ((ulong)data[5] << 40) |
        ((ulong)data[6] << 48) | ((ulong)data[7] << 56);
  }
}