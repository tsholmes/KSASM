
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Brutal.ImGuiApi;
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
    public readonly LogBuffer Logs = new(65536, 100);

    public int LastSteps { get; private set; } = 0;
    public double LastMs { get; private set; } = 0;

    public bool SingleStep { get; set; }

    public string Id => Vehicle.Id;

    private readonly Stopwatch stopwatch;

    public ProcSystem(Vehicle vehicle, Action<string> log, Terminal terminal)
    {
      this.Vehicle = vehicle;
      this.Terminal = terminal;
      stopwatch = new();

      Reset();
    }

    public void Reset()
    {
      this.Processor = new(
        SystemDeviceDefinition.Make("system", new() { System = Vehicle.System, Terminal = Terminal }),
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
      Logs.Clear();
    }

    public void Assemble(string source)
    {
      Reset();
      var stopwatch = Stopwatch.StartNew();
      try
      {
        Symbols = Assembler.Assemble(new("script", source), Processor.Memory);

        stopwatch.Stop();
        Logs.Log($"Assembled in {stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
      }
      catch (Exception ex)
      {
        Logs.Log(ex.ToString());
      }
    }

    public void Restart()
    {
      Processor.PC = Processor.SP = Processor.FP = 0;
      Processor.SleepTime = 0;
    }

    public void Resume()
    {
      Processor.SleepTime = 0;
    }

    public void Stop()
    {
      Processor.SleepTime = ulong.MaxValue;
    }

    public void OnFrame(int maxSteps = STEPS_PER_FRAME)
    {
      if (SingleStep)
      {
        maxSteps = 1;
        Processor.SleepTime = 0;
      }

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

        if (SingleStep)
        {
          Processor.SleepTime = ulong.MaxValue;
          SingleStep = false;
        }
      }
    }

    private void OnDebug(ValArray A) => Logs.Log($"> {A}");
    private void OnDebugStr(string str) => Logs.Log($"> {str}");
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

  public class LogBuffer(int bufSize, int maxLines)
  {
    private readonly char[] data = new char[bufSize];
    private int dataCursor = 0;

    private readonly FixedRange[] lines = new FixedRange[maxLines];
    private int lineStart = 0;
    private int lineCount = 0;
    private int totalLogs = 0;

    public int Total => totalLogs;
    public int Count => lineCount;

    public void Clear() => dataCursor = lineStart = lineCount = 0;

    public void Log(ReadOnlySpan<char> log)
    {
      while (log.IndexOf('\n') is int idx && idx >= 0)
      {
        AddLine(log[..idx]);
        log = log[(idx + 1)..];
      }
      if (log.Length > 0)
        AddLine(log);
    }

    public void AddLine(ReadOnlySpan<char> line)
    {
      var range = new FixedRange(dataCursor, line.Length);
      if (range.End > data.Length)
        range = new(0, line.Length);
      while (lineCount > 0)
      {
        if (range.Start == 0 && lines[lineStart].Start >= dataCursor)
        {
          lineCount--;
          lineStart = (lineStart + 1) % data.Length;
          continue;
        }
        if (lines[lineStart].End <= range.End)
          break;
        lineCount--;
        lineStart = (lineStart + 1) % data.Length;
      }
      var idx = (lineStart + lineCount) % lines.Length;
      lines[idx] = range;
      line.CopyTo(data.AsSpan(range.Start, range.Length));
      dataCursor = range.End;
      lineCount++;
      totalLogs++;
    }

    public ReadOnlySpan<char> this[int index]
    {
      get
      {
        if (index < 0 || index >= lineCount)
          throw new IndexOutOfRangeException($"{index}");
        var range = lines[(lineStart + index) % lines.Length];
        return data.AsSpan(range.Start, range.Length);
      }
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator(LogBuffer buf)
    {
      private readonly LogBuffer buf = buf;
      private int index = -1;
      public ReadOnlySpan<char> Current => buf[index];
      public bool MoveNext() => ++index < buf.lineCount;
    }
  }

  public class TerminalLabel : GaugeLabelReference
  {
    public static List<TerminalLabel> Labels = [];

    public uint4 PackedText = Terminal.CharCodes[0];
    public override uint4 PackData0() => PackedText;
  }

  public static partial class Extensions
  {
    public static bool Contains(this in floatRect r, float2 p) =>
      p.X >= r.Min.X && p.X < r.Max.X && p.Y >= r.Min.Y && p.Y < r.Max.Y;

    public static ImGuiID RootNode(this Brutal.ImGuiApi.Internal.ImGuiDockNodePtr node)
    {
      if (node.IsNull())
        return 0;
      if (node.ParentNode.IsNull())
        return node.ID;
      return node.ParentNode.RootNode();
    }
  }
}