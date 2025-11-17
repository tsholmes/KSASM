
using System;

namespace KSASM
{
  public static class DeviceFieldExtensions
  {
    public static ReadOnlyDeviceField<T> ReadOnly<T>(this IDeviceField<T> field) => new(field);
    public static OffsetDeviceField<T> WithOffset<T>(this IDeviceField<T> field, int offset) => new(offset, field);
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

  public class OffsetDeviceField<T>(int offset, IDeviceField<T> inner) : IDeviceField<T>
  {
    public int Offset => offset;
    public int Length => inner.Length;
    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      inner.Read(ref parent, deviceBuf, readBuf, offset);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) =>
      inner.Read(ref parent, deviceBuf, writeBuf, offset);
  }

  public class SearchViewDeviceField<T>(int offset, IDeviceField<SearchView<T>> resultField)
  : CompositeDeviceField<T, SearchView<T>>(offset, GetValue, KeyParam, resultField.WithOffset(KeyParam.End()))
  {
    private static SearchView<T> GetValue(ref T parent, Span<byte> deviceBuf) =>
       new() { Parent = parent, Key = KeyParam.GetValue(deviceBuf) };

    public static readonly ParamDeviceField<SearchView<T>, uint> KeyParam =
      new(DataType.U64, 0, UintValueConverter.Instance);
  }

  public struct SearchView<T>
  {
    public T Parent;
    public ulong Key;
  }
}