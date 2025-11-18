
using Brutal.Numerics;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Uint(DeviceFieldGetter<V, uint> getter, DeviceFieldSetter<V, uint> setter = null) =>
      Leaf(DataType.U64, UintValueConverter.Instance, getter, setter);

    public B UintParameter(out ParamDeviceField<V, uint> field) =>
      Parameter(DataType.U64, UintValueConverter.Instance, out field);

    public B Double(DeviceFieldGetter<V, double> getter, DeviceFieldSetter<V, double> setter = null) =>
      Leaf(DataType.F64, DoubleValueConverter.Instance, getter, setter);

    public B Double3(DeviceFieldBufGetter<V, double3> getter, DeviceFieldSetter<V, double3> setter = null) =>
      ValueComposite(getter, setter, b => b
        .Double((ref d3) => d3[0], (ref d3, v) => d3[0] = v)
        .Double((ref d3) => d3[1], (ref d3, v) => d3[1] = v)
        .Double((ref d3) => d3[2], (ref d3, v) => d3[2] = v)
      );
  }
}