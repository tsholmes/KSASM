
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

  public abstract class LeafDeviceField<T, V>(DataType type, int offset, IValueConverter<V> converter)
  : IDeviceField<T>
  {
    public int Offset => offset;
    public int Length => type.SizeBytes();

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      Encoding.Encode(deviceBuf, type, converter.ToValue(GetValue(ref parent)));
      deviceBuf[offset..(offset + readBuf.Length)].CopyTo(readBuf);
    }

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      Encoding.Encode(deviceBuf, type, converter.ToValue(GetValue(ref parent)));
      writeBuf.CopyTo(deviceBuf[offset..]);
      SetValue(ref parent, converter.FromValue(Encoding.Decode(deviceBuf, type)));
    }

    protected abstract V GetValue(ref T parent);
    protected abstract void SetValue(ref T parent, V value);
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

  public abstract class CompositeDeviceField<T, V> : IDeviceField<T>
  {
    public int Offset { get; }
    public int Length { get; }

    private readonly RangeList<DeviceFieldRange<V>> fieldRanges = new();

    public CompositeDeviceField(int offset, params IDeviceField<V>[] children)
    {
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
      var self = GetValue(ref parent, deviceBuf);
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
      var self = GetValue(ref parent, deviceBuf);
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

    protected abstract V GetValue(ref T parent, Span<byte> deviceBuf);
  }

  public abstract class ValueCompositeDeviceField<T, V>(int offset, params IDeviceField<V>[] children)
  : CompositeDeviceField<T, V>(offset, children)
  {
    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = GetValue(ref parent, deviceBuf);
      base.WriteSelf(ref self, deviceBuf, writeBuf, offset);
      SetValue(ref parent, self);
    }

    protected abstract void SetValue(ref T parent, V self);
  }

  public class RootDeviceField<T>(params IDeviceField<T>[] children) : CompositeDeviceField<T, T>(0, children)
  {
    protected override T GetValue(ref T parent, Span<byte> deviceBuf) => parent;
  }

  public abstract class DeviceDefinition<T, Def> where Def : DeviceDefinition<T, Def>, new()
  {
    public static Def Instance { get; } = new();

    public abstract ulong GetId(T device);
    public abstract IDeviceField<T> RootField { get; }

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