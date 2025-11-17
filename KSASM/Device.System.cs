
using System;
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<CelestialSystem, SystemDeviceDefinition>
  {
    public override ulong GetId(CelestialSystem device) => 1;

    public override IDeviceField<CelestialSystem> RootField { get; } =
      new RootDeviceField<CelestialSystem>(AstronomicalSearch);

    public static readonly SearchViewDeviceField<CelestialSystem, Astronomical> AstronomicalSearch =
      new(0, new AstronomicalField(0).Nullable(), SearchAstronomical);

    private static Astronomical SearchAstronomical(
      ref SearchView<CelestialSystem> search,
      Span<byte> deviceBuf
    ) => search.Key == 0 ? search.Parent.GetWorldSun() : search.Parent.Get((uint)search.Key);

    public class AstronomicalField(int offset)
    : CompositeDeviceField<Astronomical, Astronomical>(offset, Hash, Orbit)
    {
      protected override Astronomical GetValue(ref Astronomical parent, Span<byte> deviceBuf) => parent;

      public static readonly HashField Hash = new();
      public static readonly OrbitField Orbit = new();

      public class HashField() : ReadOnlyUintDeviceField<Astronomical>(0)
      {
        protected override uint GetValue(ref Astronomical parent) => parent.Hash;
      }

      public class OrbitField() : OrbitDeviceField<Astronomical>(Hash.End())
      {
        protected override Orbit GetValue(ref Astronomical parent, Span<byte> deviceBuf) =>
          (parent as IOrbiting)?.Orbit;
      }
    }
  }
}