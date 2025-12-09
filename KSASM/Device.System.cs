
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<CelestialSystem, SystemDeviceDefinition>
  {
    public override ulong GetId(CelestialSystem device) => 1;

    public override IDeviceFieldBuilder<CelestialSystem> Build(RootDeviceFieldBuilder<CelestialSystem> b) => b
      .Double("time", (ref _) => Universe.GetElapsedSeconds())
      .SearchView(null, "hash_param", b => b
        .Astronomical(null, (ref v, _) => v.Key == 0 ? v.Parent.GetWorldSun() : v.Parent.Get(v.Key))
      .Raw(
        "terminal_data",
        Terminal.TOTAL_SIZE,
        (ref v) => AsmUi.Current.Terminal.Data,
        (ref v, data) => AsmUi.Current.Terminal.Update())
    );
  }
}