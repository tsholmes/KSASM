
using System;

namespace KSASM
{
  public static class DeviceFieldExtensions
  {
  }

  public struct SearchView<T>
  {
    public T Parent;
    public uint Key;
  }

  public class SearchViewDeviceField<T>(IDeviceField<SearchView<T>> resultField)
  : CompositeDeviceField<T, SearchView<T>>(GetValue, KeyParam, resultField)
  {
    private static SearchView<T> GetValue(ref T parent, Span<byte> deviceBuf) =>
       new() { Parent = parent, Key = KeyParam.GetValue(deviceBuf) };

    public static readonly ParamDeviceField<SearchView<T>, uint> KeyParam =
      new(DataType.U64, UintValueConverter.Instance);
  }
}