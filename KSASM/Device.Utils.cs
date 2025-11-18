
using System;
using System.Collections.Generic;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Null(int length) => Field(new NullDeviceField<V>(length));

    public B SearchView(
      ChildBuilder<CompositeDeviceFieldBuilder<V, SearchView<V>>> build)
    {
      ParamDeviceField<SearchView<V>, uint> keyParam = null;
      return Composite(
        (ref v, buf) => new SearchView<V> { Parent = v, Key = keyParam.GetValue(buf) },
        b => b
          .UintParameter(out keyParam)
          .Chain(build)
      );
    }

    public B Switch(ChildBuilder<SwitchDeviceFieldBuilder<V>> build) => Field(build(new()));

    public B ListView<V2>(
      Func<V, int> getLength,
      DeviceFieldBufGetter<ListView<V>, V2> getValue,
      ChildBuilder<CompositeDeviceFieldBuilder<ListView<V>, V2>> build)
    {
      ParamDeviceField<ListView<V>, uint> indexParam = null;
      return Composite(
        (ref v, buf) => new ListView<V>
        {
          Parent = v,
          Length = (uint)getLength(v),
          Index = indexParam.GetValue(buf[DataType.U64.SizeBytes()..]), // after Length field
        }, b => b
          .Uint((ref v) => (uint)getLength(v.Parent))
          .UintParameter(out indexParam)
          .Switch(sb => sb.Case(v => v.Index < v.Length, getValue, build))
      );
    }
  }

  public class NullDeviceField<T>(int length) : IDeviceField<T>
  {
    public int Length => length;
    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) => readBuf.Clear();
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) { }
  }

  public class SwitchDeviceFieldBuilder<T> : IDeviceFieldBuilder<T>
  {
    private readonly List<(FilterFunc<T>, IDeviceField<T>)> cases = [];
    private IDeviceField<T> defaultCase;

    public SwitchDeviceFieldBuilder<T> Case<V>(
      FilterFunc<T> filter,
      DeviceFieldBufGetter<T, V> getValue,
      ChildBuilder<CompositeDeviceFieldBuilder<T, V>> build)
    {
      cases.Add((filter, build(new CompositeDeviceFieldBuilder<T, V>(getValue)).Build()));
      return this;
    }

    public SwitchDeviceFieldBuilder<T> Default<V>(
      DeviceFieldBufGetter<T, V> getValue,
      ChildBuilder<CompositeDeviceFieldBuilder<T, V>> build)
    {
      if (defaultCase != null)
        throw new InvalidOperationException("Duplicate default case");
      defaultCase = build(new CompositeDeviceFieldBuilder<T, V>(getValue)).Build();
      return this;
    }

    public IDeviceField<T> Build() => new SwitchDeviceField<T>(defaultCase, cases);
  }

  public delegate bool FilterFunc<T>(T parent);
  public class SwitchDeviceField<T> : IDeviceField<T>
  {
    private readonly List<(FilterFunc<T>, IDeviceField<T>)> cases = [];
    private readonly IDeviceField<T> defaultCase;

    public int Length { get; }

    public SwitchDeviceField(IDeviceField<T> defaultCase, params List<(FilterFunc<T>, IDeviceField<T>)> cases)
    {
      var maxLen = defaultCase?.Length ?? 0;
      foreach (var (_, field) in cases)
        maxLen = Math.Max(maxLen, field.Length);
      Length = maxLen;

      this.defaultCase = PadToLength(defaultCase);
      foreach (var (filter, field) in cases)
        this.cases.Add((filter, PadToLength(field)));
    }

    private IDeviceField<T> PadToLength(IDeviceField<T> field)
    {
      if (field == null)
        return new NullDeviceField<T>(Length);
      if (field.Length == Length)
        return field;
      return new CompositeDeviceField<T, T>((ref v, _) => v, field, new NullDeviceField<T>(Length - field.Length));
    }

    private IDeviceField<T> Pick(T val)
    {
      foreach (var (filter, field) in cases)
      {
        if (filter(val))
          return field;
      }
      return defaultCase;
    }

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      Pick(parent).Read(ref parent, deviceBuf, readBuf, offset);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) =>
      Pick(parent).Write(ref parent, deviceBuf, writeBuf, offset);
  }

  public struct SearchView<T>
  {
    public T Parent;
    public uint Key;
  }

  public struct ListView<T>
  {
    public T Parent;
    public uint Length;
    public uint Index;
  }
}