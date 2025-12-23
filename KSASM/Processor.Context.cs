
using KSA;

namespace KSASM
{
  public abstract class ProcContext(string id)
  {
    public readonly string Id = id;

    public abstract IDevice[] MakeDevices();
  }

  public class VehicleProcContext(Vehicle vehicle) : ProcContext(vehicle.Id)
  {
    private readonly Vehicle vehicle = vehicle;

    public override IDevice[] MakeDevices()
    {
      var terminal = new Terminal(TerminalLabel.Labels);
      terminal.Update();
      return [
        SystemDeviceDefinition.Make("system", new() { System = vehicle.System, Terminal = terminal }),
        VehicleDeviceDefinition.Make("vehicle", vehicle),
        FlightComputerDeviceDefinition.Make("fc", vehicle),
      ];
    }
  }

  public class StandaloneProcContext() : ProcContext("standalone")
  {
    public override IDevice[] MakeDevices() => [];
  }
}