
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public struct VehicleFC
  {
    public Vehicle Vehicle;
    public FlightComputer FC => Vehicle.FlightComputer;

    public static implicit operator VehicleFC(Vehicle vehicle) => new() { Vehicle = vehicle };
  }

  public class FlightComputerDeviceDefinition : DeviceDefinition<VehicleFC, FlightComputerDeviceDefinition>
  {
    public override ulong GetId(VehicleFC device) => 3;

    public override IDeviceFieldBuilder<VehicleFC> Build(RootDeviceFieldBuilder<VehicleFC> b) => b
      .Leaf("thrust_mode", DataType.U64, ThrustModeConverter.Instance,
        (ref fc) => fc.FC.ManualThrustMode, (ref fc, mode) => fc.FC.SetManualThrustMode(mode))
      .Bool("burns.outdated", (ref fc) => fc.FC.BurnPlan.FlightPlansOutOfDate && AnyFutureBurns(fc.FC.BurnPlan))
      .Double("burns.add_time", (ref fc) => 0, (ref fc, v) => AddBurn(fc, v))
      .ListView(
        "burns",
        fc => fc.FC.BurnPlan.BurnCount,
        b => b.Burn(
          "burn",
          (ref v, _) => v.Parent.FC.BurnPlan.TryGetBurn((int)v.Index, out var burn) ? burn : null,
          (ref v, b) => v.Parent.FC.BurnUpdated(b)));

    private static bool AnyFutureBurns(BurnPlan burns)
    {
      var time = Universe.GetElapsedSimTime();
      for (var i = 0; i < burns.BurnCount; i++)
      {
        if (burns.TryGetBurn(i, out var burn) && burn.Time >= time)
          return true;
      }
      return false;
    }

    private static void AddBurn(VehicleFC fc, double time)
    {
      var burn = Burn.Create(OrbitPointCce.Zero, time, double3.Zero, fc.Vehicle.Patch, fc.Vehicle);
      fc.FC.AddBurn(burn);
    }

    private class ThrustModeConverter : UnsignedValueConverter<ThrustModeConverter, FlightComputerManualThrustMode>
    {
      public override FlightComputerManualThrustMode FromUnsigned(ulong val) => (FlightComputerManualThrustMode)val;
      public override ulong ToUnsigned(FlightComputerManualThrustMode val) => (ulong)val;
    }
  }
}