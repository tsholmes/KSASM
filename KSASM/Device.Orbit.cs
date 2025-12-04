
using System;
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Orbit(DeviceFieldBufGetter<V, Orbit> getter) => NonNull(getter, b => b
      .Hash((ref o) => o.Parent)
      .Double((ref o) => o.TimeAtPeriapsis.Seconds())
      .Double((ref o) => o.Eccentricity)
      .Double((ref o) => o.SemiMajorAxis)
      .Double((ref o) => o.SemiMinorAxis)
      .Double((ref o) => o.Inclination)
      .Double((ref o) => o.LongitudeOfAscendingNode)
      .Double((ref o) => o.ArgumentOfPeriapsis)
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

    public B Astronomical(DeviceFieldBufGetter<V, Astronomical> getter) => NonNull(getter, b => b
      .Uint((ref a) => a.Hash)
      .Double((ref a) => a.MeanRadius)
      .Double((ref a) => a.AngularVelocity)
      .Double((ref a) => a is Vehicle v ? v.TotalMass : a.Mass)
      .Double((ref a) => a.IsStar() ? double.PositiveInfinity : a.SphereOfInfluence)
      .Frame(FrameDef.Ecl)
      .Frame(FrameDef.Cci)
      .Frame(FrameDef.Cce)
      .Frame(FrameDef.Orb)
      .DoubleQuat((ref a, _) => a.GetOrb2Cci())
      .DoubleQuat((ref a, _) => a.GetCci2Cce())
      .Orbit((ref a, _) => (a as IOrbiting)?.Orbit)
      .List((ref a) => a.Children, (b, get) => b.ChildHash(new(get)))
      .String(32, (ref a, _) => a.Id)
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

  public class FrameDef(Func<Astronomical, double3> pos, Func<Astronomical, double3> vel)
  {
    public readonly Func<Astronomical, double3> Pos = pos;
    public readonly Func<Astronomical, double3> Vel = vel;

    public static readonly FrameDef Ecl = new(a => a.GetPositionEcl(), a => a.GetVelocityEcl());
    public static readonly FrameDef Cci = new(a => a.GetPositionCci(), a => a.GetVelocityCci());
    public static readonly FrameDef Cce = new(a => a.GetPositionCce(), a => a.GetVelocityCce());
    public static readonly FrameDef Orb = new(a => a.GetPositionOrb(), a => a.GetVelocityOrb());
  }

  public static partial class Extensions
  {
    public static B Frame<B, T, V>(this DeviceFieldBuilder<B, T, V> b, FrameDef frame)
      where B : DeviceFieldBuilder<B, T, V>
      where V : Astronomical => b
        .Double3((ref a, _) => frame.Pos(a))
        .Double3((ref a, _) => frame.Vel(a));
  }
}