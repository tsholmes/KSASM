
using System;
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public partial class DeviceFieldBuilder<B, T, V>
  {
    public B Orbit(string name, DeviceFieldBufGetter<V, Orbit> getter) => NonNull(name, getter, b => b
      .Hash("parent", (ref o) => o.Parent)
      .Double("time_at_periapsis", (ref o) => o.TimeAtPeriapsis.Seconds())
      .Double("eccentricity", (ref o) => o.Eccentricity)
      .Double("semi_major_axis", (ref o) => o.SemiMajorAxis)
      .Double("semi_minor_axis", (ref o) => o.SemiMinorAxis)
      .Double("inclination", (ref o) => o.Inclination)
      .Double("longitude_of_ascending_node", (ref o) => o.LongitudeOfAscendingNode)
      .Double("argument_of_periapsis", (ref o) => o.ArgumentOfPeriapsis)
      .Double("periapsis", (ref o) => o.Periapsis)
      .Double("apoapsis", (ref o) => o.Apoapsis)
      .Double("period", (ref o) => o.Period)
    );

    public B Patch(string name, DeviceFieldBufGetter<V, PatchedConic> getter) => NonNull(name, getter, b => b
      .Double("start_time", (ref p) => p.StartTime.Seconds())
      .Double("end_time", (ref p) => p.EndTime.Seconds())
      .Orbit("orbit", (ref p, _) => p.Orbit)
    );

    public B FlightPlan(string name, DeviceFieldBufGetter<V, FlightPlan> getter) =>
      Composite(name, getter, b => b
        .List("patches", (ref v) => v.Patches, (b, get) => b.Patch("patch", get))
      );

    public B Astronomical(
      string name, DeviceFieldBufGetter<V, Astronomical> getter
    ) => NonNull(name, getter, b => b
      .Uint("hash", (ref a) => a.Hash)
      .Double("mean_radius", (ref a) => a.MeanRadius)
      .Double("angular_velocity", (ref a) => a.AngularVelocity)
      .Double("mass", (ref a) => a is Vehicle v ? v.TotalMass : a.Mass)
      .Double("soi", (ref a) => a.IsStar() ? double.PositiveInfinity : a.SphereOfInfluence)
      .Frame(FrameDef.Ecl)
      .Frame(FrameDef.Cci)
      .Frame(FrameDef.Cce)
      .Frame(FrameDef.Orb)
      .DoubleQuat("orb2cci", (ref a, _) => a.GetOrb2Cci())
      .DoubleQuat("cci2cce", (ref a, _) => a.GetCci2Cce())
      .Orbit("orbit", (ref a, _) => (a as IOrbiting)?.Orbit)
      .List("children", (ref a) => a.Children, (b, get) => b.ChildHash("child.hash", new(get)))
      .String("id", 32, (ref a, _) => a.Id)
    );

    public B Burn(
      string name,
      DeviceFieldBufGetter<V, Burn> getter,
      DeviceFieldSetter<V, Burn> setter = null
    ) => ValueComposite(name, getter, setter, b => b
      .Double("time", (ref b) => b.Time.Seconds(), (ref b, v) => b.Time = new(v))
      .Double3("deltav", (ref b, _) => b.DeltaVVlf, (ref b, v) => b.DeltaVVlf = v)
      .FlightPlan(null, (ref b, _) => b.FlightPlan)
      .Bool("delete", (ref b) => false, (ref b, del) => { if (del) b.Vehicle.FlightComputer.RemoveBurn(b); })
    );
  }

  public class FrameDef(string name, Func<Astronomical, double3> pos, Func<Astronomical, double3> vel)
  {
    public readonly string Name = name;
    public readonly Func<Astronomical, double3> Pos = pos;
    public readonly Func<Astronomical, double3> Vel = vel;

    public static readonly FrameDef Ecl = new("ecl", a => a.GetPositionEcl(), a => a.GetVelocityEcl());
    public static readonly FrameDef Cci = new("cci", a => a.GetPositionCci(), a => a.GetVelocityCci());
    public static readonly FrameDef Cce = new("cce", a => a.GetPositionCce(), a => a.GetVelocityCce());
    public static readonly FrameDef Orb = new("orb", a => a.GetPositionOrb(), a => a.GetVelocityOrb());
  }

  public static partial class Extensions
  {
    public static B Frame<B, T, V>(this DeviceFieldBuilder<B, T, V> b, FrameDef frame)
      where B : DeviceFieldBuilder<B, T, V>
      where V : Astronomical => b.Composite(frame.Name, (ref a, _) => a, b => b
        .Double3("pos", (ref a, _) => frame.Pos(a))
        .Double3("vel", (ref a, _) => frame.Vel(a))
      );
  }
}