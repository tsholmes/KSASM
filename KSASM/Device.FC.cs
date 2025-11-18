
using KSA;

namespace KSASM
{
  public class FlightComputerDeviceDefinition : DeviceDefinition<FlightComputer, FlightComputerDeviceDefinition>
  {
    public override ulong GetId(FlightComputer device) => 3;

    public override IDeviceFieldBuilder<FlightComputer> Build(RootDeviceFieldBuilder<FlightComputer> b) => b
      .Leaf(
        DataType.U64,
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