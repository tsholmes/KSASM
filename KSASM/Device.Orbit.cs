
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

  public class PatchDeviceField<T>(DeviceFieldBufGetter<T, PatchedConic> getValue)
  : CompositeDeviceField<T, PatchedConic>(getValue, StartTime, EndTime, Orbit)
  {
    public static readonly DoubleDeviceField<PatchedConic> StartTime = new((ref p) => p?.StartTime.Seconds() ?? 0);
    public static readonly DoubleDeviceField<PatchedConic> EndTime = new((ref p) => p?.EndTime.Seconds() ?? 0);
    public static readonly OrbitDeviceField<PatchedConic> Orbit = new((ref p, _) => p?.Orbit);
  }
}