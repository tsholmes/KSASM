
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

    public B FlightPlan(DeviceFieldBufGetter<V, FlightPlan> getter) => Composite(getter, b => b
      .ListView(
        v => v.Patches.Count,
        b => b.Patch((ref v, _) => v.Parent.Patches[(int)v.Index]))
    );

    public B Astronomical(DeviceFieldBufGetter<V, Astronomical> getter) => Composite(getter, b => b
      .Uint((ref a) => a?.Hash ?? 0)
      .Orbit((ref a, _) => (a as IOrbiting)?.Orbit)
    );

    public B Burn(DeviceFieldBufGetter<V, Burn> getter) => Composite(getter, b => b
      .Double((ref b) => b.Time.Seconds())
      .Double3((ref b, _) => b.DeltaVVlf)
      .FlightPlan((ref b, _) => b.FlightPlan)
    );
  }
}