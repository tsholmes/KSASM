
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Bool(string name, DeviceFieldGetter<V, bool> getter, DeviceFieldSetter<V, bool> setter = null) =>
      Leaf(name, DataType.U8, BoolValueConverter.Instance, getter, setter);

    public B Byte(string name, DeviceFieldGetter<V, byte> getter, DeviceFieldSetter<V, byte> setter = null) =>
      Leaf(name, DataType.U8, ByteValueConverter.Instance, getter, setter);

    public B Uint(string name, DeviceFieldGetter<V, uint> getter, DeviceFieldSetter<V, uint> setter = null) =>
      Leaf(name, DataType.U64, UintValueConverter.Instance, getter, setter);

    public B UintParameter(string name, out ParamDeviceField<V, uint> field) =>
      Parameter(name, DataType.U64, UintValueConverter.Instance, out field);

    public B Ulong(string name, DeviceFieldGetter<V, ulong> getter, DeviceFieldSetter<V, ulong> setter = null) =>
      Leaf(name, DataType.U64, UlongValueConverter.Instance, getter, setter);

    public B UlongParameter(string name, out ParamDeviceField<V, ulong> field, DeviceFieldSetter<T, V> onSet = null) =>
      Parameter(name, DataType.U64, UlongValueConverter.Instance, out field);

    public B Double(string name, DeviceFieldGetter<V, double> getter, DeviceFieldSetter<V, double> setter = null) =>
      Leaf(name, DataType.F64, DoubleValueConverter.Instance, getter, setter);

    public B Double3(string name, DeviceFieldBufGetter<V, double3> getter, DeviceFieldSetter<V, double3> setter = null) =>
      ValueComposite(name, getter, setter, b => b
        .Double("x", (ref d3) => d3[0], (ref d3, v) => d3[0] = v)
        .Double("y", (ref d3) => d3[1], (ref d3, v) => d3[1] = v)
        .Double("z", (ref d3) => d3[2], (ref d3, v) => d3[2] = v)
      );

    public B DoubleQuat(
        string name,
        DeviceFieldBufGetter<V, doubleQuat> getter, DeviceFieldSetter<V, doubleQuat> setter = null) =>
      ValueComposite(name, getter, setter, b => b
        .Double("x", (ref dq) => dq[0], (ref dq, v) => dq[0] = v)
        .Double("y", (ref dq) => dq[1], (ref dq, v) => dq[1] = v)
        .Double("z", (ref dq) => dq[2], (ref dq, v) => dq[2] = v)
        .Double("w", (ref dq) => dq[3], (ref dq, v) => dq[3] = v)
      );

    public B String(string name, int maxLen, DeviceFieldBufGetter<V, string> getter) =>
      Composite<string>(null, getter, b => b
        .Byte($"{name}.len", (ref s) => (byte)s.Length)
        .Field(new StringDeviceField<string>(name, maxLen, (ref s) => s))
      );
  }

  public class StringDeviceField<T>(
    string name, int maxLen, DeviceFieldGetter<T, string> getter
  ) : BaseDeviceField<T, string>(name)
  {
    private static readonly System.Text.Encoding Encoding = System.Text.Encoding.ASCII;
    public override int Length => maxLen;

    public override void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      var str = getter(ref parent);
      var trimmed = Trim(str);
      var bytes = Encoding.GetBytes(trimmed, deviceBuf);
      if (bytes < maxLen)
        deviceBuf[bytes..].Clear();
      deviceBuf.Slice(offset, readBuf.Length).CopyTo(readBuf);
    }

    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      // read only
    }

    public override void OnDrawUi(ref T parent, Span<byte> deviceBuf, int offset)
    {
      var val = Trim(getter(ref parent));
      IDevice.DrawTreeLeaf($"{Name}@{offset}x{Length}: \"{val}\"");
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