
using System;

namespace KSASM
{
  public interface IDevice : IMemory
  {
    public ulong Id { get; }
  }

  public interface IDeviceField<T>
  {
    public int Offset { get; }
    public int Length { get; }

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset);
  }

  public struct DeviceFieldRange<T> : IRange<DeviceFieldRange<T>>
  {
    public int Offset { get; set; }
    public int Length { get; set; }
    public IDeviceField<T> Field;

    public DeviceFieldRange<T> Slice(int offset, int length) => this with
    {
      Offset = offset,
      Length = length,
    };

    public bool TryMerge(DeviceFieldRange<T> next, out DeviceFieldRange<T> merged)
    {
      merged = default;
      return false;
    }
  }

  public delegate V DeviceFieldGetter<T, V>(ref T parent);
  public delegate V DeviceFieldBufGetter<T, V>(ref T parent, Span<byte> deviceBuffer);
  public delegate void DeviceFieldSetter<T, V>(ref T parent, V value);

  public class LeafDeviceField<T, V>(
    DataType type,
    int offset,
    IValueConverter<V> converter,
    DeviceFieldGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue = null
  ) : IDeviceField<T>
  {
    public int Offset => offset;
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

  public class ParamDeviceField<T, V>(DataType type, int offset, IValueConverter<V> converter) : IDeviceField<T>
  {
    public int Offset => offset;
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
    public int Offset { get; }
    public int Length { get; }

    protected readonly DeviceFieldBufGetter<T, V> getValue;
    private readonly RangeList<DeviceFieldRange<V>> fieldRanges = new();

    public CompositeDeviceField(
      int offset,
      DeviceFieldBufGetter<T, V> getValue,
      params IDeviceField<V>[] children)
    {
      this.getValue = getValue;
      var len = 0;
      foreach (var child in children)
      {
        fieldRanges.AddRange(new()
        {
          Offset = child.Offset,
          Length = child.Length,
          Field = child,
        });
        var end = child.Offset + child.Length;
        if (end > len)
          len = end;
      }
      Offset = offset;
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
        var fbuf = deviceBuf[field.Offset..field.End()];
        field.Read(ref self, fbuf, brange.Span, frange.Offset - field.Offset);
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
        var fbuf = deviceBuf[field.Offset..(field.Offset + field.Length)];
        field.Write(ref self, fbuf, brange.Span, frange.Offset - field.Offset);
      }
    }
  }

  public class ValueCompositeDeviceField<T, V>(
    int offset,
    DeviceFieldBufGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue,
    params IDeviceField<V>[] children)
  : CompositeDeviceField<T, V>(offset, getValue, children)
  {
    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      base.WriteSelf(ref self, deviceBuf, writeBuf, offset);
      setValue(ref parent, self);
    }
  }

  public class RootDeviceField<T>(params IDeviceField<T>[] children)
  : CompositeDeviceField<T, T>(0, (ref v, _) => v, children)
  { }

  public abstract class DeviceDefinition<T, Def> where Def : DeviceDefinition<T, Def>, new()
  {
    public static Def Instance { get; } = new();

    public abstract ulong GetId(T device);
    public abstract RootDeviceField<T> RootField { get; }

    public static Device<T> Make(T device) => new(Instance.GetId(device), device, Instance.RootField);
  }

  public class Device<T> : IDevice
  {
    public ulong Id { get; }
    private T instance;
    public readonly IDeviceField<T> Root;

    private readonly byte[] devBuffer;

    public Device(ulong id, T instance, IDeviceField<T> root)
    {
      this.Id = id;
      this.instance = instance;
      this.Root = root;
      this.devBuffer = new byte[root.Length];

      if (root.Offset != 0)
        throw new InvalidOperationException($"{root}");
    }

    public void Read(Span<byte> buffer, int address)
    {
      Root.Read(ref instance, devBuffer, buffer, address);
    }

    public void Write(Span<byte> data, int address)
    {
      Root.Write(ref instance, devBuffer, data, address);
    }
  }

  public class NullDevice : IDevice
  {
    public ulong Id => 0;
    public void Read(Span<byte> buffer, int address) => buffer.Clear();
    public void Write(Span<byte> data, int address) { }
  }
}