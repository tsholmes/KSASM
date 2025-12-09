
using System;
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public class VehicleDeviceDefinition : DeviceDefinition<Vehicle, VehicleDeviceDefinition>
  {
    public override ulong GetId(Vehicle device) => 2;

    public override IDeviceFieldBuilder<Vehicle> Build(RootDeviceFieldBuilder<Vehicle> b) => b
      .Uint("hash", (ref v) => v.Hash)
      .Double3("avel", (ref v, _) => v.BodyRates)
      .DoubleQuat("body2cci", (ref v, _) => v.GetBody2Cci())
      .DoubleQuat("body2cce", (ref v, _) => v.Body2Cce)
      .DoubleQuat("lvlh2cce", (ref v, _) => v.GetLvlh2Cce() ?? doubleQuat.Identity)
      .DoubleQuat("enu2cce", (ref v, _) => v.GetEnu2Cce() ?? doubleQuat.Identity)
      .ValueComposite("inputs", (ref v, _) => InputField.Get(v), SetManualControlInputs, b => b
        .Bool("engine_on", (ref v) => v.EngineOn, (ref v, on) => v.EngineOn = on)
        .Double("engine_throttle", (ref v) => v.EngineThrottle, (ref v, t) => v.EngineThrottle = (float)t)
        .Leaf("thrust_command", DataType.U64, ThrustCommandConverter.Instance,
          (ref v) => v.ThrusterCommandFlags, (ref v, cmd) => v.ThrusterCommandFlags = cmd))
      .FlightPlan(null, (ref v, _) => v.FlightPlan)
      .List("parts", (ref v) => v.Parts.Parts, (b, get) => b.Part("part", get));
    
    private static void SetManualControlInputs(ref Vehicle vehicle, ManualControlInputs inputs)
    {
      inputs.EngineThrottle = Math.Clamp(inputs.EngineThrottle, vehicle.GetMinThrottle(), 1f);
      InputField.Set(vehicle, inputs);
    }

    public static readonly FieldWrapper<Vehicle, ManualControlInputs> InputField = new("_manualControlInputs");

    public class ThrustCommandConverter : UnsignedValueConverter<ThrustCommandConverter, ThrusterMapFlags>
    {
      public override ThrusterMapFlags FromUnsigned(ulong val) => (ThrusterMapFlags)val;
      public override ulong ToUnsigned(ThrusterMapFlags val) => (ulong)val;
    }
  }

  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Part(string name, DeviceFieldBufGetter<V, Part> getter) => NonNull(name, getter, b => b
      .Ulong("id", (ref p) => p.Id)
      .Ulong("parent", (ref p) => p.Parent?.Id ?? 0)
      .String("name", 64, (ref p, _) => p.Name)
    );
  }
}