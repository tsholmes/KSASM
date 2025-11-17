
using KSA;

namespace KSASM
{
  public abstract class OrbitDeviceField<T>(int offset)
  : CompositeDeviceField<T, Orbit>(offset, Parent, Periapsis, Apoapsis, Period)
  {
    public static readonly ParentField Parent = new();
    public static readonly PeriapsisField Periapsis = new();
    public static readonly ApoapsisField Apoapsis = new();
    public static readonly PeriodField Period = new();

    public class ParentField() : ReadOnlyUintDeviceField<Orbit>(0)
    {
      protected override uint GetValue(ref Orbit parent) => parent?.Parent?.Hash ?? 0;
    }

    public class PeriapsisField() : ReadOnlyDoubleDeviceField<Orbit>(Parent.End())
    {
      protected override double GetValue(ref Orbit parent) => parent?.Periapsis ?? 0;
    }

    public class ApoapsisField() : ReadOnlyDoubleDeviceField<Orbit>(Periapsis.End())
    {
      protected override double GetValue(ref Orbit parent) => parent?.Apoapsis ?? 0;
    }

    public class PeriodField() : ReadOnlyDoubleDeviceField<Orbit>(Apoapsis.End())
    {
      protected override double GetValue(ref Orbit parent) => parent?.Period ?? 0;
    }
  }
}