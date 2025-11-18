
using System;
using System.Collections.Generic;

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

  public interface IDeviceFieldBuilder<T>
  {
    IDeviceField<T> Build();
  }

  public delegate B2 ChildBuilder<B2>(B2 builder);

  public abstract partial class DeviceFieldBuilder<B, T, V> : IDeviceFieldBuilder<T>
    where B : DeviceFieldBuilder<B, T, V>
  {
    public abstract IDeviceField<T> Build();
    protected abstract void Add(IDeviceField<V> field);

    protected B Self => (B)this;

    public B Field(IDeviceField<V> field) { Add(field); return Self; }
    public B Field(IDeviceFieldBuilder<V> field) => Field(field.Build());

    public B Composite<V2>(
      DeviceFieldBufGetter<V, V2> getValue,
      ChildBuilder<CompositeDeviceFieldBuilder<V, V2>> build
    ) => Field(build(new(getValue)));

    public B ValueComposite<V2>(
      DeviceFieldBufGetter<V, V2> getValue,
      DeviceFieldSetter<V, V2> setValue,
      ChildBuilder<ValueCompositeDeviceFieldBuilder<V, V2>> build
    ) => Field(build(new(getValue, setValue)));

    public B Leaf<V2>(
      DataType type,
      IValueConverter<V2> converter,
      DeviceFieldGetter<V, V2> getter,
      DeviceFieldSetter<V, V2> setter = null
    ) => Field(new LeafDeviceField<V, V2>(type, converter, getter, setter));

    public B Parameter<V2>(
      DataType type,
      IValueConverter<V2> converter,
      out ParamDeviceField<V, V2> param
    ) => Field(param = new ParamDeviceField<V, V2>(type, converter));

    public B Chain(ChildBuilder<B> build) => build(Self);
  }

  public class CompositeDeviceFieldBuilder<T, V>(DeviceFieldBufGetter<T, V> getValue)
  : DeviceFieldBuilder<CompositeDeviceFieldBuilder<T, V>, T, V>
  {
    protected readonly List<IDeviceField<V>> fields = [];

    public override IDeviceField<T> Build() => new CompositeDeviceField<T, V>(getValue, fields);
    protected override void Add(IDeviceField<V> field) => fields.Add(field);
  }

  public class ValueCompositeDeviceFieldBuilder<T, V>(
    DeviceFieldBufGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue = null
  ) : DeviceFieldBuilder<ValueCompositeDeviceFieldBuilder<T, V>, T, V>
  {
    private readonly List<IDeviceField<V>> fields = [];

    public override IDeviceField<T> Build() => new ValueCompositeDeviceField<T, V>(getValue, setValue, fields);
    protected override void Add(IDeviceField<V> field) => fields.Add(field);
  }

  public class RootDeviceFieldBuilder<T>() : CompositeDeviceFieldBuilder<T, T>((ref t, _) => t)
  {
    public override IDeviceField<T> Build() => new RootDeviceField<T>(fields);
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
      params List<IDeviceField<V>> children)
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
    params List<IDeviceField<V>> children)
  : CompositeDeviceField<T, V>(getValue, children)
  {
    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      base.WriteSelf(ref self, deviceBuf, writeBuf, offset);
      setValue?.Invoke(ref parent, self);
    }
  }

  public class RootDeviceField<T>(params List<IDeviceField<T>> children)
  : CompositeDeviceField<T, T>((ref v, _) => v, children)
  { }

  public abstract class DeviceDefinition<T, Def> where Def : DeviceDefinition<T, Def>, new()
  {
    public static Def Instance { get; } = new();

    public abstract ulong GetId(T device);

    private IDeviceField<T> _rootField;
    public IDeviceField<T> RootField => _rootField ??= Build(new()).Build();

    public abstract IDeviceFieldBuilder<T> Build(RootDeviceFieldBuilder<T> b);

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