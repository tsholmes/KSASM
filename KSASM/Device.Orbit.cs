
using KSA;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Orbit(DeviceFieldBufGetter<V, Orbit> getter) => Composite(getter, b => b
      .Uint((ref o) => o?.Parent?.Hash ?? 0)
      .Double((ref o) => o?.Periapsis ?? 0)
      .Double((ref o) => o?.Apoapsis ?? 0)
      .Double((ref o) => o?.Period ?? 0)
    );

    public B Patch(DeviceFieldBufGetter<V, PatchedConic> getter) => Composite(getter, b => b
      .Double((ref p) => p?.StartTime.Seconds() ?? 0)
      .Double((ref p) => p?.EndTime.Seconds() ?? 0)
      .Orbit((ref p, _) => p?.Orbit)
    );

    public B Astronomical(DeviceFieldBufGetter<V, Astronomical> getter) => Composite(getter, b => b
      .Uint((ref a) => a?.Hash ?? 0)
      .Orbit((ref a, _) => (a as IOrbiting)?.Orbit)
    );
  }
}