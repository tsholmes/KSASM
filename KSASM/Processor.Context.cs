
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
      var terminal = new GaugeTerminal();
      terminal.Update();
      return [
        SystemDeviceDefinition.Make("system", vehicle.System),
        VehicleDeviceDefinition.Make("vehicle", vehicle),
        FlightComputerDeviceDefinition.Make("fc", vehicle),
        TerminalDeviceDefinition.Make("term", terminal),
      ];
    }
  }

  public class StandaloneProcContext() : ProcContext("standalone")
  {
    public readonly ImGuiTerminal Terminal = new();
    public override IDevice[] MakeDevices() => [
      TerminalDeviceDefinition.Make("term", Terminal),
    ];
  }
}