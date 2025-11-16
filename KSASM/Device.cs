
using System;
using System.Reflection;
using KSA;

namespace KSASM
{
  public interface IDevice : IMemory
  {
    public ulong Id { get; }
  }

  public interface IDeviceField : IMemory
  {
    public int Size { get; }
  }

  public abstract class BaseDevice : IDevice
  {
    public abstract ulong Id { get; }

    private readonly MappedMemory memory = new();

    public BaseDevice(params IDeviceField[] fields)
    {
      var addr = 0;
      foreach (var field in fields)
      {
        memory.MapRange(addr, field, 0, field.Size);
        addr += field.Size;
      }
    }

    public void Read(Span<byte> buffer, int address) => memory.Read(buffer, address);
    public void Write(Span<byte> data, int address) => memory.Write(data, address);

    protected static Value Unsigned(ulong val) => new() { Unsigned = val };
    protected static Value Signed(long val) => new() { Signed = val };
    protected static Value Float(double val) => new() { Float = val };
  }

  public abstract class BaseDeviceField : IDeviceField
  {
    private readonly byte[] buffer;
    protected readonly DataType type;

    public BaseDeviceField(DataType type)
    {
      this.buffer = new byte[type.SizeBytes()];
      this.type = type;
    }

    public int Size => buffer.Length;

    public void Read(Span<byte> buffer, int address)
    {
      Encoding.Encode(this.buffer, type, GetValue());
      this.buffer.AsSpan()[address..].CopyTo(buffer);
    }

    public void Write(Span<byte> data, int address)
    {
      Encoding.Encode(buffer, type, GetValue());
      data.CopyTo(buffer.AsSpan()[address..]);
      SetValue(Encoding.Decode(buffer, type));
    }

    protected abstract Value GetValue();
    protected abstract void SetValue(Value val);
  }

  public class LambdaDeviceField<T> : BaseDeviceField
  {
    public delegate Value GetValueDelegate(T device);
    public delegate void SetValueDelegate(T device, Value value);

    private readonly T device;
    private readonly GetValueDelegate getValue;
    private readonly SetValueDelegate setValue;

    public LambdaDeviceField(T device, DataType type, GetValueDelegate getValue, SetValueDelegate setValue) : base(type)
    {
      this.device = device;
      this.getValue = getValue;
      this.setValue = setValue;
    }

    protected override Value GetValue() => getValue(device);
    protected override void SetValue(Value val) => setValue(device, val);
  }

  public class NullDevice : IDevice
  {
    public ulong Id => 0;
    public void Read(Span<byte> buffer, int address) => buffer.Clear();
    public void Write(Span<byte> data, int address) { }
  }

  public class VehicleDevice : BaseDevice
  {
    public override ulong Id => 2;

    public VehicleDevice(Vehicle vehicle)
    : base(AVelX(vehicle), AVelY(vehicle), AVelZ(vehicle), ThrustCommand(vehicle)) { }

    private static LambdaDeviceField<Vehicle> AVelX(Vehicle vehicle) => new(
      vehicle, DataType.F64,
      v => Float(vehicle.LastKinematicStates.BodyRates.X),
      (v, val) => { }
    );

    private static LambdaDeviceField<Vehicle> AVelY(Vehicle vehicle) => new(
      vehicle, DataType.F64,
      v => Float(vehicle.LastKinematicStates.BodyRates.Y),
      (v, val) => { }
    );

    private static LambdaDeviceField<Vehicle> AVelZ(Vehicle vehicle) => new(
      vehicle, DataType.F64,
      v => Float(vehicle.LastKinematicStates.BodyRates.Z),
      (v, val) => { }
    );

    private static LambdaDeviceField<Vehicle> ThrustCommand(Vehicle vehicle) => new(
      vehicle, DataType.U64,
      v => Unsigned((ulong)GetInputs(v).ThrusterCommandFlags),
      (v, val) => SetInputs(vehicle, GetInputs(vehicle) with
      {
        ThrusterCommandFlags = (ThrusterMapFlags)val.Unsigned
      })
    );

    private static FieldInfo _inputsField;
    private static FieldInfo InputsField =>
      _inputsField ??= typeof(Vehicle).GetField(
        "_manualControlInputs",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static ManualControlInputs GetInputs(Vehicle vehicle) =>
      (ManualControlInputs)InputsField.GetValue(vehicle);
    private static void SetInputs(Vehicle vehicle, ManualControlInputs inputs) =>
      InputsField.SetValue(vehicle, inputs);
  }

  public class FlightComputerDevice : BaseDevice
  {
    public override ulong Id => 3;

    public FlightComputerDevice(FlightComputer fc)
    : base(ThrustMode(fc)) { }

    private static LambdaDeviceField<FlightComputer> ThrustMode(FlightComputer fc) => new(
      fc, DataType.U64,
      fc => Unsigned((ulong)fc.ManualThrustMode),
      (fc, val) => fc.SetManualThrustMode((FlightComputerManualThrustMode)val.Unsigned)
    );
  }
}