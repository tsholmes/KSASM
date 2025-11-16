
using System;
using System.Diagnostics;
using KSA;

namespace KSASM
{
  public class ProcSystem
  {
    public const int STEPS_PER_FRAME = 10000;

    public readonly Vehicle Vehicle;
    public Processor Processor { get; private set; }
    public readonly DeviceHandler Devices;

    public int LastSteps { get; private set; } = 0;
    public double LastMs { get; private set; } = 0;

    private readonly Stopwatch stopwatch;

    public ProcSystem(Vehicle vehicle, Action<string> log)
    {
      this.Vehicle = vehicle;
      this.Devices = new(vehicle, log,
        new LogDevice(log),
        new VehicleDevice(vehicle));
      stopwatch = new();

      Reset();
    }

    public void Reset()
    {
      this.Processor = Processor.NewDefault();
      this.Processor.OnDevRead = Devices.OnDeviceRead;
      this.Processor.OnDevWrite = Devices.OnDeviceWrite;
      this.Processor.SleepTime = ulong.MaxValue;

      LastSteps = 0;
      LastMs = 0;
    }

    public void OnFrame(int maxSteps = STEPS_PER_FRAME)
    {
      if (Processor.SleepTime > 0)
        Processor.SleepTime--;

      if (Processor.SleepTime > 0)
      {
        LastSteps = 0;
        LastMs = 0;
        return;
      }

      var step = 0;
      stopwatch.Restart();

      for (; step < maxSteps && Processor.SleepTime == 0; step++)
        Processor.Step();

      stopwatch.Stop();
      LastSteps = step;
      LastMs = stopwatch.Elapsed.Milliseconds;
    }
  }
}