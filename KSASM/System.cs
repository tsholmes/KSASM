
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
    private readonly Action<string> log;

    public int LastSteps { get; private set; } = 0;
    public double LastMs { get; private set; } = 0;

    private readonly Stopwatch stopwatch;

    public ProcSystem(Vehicle vehicle, Action<string> log)
    {
      this.Vehicle = vehicle;
      this.log = log;
      stopwatch = new();

      Reset();
    }

    public void Reset()
    {
      this.Processor = new(
        SystemDeviceDefinition.Make(Vehicle.System),
        VehicleDeviceDefinition.Make(Vehicle),
        FlightComputerDeviceDefinition.Make(Vehicle))
      {
        OnDebug = OnDebug,
        SleepTime = ulong.MaxValue
      };

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

    private void OnDebug(ValArray A, ValArray B) => log?.Invoke($"> {A} {B}");
  }
}