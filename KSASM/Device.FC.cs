
using KSA;

namespace KSASM
{
  public class FlightComputerDeviceDefinition : DeviceDefinition<FlightComputer, FlightComputerDeviceDefinition>
  {
    public override ulong GetId(FlightComputer device) => 3;
    public override IDeviceField<FlightComputer> RootField { get; } =
      new RootDeviceField<FlightComputer>(ThrustMode);

    public static readonly ThrustModeField ThrustMode = new();

    public class ThrustModeField : LeafDeviceField<FlightComputer, FlightComputerManualThrustMode>
    {
      public ThrustModeField() : base(DataType.U64, 0, EnumConverter.Instance) { }

      protected override FlightComputerManualThrustMode GetValue(ref FlightComputer parent) =>
        parent.ManualThrustMode;
      protected override void SetValue(ref FlightComputer parent, FlightComputerManualThrustMode value) =>
        parent.SetManualThrustMode(value);

      public class EnumConverter : UnsignedValueConverter<EnumConverter, FlightComputerManualThrustMode>
      {
        public override FlightComputerManualThrustMode FromUnsigned(ulong val) =>
          (FlightComputerManualThrustMode)val;
        public override ulong ToUnsigned(FlightComputerManualThrustMode val) => (ulong)val;
      }
    }
  }
}