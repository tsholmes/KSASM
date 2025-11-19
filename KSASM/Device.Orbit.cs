
using KSA;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Orbit(DeviceFieldBufGetter<V, Orbit> getter) => NonNull(getter, b => b
      .Hash((ref o) => o.Parent)
      .Double((ref o) => o.Periapsis)
      .Double((ref o) => o.Apoapsis)
      .Double((ref o) => o.Period)
    );

    public B Patch(DeviceFieldBufGetter<V, PatchedConic> getter) => NonNull(getter, b => b
      .Double((ref p) => p.StartTime.Seconds())
      .Double((ref p) => p.EndTime.Seconds())
      .Orbit((ref p, _) => p.Orbit)
    );

    public B FlightPlan(DeviceFieldBufGetter<V, FlightPlan> getter) => Composite(getter, b => b
      .List((ref v) => v.Patches, (b, get) => b.Patch(get))
    );

    public B Astronomical(DeviceFieldBufGetter<V, Astronomical> getter) => Composite(getter, b => b
      .Uint((ref a) => a?.Hash ?? 0)
      .Orbit((ref a, _) => (a as IOrbiting)?.Orbit)
    );

    public B Burn(
      DeviceFieldBufGetter<V, Burn> getter,
      DeviceFieldSetter<V, Burn> setter = null
    ) => ValueComposite(getter, setter, b => b
      .Double((ref b) => b.Time.Seconds(), (ref b, v) => b.Time = new(v))
      .Double3((ref b, _) => b.DeltaVVlf, (ref b, v) => b.DeltaVVlf = v)
      .FlightPlan((ref b, _) => b.FlightPlan)
      .Bool((ref b) => false, (ref b, del) => { if (del) b.Vehicle.FlightComputer.RemoveBurn(b); })
    );
  }
}