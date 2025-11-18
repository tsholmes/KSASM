
using KSA;

namespace KSASM
{
  public class FlightComputerDeviceDefinition : DeviceDefinition<FlightComputer, FlightComputerDeviceDefinition>
  {
    public override ulong GetId(FlightComputer device) => 3;

    public override IDeviceFieldBuilder<FlightComputer> Build(RootDeviceFieldBuilder<FlightComputer> b) => b
      .Leaf(DataType.U64, ThrustModeConverter.Instance,
        (ref fc) => fc.ManualThrustMode, (ref fc, mode) => fc.SetManualThrustMode(mode))
      .ListView(
        fc => fc.BurnPlan.BurnCount,
        b => b.Burn((ref v, _) => v.Parent.BurnPlan.TryGetBurn((int)v.Index, out var burn) ? burn : null));

    private class ThrustModeConverter : UnsignedValueConverter<ThrustModeConverter, FlightComputerManualThrustMode>
    {
      public override FlightComputerManualThrustMode FromUnsigned(ulong val) => (FlightComputerManualThrustMode)val;
      public override ulong ToUnsigned(FlightComputerManualThrustMode val) => (ulong)val;
    }
  }
}