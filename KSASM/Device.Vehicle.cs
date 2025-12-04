
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public class VehicleDeviceDefinition : DeviceDefinition<Vehicle, VehicleDeviceDefinition>
  {
    public override ulong GetId(Vehicle device) => 2;

    public override IDeviceFieldBuilder<Vehicle> Build(RootDeviceFieldBuilder<Vehicle> b) => b
      .Uint((ref v) => v.Hash)
      .Double3((ref v, _) => v.BodyRates)
      .DoubleQuat((ref v, _) => v.GetBody2Cci())
      .DoubleQuat((ref v, _) => v.Body2Cce)
      .DoubleQuat((ref v, _) => v.GetLvlh2Cce() ?? doubleQuat.Identity)
      .DoubleQuat((ref v, _) => v.GetEnu2Cce() ?? doubleQuat.Identity)
      .ValueComposite((ref v, _) => InputField.Get(v), (ref v, i) => InputField.Set(v, i), b => b
        .Leaf(DataType.U64, ThrustCommandConverter.Instance,
        (ref v) => v.ThrusterCommandFlags, (ref v, cmd) => v.ThrusterCommandFlags = cmd))
      .FlightPlan((ref v, _) => v.FlightPlan)
      .List((ref v) => v.Parts.Parts, (b, get) => b.Part(get));

    public readonly FieldWrapper<Vehicle, ManualControlInputs> InputField = new("_manualControlInputs");

    public class ThrustCommandConverter : UnsignedValueConverter<ThrustCommandConverter, ThrusterMapFlags>
    {
      public override ThrusterMapFlags FromUnsigned(ulong val) => (ThrusterMapFlags)val;
      public override ulong ToUnsigned(ThrusterMapFlags val) => (ulong)val;
    }
  }

  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Part(DeviceFieldBufGetter<V, Part> getter) => NonNull(getter, b => b
      .Ulong((ref p) => p.Id)
      .Ulong((ref p) => p.Parent?.Id ?? 0)
      .String(64, (ref p, _) => p.Name)
    );
  }
}