
using System;
using System.Reflection;
using KSA;

namespace KSASM
{
  public class VehicleDeviceDefinition : DeviceDefinition<Vehicle, VehicleDeviceDefinition>
  {
    public override ulong GetId(Vehicle device) => 2;
    public override RootDeviceField<Vehicle> RootField { get; } = new(Hash, AVel, Inputs, Patches);

    public static readonly UintDeviceField<Vehicle> Hash = new((ref v) => v.Hash);
    public static readonly Double3DeviceField<Vehicle> AVel = new((ref v, _) => v.BodyRates);
    public static readonly InputsField Inputs = new();
    public static readonly ListViewDeviceField<Vehicle> Patches = new(
      v => v.FlightPlan.Patches.Count,
      new PatchDeviceField<ListView<Vehicle>>((ref v, _) => v.Parent.FlightPlan.Patches[(int)v.Index]));

    public class InputsField()
    : ValueCompositeDeviceField<Vehicle, ManualControlInputs>(GetValue, SetValue, ThrustCommand)
    {
      public static readonly LeafDeviceField<ManualControlInputs, ThrusterMapFlags> ThrustCommand = new(
        DataType.U64, ThrustCommandConverter.Instance,
        (ref v) => v.ThrusterCommandFlags, (ref v, cmd) => v.ThrusterCommandFlags = cmd);

      private static FieldInfo _field;
      private static FieldInfo Field =>
        _field ??= typeof(Vehicle).GetField(
          "_manualControlInputs",
          BindingFlags.Instance | BindingFlags.NonPublic);

      private static ManualControlInputs GetValue(ref Vehicle parent, Span<byte> deviceBuf) =>
        (ManualControlInputs)Field.GetValue(parent);
      private static void SetValue(ref Vehicle parent, ManualControlInputs self) => Field.SetValue(parent, self);
      private class ThrustCommandConverter : UnsignedValueConverter<ThrustCommandConverter, ThrusterMapFlags>
      {
        public override ThrusterMapFlags FromUnsigned(ulong val) => (ThrusterMapFlags)val;
        public override ulong ToUnsigned(ThrusterMapFlags val) => (ulong)val;
      }
    }
  }
}