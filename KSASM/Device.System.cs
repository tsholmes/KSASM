
using System;
using KSA;

namespace KSASM
{
  public class SystemDeviceDefinition : DeviceDefinition<CelestialSystem, SystemDeviceDefinition>
  {
    public override ulong GetId(CelestialSystem device) => 1;

    public override IDeviceField<CelestialSystem> RootField { get; } =
      new RootDeviceField<CelestialSystem>(SystemQuery);

    public static readonly SystemQueryField SystemQuery = new();

    public class SystemQueryField()
    : CompositeDeviceField<CelestialSystem, SystemQueryView>(0, HashParam, Hash, Orbit)
    {
      protected override SystemQueryView GetValue(ref CelestialSystem parent, Span<byte> deviceBuf)
      {
        var hash = HashParam.GetValue(deviceBuf);
        var current = hash == 0 ? parent.GetWorldSun() : parent.Get(hash);
        return new()
        {
          System = parent,
          Current = current,
        };
      }

      public static ParamDeviceField<SystemQueryView, uint> HashParam =
        new(DataType.U64, 0, UintValueConverter.Instance);
      public static readonly HashField Hash = new();
      public static readonly OrbitField Orbit = new();

      public class HashField() : UintDeviceField<SystemQueryView>(HashParam.End())
      {
        protected override uint GetValue(ref SystemQueryView parent) => parent.Current?.Hash ?? 0;
        protected override void SetValue(ref SystemQueryView parent, uint value) { }
      }

      public class OrbitField() : OrbitDeviceField<SystemQueryView>(Hash.End())
      {
        protected override Orbit GetValue(ref SystemQueryView parent, Span<byte> deviceBuf) =>
          (parent.Current as IOrbiting)?.Orbit;
      }
    }

    public struct SystemQueryView
    {
      public CelestialSystem System;
      public Astronomical Current;
    }
  }
}