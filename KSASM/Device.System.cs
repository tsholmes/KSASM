
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<CelestialSystem, SystemDeviceDefinition>
  {
    public override ulong GetId(CelestialSystem device) => 1;

    public override IDeviceFieldBuilder<CelestialSystem> Build(RootDeviceFieldBuilder<CelestialSystem> b) => b
      .Double((ref _) => Universe.GetElapsedSeconds())
      .SearchView(b => b.Astronomical((ref v, _) => v.Key == 0 ? v.Parent.GetWorldSun() : v.Parent.Get(v.Key))
    );
  }
}