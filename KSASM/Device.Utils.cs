
using System;
using System.Linq;

namespace KSASM
{
  public static class DeviceFieldExtensions
  {
  }

  public class NullDeviceField<T>(int length) : IDeviceField<T>
  {
    public int Length => length;
    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) => readBuf.Clear();
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) { }
  }

  public class SwitchDeviceField<T>(Func<T, int> switchIndex, params IDeviceField<T>[] options) : IDeviceField<T>
  {
    public int Length { get; } = options.Max(o => o.Length);

    public void Read(ref T parent, Span<byte> deviceBuf, Span<byte> readBuf, int offset) =>
      options[switchIndex(parent)].Read(ref parent, deviceBuf, readBuf, offset);
    public void Write(ref T parent, Span<byte> deviceBuf, Span<byte> writeBuf, int offset) =>
      options[switchIndex(parent)].Read(ref parent, deviceBuf, writeBuf, offset);
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

  public struct ListView<T>
  {
    public T Parent;
    public uint Length;
    public uint Index;
  }

  public class ListViewDeviceField<T>(Func<T, int> getLength, IDeviceField<ListView<T>> resultField)
  : CompositeDeviceField<T, ListView<T>>(Getter(getLength), LengthField, IndexParam, SwitchedResult(resultField))
  {
    private static DeviceFieldBufGetter<T, ListView<T>> Getter(Func<T, int> getLength) => (ref p, buf) => new()
    {
      Parent = p,
      Length = (uint)getLength(p),
      Index = IndexParam.GetValue(buf[LengthField.Length..]),
    };

    private static SwitchDeviceField<ListView<T>> SwitchedResult(IDeviceField<ListView<T>> resultField) =>
      new(v => v.Index < v.Length ? 0 : 1, resultField, new NullDeviceField<ListView<T>>(resultField.Length));

    public static readonly UintDeviceField<ListView<T>> LengthField = new((ref v) => v.Length);

    public static readonly ParamDeviceField<ListView<T>, uint> IndexParam =
      new(DataType.U64, UintValueConverter.Instance);
  }
}