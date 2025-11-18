
using KSA;

namespace KSASM
{
  public class VehicleDeviceDefinition : DeviceDefinition<Vehicle, VehicleDeviceDefinition>
  {
    public override ulong GetId(Vehicle device) => 2;

    public override IDeviceFieldBuilder<Vehicle> Build(RootDeviceFieldBuilder<Vehicle> b) => b
      .Uint((ref v) => v.Hash)
      .Double3((ref v, _) => v.BodyRates)
      .ValueComposite((ref v, _) => InputField.Get(v), (ref v, i) => InputField.Set(v, i), b => b
        .Leaf(DataType.U64, ThrustCommandConverter.Instance,
        (ref v) => v.ThrusterCommandFlags, (ref v, cmd) => v.ThrusterCommandFlags = cmd))
      .FlightPlan((ref v, _) => v.FlightPlan);

    public readonly FieldWrapper<Vehicle, ManualControlInputs> InputField = new("_manualControlInputs");

    public class ThrustCommandConverter : UnsignedValueConverter<ThrustCommandConverter, ThrusterMapFlags>
    {
      public override ThrusterMapFlags FromUnsigned(ulong val) => (ThrusterMapFlags)val;
      public override ulong ToUnsigned(ThrusterMapFlags val) => (ulong)val;
    }
  }
}