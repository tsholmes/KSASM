
using KSA;

namespace KSASM
{
  public class OrbitDeviceField<T>(DeviceFieldBufGetter<T, Orbit> getValue)
  : CompositeDeviceField<T, Orbit>(getValue, Parent, Periapsis, Apoapsis, Period)
  {
    public static readonly UintDeviceField<Orbit> Parent = new((ref o) => o?.Parent?.Hash ?? 0);
    public static readonly DoubleDeviceField<Orbit> Periapsis = new((ref o) => o?.Periapsis ?? 0);
    public static readonly DoubleDeviceField<Orbit> Apoapsis = new((ref o) => o?.Apoapsis ?? 0);
    public static readonly DoubleDeviceField<Orbit> Period = new((ref o) => o?.Period ?? 0);
  }
}