
using System;

namespace KSACPU
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
      public static ulong Decode(byte[] data, int offset) => Read1(data, offset);
      public static void Encode(ulong val, byte[] data, int offset) => data[offset] = (byte)val;
    }

    public static class I16
    {
      public static long Decode(byte[] data, int offset) => (short)Read2(data, offset);

      public static void Encode(long val, byte[] data, int offset)
      {
        data[offset + 0] = (byte)(val >> 0);
        data[offset + 1] = (byte)(val >> 8);
      }
    }

    public static class I32
    {
      public static long Decode(byte[] data, int offset) => (int)Read4(data, offset);

      public static void Encode(long val, byte[] data, int offset)
      {
        data[offset + 0] = (byte)(val >> 0);
        data[offset + 1] = (byte)(val >> 8);
        data[offset + 2] = (byte)(val >> 16);
        data[offset + 3] = (byte)(val >> 24);
      }
    }

    public static class I64
    {
      public static long Decode(byte[] data, int offset) => (long)Read8(data, offset);

      public static void Encode(long val, byte[] data, int offset)
      {
        data[offset + 0] = (byte)(val >> 0);
        data[offset + 1] = (byte)(val >> 8);
        data[offset + 2] = (byte)(val >> 16);
        data[offset + 3] = (byte)(val >> 24);
        data[offset + 4] = (byte)(val >> 32);
        data[offset + 5] = (byte)(val >> 40);
        data[offset + 6] = (byte)(val >> 48);
        data[offset + 7] = (byte)(val >> 56);
      }
    }

    public static class U64
    {
      public static ulong Decode(byte[] data, int offset) => Read8(data, offset);

      public static void Encode(ulong val, byte[] data, int offset)
      {
        data[offset + 0] = (byte)(val >> 0);
        data[offset + 1] = (byte)(val >> 8);
        data[offset + 2] = (byte)(val >> 16);
        data[offset + 3] = (byte)(val >> 24);
        data[offset + 4] = (byte)(val >> 32);
        data[offset + 5] = (byte)(val >> 40);
        data[offset + 6] = (byte)(val >> 48);
        data[offset + 7] = (byte)(val >> 56);
      }
    }

    public static class F64
    {
      public static double Decode(byte[] data, int offset) =>
        BitConverter.UInt64BitsToDouble(Read8(data, offset));

      public static void Encode(double value, byte[] data, int offset) =>
        U64.Encode(BitConverter.DoubleToUInt64Bits(value), data, offset);
    }

    public static class P24
    {
      public static ulong Decode(byte[] data, int offset) => Read3(data, offset);

      public static void Encode(ulong val, byte[] data, int offset)
      {
        data[offset + 0] = (byte)(val >> 0);
        data[offset + 1] = (byte)(val >> 8);
        data[offset + 2] = (byte)(val >> 16);
      }
    }

    // TODO: use complex type for this
    public static class C128
    {
      public static double Decode(byte[] data, int offset) =>
        throw new NotImplementedException();

      public static void Encode(double value, byte[] data, int offset) =>
        throw new NotImplementedException();
    }

    private static ulong Read1(byte[] data, int offset) => data[offset];

    private static ulong Read2(byte[] data, int offset) =>
      ((ulong)data[offset + 0] << 0) | ((ulong)data[offset + 1] << 8);

    private static ulong Read3(byte[] data, int offset) =>
        ((ulong)data[offset + 0] << 0) | ((ulong)data[offset + 1] << 8) | ((ulong)data[offset + 2] << 16);

    private static ulong Read4(byte[] data, int offset) =>
        ((ulong)data[offset + 0] << 0) | ((ulong)data[offset + 1] << 8) |
        ((ulong)data[offset + 2] << 16) | ((ulong)data[offset + 3] << 24);

    private static ulong Read8(byte[] data, int offset) =>
        ((ulong)data[offset + 0] << 0) | ((ulong)data[offset + 1] << 8) |
        ((ulong)data[offset + 2] << 16) | ((ulong)data[offset + 3] << 24) |
        ((ulong)data[offset + 4] << 32) | ((ulong)data[offset + 5] << 40) |
        ((ulong)data[offset + 6] << 48) | ((ulong)data[offset + 7] << 56);
  }
}