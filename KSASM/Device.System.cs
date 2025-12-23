
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<SystemDevice, SystemDeviceDefinition>
  {
    public override ulong GetId(SystemDevice device) => 1;

    public override IDeviceFieldBuilder<SystemDevice> Build(RootDeviceFieldBuilder<SystemDevice> b) => b
      .Double("time", (ref _) => Universe.GetElapsedSeconds())
      .SearchView(null, "hash_param", b => b
        .Astronomical(null,
          (ref v, _) => v.Key == 0 ? v.Parent.System.GetWorldSun() : v.Parent.System.Get(v.Key)))
      .Raw(
        "terminal_data",
        Terminal.TOTAL_SIZE,
        (ref v) => v.Terminal.Data,
        (ref v, data) => v.Terminal.Update());
  }

  public struct SystemDevice
  {
    public CelestialSystem System;
    public Terminal Terminal;
  }
}