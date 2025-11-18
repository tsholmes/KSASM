
using Brutal.Numerics;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Bool(DeviceFieldGetter<V, bool> getter, DeviceFieldSetter<V, bool> setter = null) =>
      Leaf(DataType.U8, BoolValueConverter.Instance, getter, setter);

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

  public class BoolValueConverter : UnsignedValueConverter<BoolValueConverter, bool>
  {
    public override bool FromUnsigned(ulong val) => val != 0;
    public override ulong ToUnsigned(bool val) => val ? 1u : 0u;
  }

  public class UintValueConverter : UnsignedValueConverter<UintValueConverter, uint>
  {
    public override uint FromUnsigned(ulong val) => (uint)val;
    public override ulong ToUnsigned(uint val) => val;
  }

  public class DoubleValueConverter : FloatValueConverter<DoubleValueConverter, double>
  {
    public override double FromFloat(double val) => val;
    public override double ToFloat(double val) => val;
  }
}