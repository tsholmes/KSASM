
using KSA;

namespace KSASM
{
  public class OrbitDeviceField<T>(int offset, DeviceFieldBufGetter<T, Orbit> getValue)
  : CompositeDeviceField<T, Orbit>(offset, getValue, Parent, Periapsis, Apoapsis, Period)
  {
    public static readonly UintDeviceField<Orbit> Parent = new(0, (ref o) => o?.Parent?.Hash ?? 0);
    public static readonly DoubleDeviceField<Orbit> Periapsis = new(Parent.End(), (ref o) => o?.Periapsis ?? 0);
    public static readonly DoubleDeviceField<Orbit> Apoapsis = new(Periapsis.End(), (ref o) => o?.Apoapsis ?? 0);
    public static readonly DoubleDeviceField<Orbit> Period = new(Apoapsis.End(), (ref o) => o?.Period ?? 0);
  }
}