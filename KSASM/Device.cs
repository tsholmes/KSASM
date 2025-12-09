
using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;

namespace KSASM
{
  public interface IDevice : IMemory
  {
    public const ImGuiTreeNodeFlags TREE_FLAGS =
      ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.DrawLinesToNodes;
    public ulong Id { get; }
    public void OnDrawUi();

    public static void DrawTreeLeaf(ImString text)
    {
      ImGui.Indent(ImGui.GetTreeNodeToLabelSpacing());
      ImGui.Text(text);
      ImGui.Unindent(ImGui.GetTreeNodeToLabelSpacing());
    }
  }

  public interface IDeviceField<T>
  {
    public int Length { get; }

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset);
    public void OnDrawUi(ref T parent, Span<byte> deviceBuf, int offset);
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
      string name,
      DeviceFieldBufGetter<V, V2> getValue,
      CompositeBuildFunc<V, V2> build
    ) => Field(build(new(name, getValue)));

    public B ValueComposite<V2>(
      string name,
      DeviceFieldBufGetter<V, V2> getValue,
      DeviceFieldSetter<V, V2> setValue,
      ChildBuilder<ValueCompositeDeviceFieldBuilder<V, V2>> build
    ) => Field(build(new(name, getValue, setValue)));

    public B Leaf<V2>(
      string name,
      DataType type,
      IValueConverter<V2> converter,
      DeviceFieldGetter<V, V2> getter,
      DeviceFieldSetter<V, V2> setter = null
    ) => Field(new LeafDeviceField<V, V2>(name, type, converter, getter, setter));

    public B Parameter<V2>(
      string name,
      DataType type,
      IValueConverter<V2> converter,
      out ParamDeviceField<V, V2> param
    ) => Field(param = new ParamDeviceField<V, V2>(name, type, converter));

    public B Chain(ChildBuilder<B> build) => build(Self);
  }

  public delegate CompositeDeviceFieldBuilder<T, V> CompositeBuildFunc<T, V>(
    CompositeDeviceFieldBuilder<T, V> builder);
  public class CompositeDeviceFieldBuilder<T, V>(string name, DeviceFieldBufGetter<T, V> getValue)
  : DeviceFieldBuilder<CompositeDeviceFieldBuilder<T, V>, T, V>
  {
    protected readonly List<IDeviceField<V>> fields = [];

    public override IDeviceField<T> Build() => new CompositeDeviceField<T, V>(name, getValue, fields);
    protected override void Add(IDeviceField<V> field) => fields.Add(field);
  }

  public class ValueCompositeDeviceFieldBuilder<T, V>(
    string name,
    DeviceFieldBufGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue = null
  ) : DeviceFieldBuilder<ValueCompositeDeviceFieldBuilder<T, V>, T, V>
  {
    private readonly List<IDeviceField<V>> fields = [];

    public override IDeviceField<T> Build() =>
      new ValueCompositeDeviceField<T, V>(name, getValue, setValue, fields);
    protected override void Add(IDeviceField<V> field) => fields.Add(field);
  }

  public class RootDeviceFieldBuilder<T>() : CompositeDeviceFieldBuilder<T, T>(null, (ref t, _) => t)
  {
    public override IDeviceField<T> Build() => new RootDeviceField<T>(fields);
  }

  public delegate V DeviceFieldGetter<T, V>(ref T parent) where V : allows ref struct;
  public delegate V DeviceFieldBufGetter<T, V>(ref T parent, Span<byte> deviceBuffer) where V : allows ref struct;
  public delegate void DeviceFieldSetter<T, V>(ref T parent, V value) where V : allows ref struct;

  public abstract class BaseDeviceField<T, V>(string name) : IDeviceField<T>
  {
    public readonly string Name = name;

    public abstract int Length { get; }
    public abstract void OnDrawUi(ref T parent, Span<byte> deviceBuf, int offset);
    public abstract void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset);
    public abstract void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset);
  }

  public class LeafDeviceField<T, V>(
    string name,
    DataType type,
    IValueConverter<V> converter,
    DeviceFieldGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue = null
  ) : BaseDeviceField<T, V>(name)
  {
    public override int Length => type.SizeBytes();

    public override void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      Encoding.Encode(deviceBuf, type, converter.ToValue(getValue(ref parent)));
      deviceBuf[offset..(offset + readBuf.Length)].CopyTo(readBuf);
    }

    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      Encoding.Encode(deviceBuf, type, converter.ToValue(getValue(ref parent)));
      writeBuf.CopyTo(deviceBuf[offset..]);
      setValue?.Invoke(ref parent, converter.FromValue(Encoding.Decode(deviceBuf, type)));
    }

    public override void OnDrawUi(ref T parent, Span<byte> deviceBuf, int offset)
    {
      var rawVal = getValue(ref parent);
      var val = converter.ToValue(rawVal);
      IDevice.DrawTreeLeaf($"{Name ?? "<no name>"}@{offset}x{Length}: {rawVal} ({type} {val.Typed(type)})");
    }
  }

  public class ParamDeviceField<T, V>(
    string name, DataType type, IValueConverter<V> converter
  ) : BaseDeviceField<T, V>(name)
  {
    public override int Length => type.SizeBytes();

    public override void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      deviceBuf[offset..(offset + readBuf.Length)].CopyTo(readBuf);
    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) =>
      writeBuf.CopyTo(deviceBuf[offset..]);

    public V GetValue(Span<byte> deviceBuf) =>
      converter.FromValue(Encoding.Decode(deviceBuf, type));
    public void SetValue(Span<byte> deviceBuf, V value) =>
      Encoding.Encode(deviceBuf, type, converter.ToValue(value));

    public override void OnDrawUi(ref T parent, Span<byte> deviceBuf, int offset)
    {
      var val = Encoding.Decode(deviceBuf, type);
      var rawVal = converter.FromValue(val);
      IDevice.DrawTreeLeaf($"{Name ?? "<no name>"}@{offset}x{Length}: {rawVal} ({type} {val.Typed(type)})");
    }
  }

  public class CompositeDeviceField<T, V> : BaseDeviceField<T, V>
  {
    public override int Length { get; }

    protected readonly DeviceFieldBufGetter<T, V> getValue;
    private readonly RangeList<DeviceFieldRange> fieldRanges = new();

    public CompositeDeviceField(
      string name,
      DeviceFieldBufGetter<T, V> getValue,
      params List<IDeviceField<V>> children) : base(name)
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

    public override void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
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

    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
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

    public override void OnDrawUi(ref T parent, Span<byte> deviceBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      if (Name == null)
      {
        for (var i = 0; i < fieldRanges.Count; i++)
        {
          var range = fieldRanges[i];
          range.Field.OnDrawUi(ref self, deviceBuf[range.Offset..][..range.Length], offset + range.Offset);
        }
        return;
      }
      if (ImGui.TreeNodeEx($"{Name}###{offset}", IDevice.TREE_FLAGS))
      {
        for (var i = 0; i < fieldRanges.Count; i++)
        {
          var range = fieldRanges[i];
          range.Field.OnDrawUi(ref self, deviceBuf[range.Offset..][..range.Length], offset + range.Offset);
        }
        ImGui.TreePop();
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
    string name,
    DeviceFieldBufGetter<T, V> getValue,
    DeviceFieldSetter<T, V> setValue,
    params List<IDeviceField<V>> children)
  : CompositeDeviceField<T, V>(name, getValue, children)
  {
    public override void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var self = getValue(ref parent, deviceBuf);
      base.WriteSelf(ref self, deviceBuf, writeBuf, offset);
      setValue?.Invoke(ref parent, self);
    }
  }

  public class RootDeviceField<T>(params List<IDeviceField<T>> children)
  : CompositeDeviceField<T, T>(null, (ref v, _) => v, children)
  { }

  public abstract class DeviceDefinition<T, Def> where Def : DeviceDefinition<T, Def>, new()
  {
    public static Def Instance { get; } = new();

    public abstract ulong GetId(T device);

    private IDeviceField<T> _rootField;
    public IDeviceField<T> RootField => _rootField ??= Build(new()).Build();

    public abstract IDeviceFieldBuilder<T> Build(RootDeviceFieldBuilder<T> b);

    public static Device<T> Make(string name, T device) => new(name, Instance.GetId(device), device, Instance.RootField);
  }

  public class Device<T>(string name, ulong id, T instance, IDeviceField<T> root) : IDevice
  {
    public ulong Id { get; } = id;

    private readonly byte[] devBuffer = new byte[root.Length];

    public void Read(Span<byte> buffer, int address) =>
      root.Read(ref instance, devBuffer, buffer, address);

    public void Write(Span<byte> data, int address) =>
      root.Write(ref instance, devBuffer, data, address);

    public void OnDrawUi()
    {
      if (ImGui.TreeNodeEx($"{name} (#{Id})###{Id}", IDevice.TREE_FLAGS))
      {
        root.OnDrawUi(ref instance, devBuffer, 0);
        ImGui.TreePop();
      }
    }
  }

  public class NullDevice : IDevice
  {
    public ulong Id => 0;
    public void Read(Span<byte> buffer, int address) => buffer.Clear();
    public void Write(Span<byte> data, int address) { }
    public void OnDrawUi() => IDevice.DrawTreeLeaf("<null>");
  }
}