
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Brutal.Numerics;
using KSA;
using KSASM.Assembly;

namespace KSASM
{
  public class ProcSystem
  {
    public const int STEPS_PER_FRAME = 10000;

    public readonly Vehicle Vehicle;
    public Processor Processor { get; private set; }
    public DebugSymbols Symbols;
    public TypeMemory TypeMem;
    public readonly Terminal Terminal;
    private readonly Action<string> log;

    public int LastSteps { get; private set; } = 0;
    public double LastMs { get; private set; } = 0;

    private readonly Stopwatch stopwatch;

    public ProcSystem(Vehicle vehicle, Action<string> log, Terminal terminal)
    {
      this.Vehicle = vehicle;
      this.log = log;
      this.Terminal = terminal;
      stopwatch = new();

      Reset();
    }

    public void Reset()
    {
      this.Processor = new(
        SystemDeviceDefinition.Make("system", Vehicle.System),
        VehicleDeviceDefinition.Make("vehicle", Vehicle),
        FlightComputerDeviceDefinition.Make("fc", Vehicle))
      {
        OnDebug = OnDebug,
        OnDebugStr = OnDebugStr,
        SleepTime = ulong.MaxValue
      };

      LastSteps = 0;
      LastMs = 0;

      Symbols = null;
      TypeMem = new();
      Processor.Memory.OnWrite = TypeMem.Write;
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

      try
      {
        for (; step < maxSteps && Processor.SleepTime == 0; step++)
          Processor.Step();
      }
      finally
      {
        stopwatch.Stop();
        LastSteps = step;
        LastMs = stopwatch.Elapsed.TotalMilliseconds;
      }
    }

    private void OnDebug(ValArray A) => log?.Invoke($"> {A}");
    private void OnDebugStr(string str) => log?.Invoke($"> {str}");
  }

  public class Terminal(List<TerminalLabel> labels)
  {
    public static uint4[] CharCodes { get; } = GenCharCodes();

    public const int X_CHARS = 32;
    public const int Y_CHARS = 16;
    public const int TOTAL_SIZE = X_CHARS * Y_CHARS;

    public readonly byte[] Data = new byte[TOTAL_SIZE];

    public void Update()
    {
      for (var i = 0; i < TOTAL_SIZE; i++)
        labels[i].PackedText = CharCodes[Data[i]];
    }

    private static uint4[] GenCharCodes()
    {
      var codes = new uint4[256];
      Array.Fill(codes, new(0xFFFFFFu, 0xFFFFFFu, 0xFFFFFFu, 0xFFFFFFu));
      for (var c = '0'; c <= '9'; c++)
        codes[c].X = (uint)(c - '0');
      for (var c = 'A'; c <= 'Z'; c++)
        codes[c].X = codes[c + 'a' - 'A'].X = (uint)(c - 'A' + 10);
      codes['-'].X = 36;
      codes['.'].X = 37;
      codes['/'].X = 38;
      codes['_'].X = codes[' '].X = 39;
      for (var i = 0; i < 256; i++)
        codes[i].X |= 0xFFFFC0u;
      return codes;
    }
  }

  public struct MemRange : IRange<MemRange>
  {
    public int Start;
    public DataType Type;
    public int Width;

    public int Offset;
    public int Length;

    int IRange<MemRange>.Offset => Offset;
    int IRange<MemRange>.Length => Length;

    public MemRange Slice(int offset, int length) => new()
    {
      Start = Start,
      Type = Type,
      Width = Width,

      Offset = offset,
      Length = length
    };

    public bool TryMerge(MemRange next, out MemRange merged)
    {
      var end = Offset + Length;
      var nend = next.Offset + next.Length;
      if (next.Start != Start || next.Type != Type || next.Width != Width || end != next.Offset)
      {
        merged = default;
        return false;
      }
      merged = new()
      {
        Start = Start,
        Type = Type,
        Width = Width,
        Offset = Offset,
        Length = nend - Offset,
      };
      return true;
    }
  }
}