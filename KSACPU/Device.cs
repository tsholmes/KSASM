
using System;
using System.Collections.Generic;
using System.Reflection;
using KSA;

namespace KSACPU
{
  public interface IDevice
  {
    public ulong Id { get; }
    public (ValueMode, Value) Read(ulong addr);
    public void Write(ValArray data);

    protected static (ValueMode, Value) Unsigned(ulong val) => (ValueMode.Unsigned, new() { Unsigned = val });
    protected static (ValueMode, Value) Signed(long val) => (ValueMode.Signed, new() { Signed = val });
    protected static (ValueMode, Value) Float(double val) => (ValueMode.Float, new() { Float = val });
  }

  public abstract class BaseDevice : IDevice
  {
    protected static (ValueMode, Value) Unsigned(ulong val) => (ValueMode.Unsigned, new() { Unsigned = val });
    protected static (ValueMode, Value) Signed(long val) => (ValueMode.Signed, new() { Signed = val });
    protected static (ValueMode, Value) Float(double val) => (ValueMode.Float, new() { Float = val });

    public abstract ulong Id { get; }
    public abstract (ValueMode, Value) Read(ulong addr);
    public abstract void Write(ValArray data);
  }

  public class DeviceHandler
  {
    public static bool Debug = false;

    public readonly Vehicle Vehicle;
    private readonly Action<string> log;
    private readonly Dictionary<ulong, IDevice> devices = [];

    public DeviceHandler(Vehicle vehicle, Action<string> log, params IDevice[] devices)
    {
      this.Vehicle = vehicle;
      this.log = log;

      foreach (var device in devices)
        this.devices[device.Id] = device;
    }

    public void OnDeviceWrite(ulong devId, ValArray data)
    {
      if (devices.TryGetValue(devId, out var device))
        device.Write(data);
      else
        log.Invoke($"write to unknown device ({devId}): {data}");
    }

    public void OnDeviceRead(ulong devId, ValArray data)
    {
      if (devices.TryGetValue(devId, out var device))
      {
        for (var i = 0; i < data.Width; i++)
        {
          var addr = data.Values[i];
          addr.Convert(data.Mode, ValueMode.Unsigned);
          var (mode, val) = device.Read(addr.Unsigned);
          if (Debug)
            log($"READ> {devId} @{addr.Unsigned}: {mode} {val.Get(mode)}");
          val.Convert(mode, data.Mode);
          data.Values[i] = val;
        }
      }
      else
        log.Invoke($"read from unknown device ({devId}): {data}");
    }
  }

  public class VehicleDevice : BaseDevice
  {
    public const ulong DEVICE_ID = 2;

    public enum Addr : ulong
    {
      AVelX,
      AVelY,
      AVelZ,
      ThrustMode,
      ThrustCommand,
    }

    private readonly Vehicle vehicle;

    public VehicleDevice(Vehicle vehicle)
    {
      this.vehicle = vehicle;
    }

    public override ulong Id => DEVICE_ID;

    public override (ValueMode, Value) Read(ulong addr) => (Addr)addr switch
    {
      Addr.AVelX => Float(vehicle.LastKinematicStates.BodyRates.X),
      Addr.AVelY => Float(vehicle.LastKinematicStates.BodyRates.Y),
      Addr.AVelZ => Float(vehicle.LastKinematicStates.BodyRates.Z),
      Addr.ThrustMode => Unsigned((ulong)vehicle.FlightComputer.ManualThrustMode),
      Addr.ThrustCommand => Unsigned((ulong)GetInputs().ThrusterCommandFlags),
      _ => default,
    };

    public override void Write(ValArray data)
    {
      switch ((Addr)data.SignedAt(0))
      {
        case Addr.ThrustMode:
          vehicle.FlightComputer.ManualThrustMode = (FlightComputerManualThrustMode)(data.UnsignedAt(1) & 1);
          break;
        case Addr.ThrustCommand:
          SetInputs(GetInputs() with { ThrusterCommandFlags = (ThrusterMapFlags)data.UnsignedAt(0) });
          break;
      }
    }

    private static FieldInfo _inputsField;
    private static FieldInfo InputsField =>
      _inputsField ??= typeof(Vehicle).GetField(
        "_manualControlInputs",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private ManualControlInputs GetInputs() => (ManualControlInputs)InputsField.GetValue(vehicle);
    private void SetInputs(ManualControlInputs inputs) => InputsField.SetValue(vehicle, inputs);
  }
}