
using System;

namespace KSASM
{
  public interface IDevice : IMemory
  {
    public ulong Id { get; }
  }

  public interface IDeviceField<T>
  {
    public int Length { get; }

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset);
  }

  public delegate V DeviceFieldGetter<T, V>(ref T parent);
  public delegate V DeviceFieldBufGetter<T, V>(ref T parent, Span<byte> deviceBuffer);
  public delegate void DeviceFieldSetter<T, V>(ref T parent, V value);

  public class LeafDeviceField<T, V>(
    DataType type,
    IValueConverter<V> converter,
    DeviceFieldGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue = null
  ) : IDeviceField<T>
  {
    public int Length => type.SizeBytes();

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      Encoding.Encode(deviceBuf, type, converter.ToValue(getValue(ref parent)));
      deviceBuf[offset..(offset + readBuf.Length)].CopyTo(readBuf);
    }

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      Encoding.Encode(deviceBuf, type, converter.ToValue(getValue(ref parent)));
      writeBuf.CopyTo(deviceBuf[offset..]);
      setValue?.Invoke(ref parent, converter.FromValue(Encoding.Decode(deviceBuf, type)));
    }
  }

  public class ParamDeviceField<T, V>(DataType type, IValueConverter<V> converter) : IDeviceField<T>
  {
    public int Length => type.SizeBytes();

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      deviceBuf[offset..(offset + readBuf.Length)].CopyTo(readBuf);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) =>
      writeBuf.CopyTo(deviceBuf[offset..]);

    public V GetValue(Span<byte> deviceBuf) =>
      converter.FromValue(Encoding.Decode(deviceBuf, type));
    public void SetValue(Span<byte> deviceBuf, V value) =>
      Encoding.Encode(deviceBuf, type, converter.ToValue(value));
  }

  public class CompositeDeviceField<T, V> : IDeviceField<T>
  {
    public int Length { get; }

    protected readonly DeviceFieldBufGetter<T, V> getValue;
    private readonly RangeList<DeviceFieldRange> fieldRanges = new();

    public CompositeDeviceField(
      DeviceFieldBufGetter<T, V> getValue,
      params IDeviceField<V>[] children)
    {
      this.getValue = getValue;
      var len = 0;
      foreach (var child in children)
      {
        fieldRanges.AddRange(new()
        {
          StartOffset = len,
          Offset = len,
          Length = child.Length,
          Field = child,
        });
        len += child.Length;
      }
      Length = len;
    }

    public virtual void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      var iter = fieldRanges.Overlap(new SpanRange<byte>
      {
        Span = readBuf,
        Offset = offset,
      });

      while (iter.Next(out var frange, out var brange))
      {
        var field = frange.Field;
        var fbuf = deviceBuf[frange.StartOffset..][..field.Length];
        field.Read(ref self, fbuf, brange.Span, frange.Offset - frange.StartOffset);
      }
    }

    public virtual void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      WriteSelf(ref self, deviceBuf, writeBuf, offset);
    }

    protected void WriteSelf(ref V self, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var iter = fieldRanges.Overlap(new SpanRange<byte>
      {
        Span = writeBuf,
        Offset = offset,
      });

      while (iter.Next(out var frange, out var brange))
      {
        var field = frange.Field;
        var fbuf = deviceBuf[frange.StartOffset..][..field.Length];
        field.Write(ref self, fbuf, brange.Span, frange.Offset - frange.StartOffset);
      }
    }

    public struct DeviceFieldRange : IRange<DeviceFieldRange>
    {
      public int StartOffset;
      public int Offset { get; set; }
      public int Length { get; set; }
      public IDeviceField<V> Field;

      public DeviceFieldRange Slice(int offset, int length) => this with
      {
        Offset = offset,
        Length = length,
      };

      public bool TryMerge(DeviceFieldRange next, out DeviceFieldRange merged)
      {
        merged = default;
        return false;
      }
    }
  }

  public class ValueCompositeDeviceField<T, V>(
    DeviceFieldBufGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue,
    params IDeviceField<V>[] children)
  : CompositeDeviceField<T, V>(getValue, children)
  {
    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      base.WriteSelf(ref self, deviceBuf, writeBuf, offset);
      setValue(ref parent, self);
    }
  }

  public class RootDeviceField<T>(params IDeviceField<T>[] children)
  : CompositeDeviceField<T, T>((ref v, _) => v, children)
  { }

  public abstract class DeviceDefinition<T, Def> where Def : DeviceDefinition<T, Def>, new()
  {
    public static Def Instance { get; } = new();

    public abstract ulong GetId(T device);
    public abstract RootDeviceField<T> RootField { get; }

    public static Device<T> Make(T device) => new(Instance.GetId(device), device, Instance.RootField);
  }

  public class Device<T>(ulong id, T instance, IDeviceField<T> root) : IDevice
  {
    public ulong Id { get; } = id;

    private readonly byte[] devBuffer = new byte[root.Length];

    public void Read(Span<byte> buffer, int address) =>
      root.Read(ref instance, devBuffer, buffer, address);

    public void Write(Span<byte> data, int address) =>
      root.Write(ref instance, devBuffer, data, address);
  }

  public class NullDevice : IDevice
  {
    public ulong Id => 0;
    public void Read(Span<byte> buffer, int address) => buffer.Clear();
    public void Write(Span<byte> data, int address) { }
  }
}