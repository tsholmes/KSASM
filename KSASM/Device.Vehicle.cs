
using System;
using System.Reflection;
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public class VehicleDeviceDefinition : DeviceDefinition<Vehicle, VehicleDeviceDefinition>
  {
    public override ulong GetId(Vehicle device) => 2;
    public override IDeviceField<Vehicle> RootField { get; } =
      new RootDeviceField<Vehicle>(Hash, AVel, Inputs);

    public static readonly IDeviceField<Vehicle> Hash = new HashField().ReadOnly();
    public static readonly IDeviceField<Vehicle> AVel = new AVelField().ReadOnly();
    public static readonly IDeviceField<Vehicle> Inputs = new InputsField();

    public class HashField() : UintDeviceField<Vehicle>(0)
    {
      protected override uint GetValue(ref Vehicle parent) => parent.Hash;
      protected override void SetValue(ref Vehicle parent, uint value) { }
    }

    public class AVelField() : Double3DeviceField<Vehicle>(Hash.End())
    {
      protected override double3 GetValue(ref Vehicle parent, Span<byte> deviceBuf) => parent.BodyRates;
      protected override void SetValue(ref Vehicle parent, double3 self) { }
    }

    public class InputsField()
    : ValueCompositeDeviceField<Vehicle, ManualControlInputs>(AVel.End(), ThrustCommand)
    {
      public static readonly ThrustCommandField ThrustCommand = new();

      private static FieldInfo _field;
      private static FieldInfo Field =>
        _field ??= typeof(Vehicle).GetField(
          "_manualControlInputs",
          BindingFlags.Instance | BindingFlags.NonPublic);

      protected override ManualControlInputs GetValue(ref Vehicle parent, Span<byte> deviceBuf) =>
        (ManualControlInputs)Field.GetValue(parent);
      protected override void SetValue(ref Vehicle parent, ManualControlInputs self) => Field.SetValue(parent, self);

      public class ThrustCommandField()
      : LeafDeviceField<ManualControlInputs, ThrusterMapFlags>(DataType.U64, 0, EnumConverter.Instance)
      {
        protected override ThrusterMapFlags GetValue(ref ManualControlInputs parent) =>
          parent.ThrusterCommandFlags;
        protected override void SetValue(ref ManualControlInputs parent, ThrusterMapFlags value) =>
          parent.ThrusterCommandFlags = value;

        private class EnumConverter : UnsignedValueConverter<EnumConverter, ThrusterMapFlags>
        {
          public override ThrusterMapFlags FromUnsigned(ulong val) => (ThrusterMapFlags)val;
          public override ulong ToUnsigned(ThrusterMapFlags val) => (ulong)val;
        }
      }
    }
  }
}