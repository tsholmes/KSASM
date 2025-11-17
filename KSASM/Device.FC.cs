
using KSA;

namespace KSASM
{
  public class FlightComputerDeviceDefinition : DeviceDefinition<FlightComputer, FlightComputerDeviceDefinition>
  {
    public override ulong GetId(FlightComputer device) => 3;
    public override RootDeviceField<FlightComputer> RootField { get; } = new(ThrustMode);

    public static readonly LeafDeviceField<FlightComputer, FlightComputerManualThrustMode> ThrustMode = new(
      DataType.U64,
      0,
      ThrustModeConverter.Instance,
      (ref fc) => fc.ManualThrustMode,
      (ref fc, mode) => fc.SetManualThrustMode(mode));

    private class ThrustModeConverter : UnsignedValueConverter<ThrustModeConverter, FlightComputerManualThrustMode>
    {
      public override FlightComputerManualThrustMode FromUnsigned(ulong val) => (FlightComputerManualThrustMode)val;
      public override ulong ToUnsigned(FlightComputerManualThrustMode val) => (ulong)val;
    }
  }
}