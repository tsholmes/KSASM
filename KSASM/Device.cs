
using System;
using Brutal.Numerics;

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
    public int End => Offset + Length;

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

  public abstract class LeafDeviceField<T, V> : IDeviceField<T>
  {
    public int Offset { get; }
    public readonly DataType Type;
    public readonly IValueConverter<V> Converter;

    public LeafDeviceField(DataType type, int offset, IValueConverter<V> converter)
    {
      this.Offset = offset;
      this.Type = type;
      this.Converter = converter;
    }

    public int Length => Type.SizeBytes();

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      Encoding.Encode(deviceBuf, Type, Converter.ToValue(GetValue(ref parent)));
      deviceBuf[offset..(offset + readBuf.Length)].CopyTo(readBuf);
    }

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      Encoding.Encode(deviceBuf, Type, Converter.ToValue(GetValue(ref parent)));
      writeBuf.CopyTo(deviceBuf[offset..]);
      SetValue(ref parent, Converter.FromValue(Encoding.Decode(deviceBuf, Type)));
    }

    protected abstract V GetValue(ref T parent);
    protected abstract void SetValue(ref T parent, V value);
  }

  public class ParamDeviceField<T, V> : LeafDeviceField<T, V>
  {
    private V value = default;

    public ParamDeviceField(
      DataType type,
      int offset,
      IValueConverter<V> converter
    ) : base(type, offset, converter) { }

    protected override V GetValue(ref T parent) => value;
    protected override void SetValue(ref T parent, V value) => this.value = value;
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
      var self = GetValue(ref parent);
      var iter = fieldRanges.Overlap(new SpanRange<byte>
      {
        Span = readBuf,
        Offset = offset,
      });

      while (iter.Next(out var frange, out var brange))
      {
        var field = frange.Field;
        var fbuf = deviceBuf[field.Offset..field.End];
        field.Read(ref self, fbuf, brange.Span, frange.Offset - field.Offset);
      }
    }

    public virtual void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = GetValue(ref parent);
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

    protected abstract V GetValue(ref T parent);
  }

  public abstract class ValueCompositeDeviceField<T, V> : CompositeDeviceField<T, V>
  {
    protected ValueCompositeDeviceField(
      int offset,
      params IDeviceField<V>[] children
    ) : base(offset, children) { }

    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = GetValue(ref parent);
      base.WriteSelf(ref self, deviceBuf, writeBuf, offset);
      SetValue(ref parent, self);
    }

    protected abstract void SetValue(ref T parent, V self);
  }

  public class ReadOnlyDeviceField<T> : IDeviceField<T>
  {
    public readonly IDeviceField<T> Field;

    public ReadOnlyDeviceField(IDeviceField<T> field) => this.Field = field;


    public int Offset => Field.Offset;
    public int Length => Field.Length;
    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      Field.Read(ref parent, deviceBuf, readBuf, offset);

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) { }
  }

  public class RootDeviceField<T> : CompositeDeviceField<T, T>
  {
    public RootDeviceField(params IDeviceField<T>[] children) : base(0, children) { }

    protected override T GetValue(ref T parent) => parent;
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

  public abstract class Double3DeviceField<T> : ValueCompositeDeviceField<T, double3>
  {
    protected Double3DeviceField(int offset) : base(offset, X, Y, Z) { }

    public static readonly Element X = new(0);
    public static readonly Element Y = new(1);
    public static readonly Element Z = new(2);

    public class Element : LeafDeviceField<double3, double>
    {
      public readonly int Index;
      public Element(int index) : base(DataType.F64, index * DataType.F64.SizeBytes(), DoubleValueConverter.Instance)
      {
        this.Index = index;
      }

      protected override double GetValue(ref double3 parent) => parent[Index];
      protected override void SetValue(ref double3 parent, double value) => parent[Index] = value;
    }
  }

  public class NullDevice : IDevice
  {
    public ulong Id => 0;
    public void Read(Span<byte> buffer, int address) => buffer.Clear();
    public void Write(Span<byte> data, int address) { }
  }
}