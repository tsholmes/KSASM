
using System;
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<CelestialSystem, SystemDeviceDefinition>
  {
    public override ulong GetId(CelestialSystem device) => 1;

    public override RootDeviceField<CelestialSystem> RootField { get; } = new(AstronomicalSearch);

    public static readonly SearchViewDeviceField<CelestialSystem, Astronomical> AstronomicalSearch =
      new(0, new AstronomicalField(0), SearchAstronomical);

    private static Astronomical SearchAstronomical(
      ref SearchView<CelestialSystem> search,
      Span<byte> deviceBuf
    ) => search.Key == 0 ? search.Parent.GetWorldSun() : search.Parent.Get((uint)search.Key);

    public class AstronomicalField(int offset)
    : CompositeDeviceField<Astronomical, Astronomical>(offset, (ref v, _) => v, Hash, Orbit)
    {
      public static readonly UintDeviceField<Astronomical> Hash = new(0, (ref a) => a?.Hash ?? 0);
      public static readonly IDeviceField<Astronomical> Orbit =
        new OrbitDeviceField<Astronomical>(Hash.End(), (ref a, _) => (a as IOrbiting)?.Orbit);
    }
  }
}