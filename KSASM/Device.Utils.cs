
using System;

namespace KSASM
{
  public static class DeviceFieldExtensions
  {
    public static ReadOnlyDeviceField<T> ReadOnly<T>(this IDeviceField<T> field) => new(field);
    public static NullableDeviceField<T> Nullable<T>(this IDeviceField<T> field) where T : class => new(field);
    public static int End<T>(this IDeviceField<T> field) => field.Offset + field.Length;
  }

  public class ReadOnlyDeviceField<T>(IDeviceField<T> inner) : IDeviceField<T>
  {
    public int Offset => inner.Offset;
    public int Length => inner.Length;
    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      inner.Read(ref parent, deviceBuf, readBuf, offset);

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) { }
  }

  public class NullableDeviceField<T>(IDeviceField<T> inner) : IDeviceField<T> where T : class
  {
    public int Offset => inner.Offset;
    public int Length => inner.Length;

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      if (parent == null)
        readBuf.Clear();
      else
        inner.Read(ref parent, deviceBuf, readBuf, offset);
    }

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      if (parent != null)
        inner.Write(ref parent, deviceBuf, writeBuf, offset);
    }
  }

  public class ProxyDeviceField<T, V>(
    int offset,
    IDeviceField<V> innerField,
    ProxyDeviceField<T, V>.InnerAccessor innerAccessor
  ) : IDeviceField<T>
  {
    public delegate V InnerAccessor(ref T parent, Span<byte> deviceBuf);

    public int Offset => offset;
    public int Length => innerField.Length;

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset)
    {
      var inner = innerAccessor(ref parent, deviceBuf);
      innerField.Read(ref inner, deviceBuf, readBuf, offset);
    }

    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset)
    {
      var inner = innerAccessor(ref parent, deviceBuf);
      innerField.Write(ref inner, deviceBuf, writeBuf, offset);
    }
  }

  public class SearchViewDeviceField<T, V>(
    int offset,
    IDeviceField<V> resultField,
    ProxyDeviceField<SearchView<T>, V>.InnerAccessor resultAccessor
  ) : CompositeDeviceField<T, SearchView<T>>(
    offset,
    KeyParam,
    new ProxyDeviceField<SearchView<T>, V>(KeyParam.End(), resultField, resultAccessor)
  )
  {
    protected override SearchView<T> GetValue(ref T parent, Span<byte> deviceBuf)
    {
      var key = KeyParam.GetValue(deviceBuf);
      return new()
      {
        Parent = parent,
        Key = key,
      };
    }

    public static readonly ParamDeviceField<SearchView<T>, uint> KeyParam =
      new(DataType.U64, 0, UintValueConverter.Instance);
  }

  public struct SearchView<T>
  {
    public T Parent;
    public ulong Key;
  }
}