
using Brutal.Numerics;

namespace KSASM
{
  public class UintDeviceField<T>(
    int offset,
    DeviceFieldGetter<T, uint> getter,
    DeviceFieldSetter<T, uint> setter = null)
  : LeafDeviceField<T, uint>(DataType.U64, offset, UintValueConverter.Instance, getter, setter)
  { }

  public class DoubleDeviceField<T>(
    int offset,
    DeviceFieldGetter<T, double> getter,
    DeviceFieldSetter<T, double> setter = null)
  : LeafDeviceField<T, double>(DataType.F64, offset, DoubleValueConverter.Instance, getter, setter)
  { }

  public class Double3DeviceField<T>(
    int offset,
    DeviceFieldBufGetter<T, double3> getValue,
    DeviceFieldSetter<T, double3> setValue = null
  ) : ValueCompositeDeviceField<T, double3>(offset, getValue, setValue, X, Y, Z)
  {
    public static readonly DoubleDeviceField<double3> X = MakeElement(0);
    public static readonly DoubleDeviceField<double3> Y = MakeElement(1);
    public static readonly DoubleDeviceField<double3> Z = MakeElement(2);

    private static DoubleDeviceField<double3> MakeElement(int index) =>
      new(index * DataType.F64.SizeBytes(), (ref v) => v[index], (ref v, el) => v[index] = el);
  }
}