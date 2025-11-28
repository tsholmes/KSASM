
using System;
using Brutal.Numerics;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Bool(DeviceFieldGetter<V, bool> getter, DeviceFieldSetter<V, bool> setter = null) =>
      Leaf(DataType.U8, BoolValueConverter.Instance, getter, setter);

    public B Byte(DeviceFieldGetter<V, byte> getter, DeviceFieldSetter<V, byte> setter = null) =>
      Leaf(DataType.U8, ByteValueConverter.Instance, getter, setter);

    public B Uint(DeviceFieldGetter<V, uint> getter, DeviceFieldSetter<V, uint> setter = null) =>
      Leaf(DataType.U64, UintValueConverter.Instance, getter, setter);

    public B UintParameter(out ParamDeviceField<V, uint> field) =>
      Parameter(DataType.U64, UintValueConverter.Instance, out field);

    public B Ulong(DeviceFieldGetter<V, ulong> getter, DeviceFieldSetter<V, ulong> setter = null) =>
      Leaf(DataType.U64, UlongValueConverter.Instance, getter, setter);

    public B UlongParameter(out ParamDeviceField<V, ulong> field, DeviceFieldSetter<T, V> onSet = null) =>
      Parameter(DataType.U64, UlongValueConverter.Instance, out field);

    public B Double(DeviceFieldGetter<V, double> getter, DeviceFieldSetter<V, double> setter = null) =>
      Leaf(DataType.F64, DoubleValueConverter.Instance, getter, setter);

    public B Double3(DeviceFieldBufGetter<V, double3> getter, DeviceFieldSetter<V, double3> setter = null) =>
      ValueComposite(getter, setter, b => b
        .Double((ref d3) => d3[0], (ref d3, v) => d3[0] = v)
        .Double((ref d3) => d3[1], (ref d3, v) => d3[1] = v)
        .Double((ref d3) => d3[2], (ref d3, v) => d3[2] = v)
      );

    public B String(int maxLen, DeviceFieldBufGetter<V, string> getter) => Composite<string>(getter, b => b
      .Byte((ref s) => (byte)s.Length)
      .Field(new StringDeviceField<string>(maxLen, (ref s) => s))
    );
  }

  public class StringDeviceField<T>(int maxLen, DeviceFieldGetter<T, string> getter) : IDeviceField<T>
  {
    private static System.Text.Encoding Encoding = System.Text.Encoding.ASCII;
    public int Length => maxLen;

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      var str = getter(ref parent);
      var trimmed = Trim(str);
      var bytes = Encoding.GetBytes(trimmed, deviceBuf);
      if (bytes < maxLen)
        deviceBuf[bytes..].Clear();
      deviceBuf.Slice(offset, readBuf.Length).CopyTo(readBuf);
    }

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      // read only
    }

    private ReadOnlySpan<char> Trim(ReadOnlySpan<char> str)
    {
      var bcount = 0;
      var ccount = 0;
      var checkLen = str.Length;
      while (ccount < str.Length)
      {
        if (ccount + checkLen > str.Length)
          checkLen = str.Length - ccount;
        var b = Encoding.GetByteCount(str[ccount..(ccount + checkLen)]);
        if (bcount + b > maxLen)
        {
          checkLen >>= 1;
          if (checkLen == 0)
            break;
        }
        else
        {
          bcount += b;
          ccount += checkLen;
        }
      }
      return str[..ccount];
    }
  }

  public class BoolValueConverter : UnsignedValueConverter<BoolValueConverter, bool>
  {
    public override bool FromUnsigned(ulong val) => val != 0;
    public override ulong ToUnsigned(bool val) => val ? 1u : 0u;
  }

  public class ByteValueConverter : UnsignedValueConverter<ByteValueConverter, byte>
  {
    public override byte FromUnsigned(ulong val) => (byte)val;
    public override ulong ToUnsigned(byte val) => val;
  }

  public class UintValueConverter : UnsignedValueConverter<UintValueConverter, uint>
  {
    public override uint FromUnsigned(ulong val) => (uint)val;
    public override ulong ToUnsigned(uint val) => val;
  }

  public class UlongValueConverter : UnsignedValueConverter<UlongValueConverter, ulong>
  {
    public override ulong FromUnsigned(ulong val) => val;
    public override ulong ToUnsigned(ulong val) => val;
  }

  public class DoubleValueConverter : FloatValueConverter<DoubleValueConverter, double>
  {
    public override double FromFloat(double val) => val;
    public override double ToFloat(double val) => val;
  }
}