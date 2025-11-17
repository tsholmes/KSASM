
using Brutal.Numerics;

namespace KSASM
{
  public class UintDeviceField<T>(
    DeviceFieldGetter<T, uint> getter,
    DeviceFieldSetter<T, uint> setter = null)
  : LeafDeviceField<T, uint>(DataType.U64, UintValueConverter.Instance, getter, setter)
  { }

  public class DoubleDeviceField<T>(
    DeviceFieldGetter<T, double> getter,
    DeviceFieldSetter<T, double> setter = null)
  : LeafDeviceField<T, double>(DataType.F64, DoubleValueConverter.Instance, getter, setter)
  { }

  public class Double3DeviceField<T>(
    DeviceFieldBufGetter<T, double3> getValue,
    DeviceFieldSetter<T, double3> setValue = null
  ) : ValueCompositeDeviceField<T, double3>(getValue, setValue, X, Y, Z)
  {
    public static readonly DoubleDeviceField<double3> X = MakeElement(0);
    public static readonly DoubleDeviceField<double3> Y = MakeElement(1);
    public static readonly DoubleDeviceField<double3> Z = MakeElement(2);

    private static DoubleDeviceField<double3> MakeElement(int index) =>
      new((ref v) => v[index], (ref v, el) => v[index] = el);
  }
}