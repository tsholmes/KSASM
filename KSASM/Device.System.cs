
using System;
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<CelestialSystem, SystemDeviceDefinition>
  {
    public override ulong GetId(CelestialSystem device) => 1;

    public override RootDeviceField<CelestialSystem> RootField { get; } = new(Time, AstronomicalSearch);

    public static readonly DoubleDeviceField<CelestialSystem> Time = new((ref _) => Universe.GetElapsedSeconds());
    public static readonly SearchViewDeviceField<CelestialSystem> AstronomicalSearch = new(new AstronomicalField());

    private static Astronomical SearchAstronomical(ref SearchView<CelestialSystem> search, Span<byte> _) =>
      search.Key == 0 ? search.Parent.GetWorldSun() : search.Parent.Get(search.Key);

    public class AstronomicalField()
    : CompositeDeviceField<SearchView<CelestialSystem>, Astronomical>(SearchAstronomical, Hash, Orbit)
    {
      public static readonly UintDeviceField<Astronomical> Hash = new((ref a) => a?.Hash ?? 0);
      public static readonly IDeviceField<Astronomical> Orbit =
        new OrbitDeviceField<Astronomical>((ref a, _) => (a as IOrbiting)?.Orbit);
    }
  }
}