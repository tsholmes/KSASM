
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Brutal.ImGuiApi.Abstractions;
using System.Linq;
using KSASM.Assembly;
using System.Text;

namespace KSASM
{
  [HarmonyPatch]
  public static class AsmUi
  {
    [HarmonyPatch(typeof(FlightComputer), nameof(FlightComputer.DrawUi)), HarmonyPostfix]
    public static void FlightComputer_DrawUi_Postfix(
      ref bool __result,
      Viewport inViewport,
      Astronomical.UiContext uiContext)
    {
      __result |= DrawUi(uiContext.Astronomical as Vehicle, inViewport, uiContext);
    }

    [HarmonyPatch(typeof(ConsoleWindowEx), nameof(ConsoleWindowEx.OnKey)), HarmonyPrefix]
    public static bool ConsoleWindowEx_OnKey_Prefix(ref bool __result, ConsoleWindow console)
    {
      if (!console.IsOpen && isTyping)
      {
        __result = true;
        return false;
      }
      return true;
    }

    [HarmonyPatch(typeof(ModLibrary), nameof(ModLibrary.LoadAll)), HarmonyPostfix]
    public static void ModLibrary_LoadAll_Suffix()
    {
      terminalCanvas = ModLibrary.Get<GaugeCanvas>("KSASMTerminal");
      var labelsComp = terminalCanvas.Components.First(c => c.Name == "Labels");

      const float EDGE_PAD = 0.025f;
      const float SCALE = 0.9f;

      static float pos(int i, int len) => EDGE_PAD + (1 - 2 * EDGE_PAD) * ((float)i / (float)len);

      for (var cy = 0; cy < Terminal.Y_CHARS; cy++)
      {
        var y0 = pos(cy, Terminal.Y_CHARS);
        var y1 = pos(cy + 1, Terminal.Y_CHARS);
        for (var cx = 0; cx < Terminal.X_CHARS; cx++)
        {
          var x0 = pos(cx, Terminal.X_CHARS);
          var x1 = pos(cx + 1, Terminal.X_CHARS);

          var label = new TerminalLabel
          {
            X = x0,
            Y = y0,
            Width = x1 - x0,
            Height = y1 - y0,
            TextColor = IndexedColor.White,
            BackgroundColor = IndexedColor.DarkGrey,
            TextScale = SCALE,
            Label = " "
          };
          labelsComp.Rects.Add(label);
          terminalLabels.Add(label);
        }
      }

      labelsComp.OnDataLoad(labelsComp.Mod);
    }

    private const ImGuiWindowFlags WINDOW_FLAGS =
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoScrollbar |
      ImGuiWindowFlags.NoCollapse |
      ImGuiWindowFlags.AlwaysAutoResize |
      ImGuiWindowFlags.NoSavedSettings;

    private static readonly byte[] buffer = new byte[65536];
    private static string stats = "";
    private static readonly List<string> output = [];

    private static bool isTyping = false;
    private static bool doStep = false;

    public static ProcSystem Current { get; private set; }

    private static GaugeCanvas terminalCanvas;
    private static readonly List<TerminalLabel> terminalLabels = [];

    public static bool DrawUi(Vehicle vehicle, Viewport inViewport, Astronomical.UiContext uiContext)
    {
      if (vehicle != KSA.Program.ControlledVehicle)
        return false;

      isTyping = false;
      Step(vehicle);

      if (!ImGui.Begin($"KSASM##KSASM-{vehicle.Id}", WINDOW_FLAGS))
      {
        ImGui.End();
        return false;
      }

      if (ImGui.BeginTabBar("##tabs"))
      {
        if (ImGui.BeginTabItem("Editor##editor"))
        {
          DrawEditor();
          ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Debug##debug"))
        {
          DrawDebug();
          ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
      }

      DrawControls();

      ImGui.End();
      return false;
    }

    private static void DrawEditor()
    {
      if (ImGui.BeginCombo("##Library", "Load Library Script"))
      {
        for (var i = 0; i < Library.Index.Count; i++)
        {
          var name = Library.Index[i];
          ImGui.PushID(i);
          if (ImGui.Selectable(name))
            LoadLibrary(name);
          ImGui.PopID();
        }
        ImGui.EndCombo();
      }

      ImGui.InputTextMultiline(
        "###source",
        buffer.AsSpan(),
        new float2(600, 400),
        ImGuiInputTextFlags.None
      );
      isTyping = ImGui.IsItemActive();

      if (ImGui.Button("Assemble##asm"))
        Assemble();
    }

    private const int VALS_PER_LINE = 16;
    private const int VAL_LINES = 16;
    private const int TOTAL_VALS = VALS_PER_LINE * VAL_LINES;
    private static int debugAddress = 0;
    private static bool debugPC = true;
    private static void ScrollToAddr(int addr, int align = 1)
    {
      debugAddress -= debugAddress % align;
      debugAddress += addr % align;

      if (addr < debugAddress || addr >= debugAddress + TOTAL_VALS)
        debugAddress = addr - VALS_PER_LINE * 3;
      else if (debugAddress > addr - VALS_PER_LINE * 3)
        debugAddress -= (debugAddress - (addr - VALS_PER_LINE * 3) + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;
      else if (addr - TOTAL_VALS + VALS_PER_LINE * 4 > debugAddress)
        debugAddress += ((addr - TOTAL_VALS + VALS_PER_LINE * 4) - debugAddress + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;

      if (debugAddress < 0)
        debugAddress = 0;
      if (debugAddress > Processor.MAIN_MEM_SIZE - TOTAL_VALS)
      {
        debugAddress = Processor.MAIN_MEM_SIZE - TOTAL_VALS;
        if (addr % align != 0)
          debugAddress -= VALS_PER_LINE;
      }

      debugAddress -= debugAddress % align;
      debugAddress += addr % align;
    }
    private static void DrawDebug()
    {
      if (debugPC)
        ScrollToAddr(Current.Processor.PC, 8);

      var pcoff = Current.Processor.PC - debugAddress;

      var line = new DataLineView(stackalloc char[128], stackalloc char[32], Current.Symbols);

      line.Clear();

      line.Add('@');
      line.Add(debugAddress, "X6");

      ImGui.Text(line);

      Span<AddrInfo> infos = stackalloc AddrInfo[TOTAL_VALS];
      Current.Symbols?.GetAddrInfo(debugAddress, infos);

      Span<byte> data = stackalloc byte[TOTAL_VALS];
      Current.Processor.MappedMemory.Read(data, debugAddress);

      line.Clear();
      line.Empty(6);
      for (var i = 0; i < VALS_PER_LINE; i++)
      {
        line.Sp();
        line.Add(i, "X2");
      }
      ImGui.Text(line);

      var offset = 0;
      for (var lnum = 0; lnum < VAL_LINES; lnum++)
      {
        line.Clear();
        var addr = debugAddress + lnum * VALS_PER_LINE;
        line.Add(addr, "X6");
        line.Sp();
        for (var i = 0; i < VALS_PER_LINE; i++)
        {
          if (offset == pcoff)
            line.HighlightData(8, VALS_PER_LINE, new(128, 16, 16));
          line.AddData(infos[offset], data[offset..]);
          offset++;
        }
        ImGui.Text(line);
      }
    }

    private static void DrawControls()
    {
      if (ImGui.Button("Run##run"))
        Restart();

      ImGui.SameLine();

      doStep = ImGui.Button("Step##step");

      ImGui.SameLine();

      if (ImGui.Button("Stop##stop"))
        Stop();

      ImGui.Text(stats);

      foreach (var line in output)
        ImGui.Text(line);

      if ((output.Count > 0 || stats.Length > 0) && ImGui.Button("Clear##clear"))
      {
        stats = "";
        output.Clear();
      }
    }

    public static void LoadLibrary(string name)
    {
      var source = Library.LoadImport(name);
      var sbytes = System.Text.Encoding.ASCII.GetBytes(source.Source);
      sbytes.CopyTo(buffer);
      buffer[sbytes.Length] = 0;
    }

    private static void Step(Vehicle vehicle)
    {
      if (vehicle != Current?.Vehicle)
      {
        Current = new(vehicle, AddOutput, new(terminalLabels));
        Current.Terminal.Update();
      }

      var maxSteps = ProcSystem.STEPS_PER_FRAME;
      if (doStep)
      {
        Current.Processor.SleepTime = 0;
        maxSteps = 1;
      }

      try
      {
        Current.OnFrame(maxSteps);
      }
      catch (Exception ex)
      {
        AddOutput(ex.ToString());
      }

      var sb = new StringBuilder();
      var pc = Current.Processor.PC;
      if (Current.Symbols != null && Current.Symbols.InstToken(pc, out var itoken))
      {
        itoken = Current.Symbols.RootToken(itoken);
        var sname = Current.Symbols.SourceName(itoken);
        var loc = Current.Symbols.SourceLine(itoken, out var lnum, out var loff);
        var id = Current.Symbols.ID(pc);

        sb.Append('@').Append(pc).Append(": ");
        sb.Append(sname).Append(':').Append(lnum + 1).Append(':').Append(loff).Append(' ');
        sb.Append(id.Label).Append('+').Append(id.Offset).AppendLine();
        sb.Append(loc).AppendLine();
      }
      else
      {
        sb.Append('@').Append(pc).AppendLine();
      }
      sb.Append(Current.LastSteps).Append(" steps in ");
      sb.AppendFormat("{0:0.##}", Current.LastMs).Append("ms");
      stats = sb.ToString();

      if (doStep)
        Stop();

      doStep = false;
    }

    private static void Assemble()
    {
      Current.Reset();
      var stopwatch = Stopwatch.StartNew();
      try
      {
        var len = buffer.IndexOf((byte)0);
        var source = System.Text.Encoding.ASCII.GetString(buffer, 0, len);

        Current.Symbols = Assembler.Assemble(new("script", source), Current.Processor.Memory);

        stopwatch.Stop();
        AddOutput($"Assembled in {stopwatch.Elapsed.Milliseconds:0.##}ms");
      }
      catch (Exception ex)
      {
        AddOutput(ex.ToString());
      }
    }

    private static void Restart()
    {
      Current.Processor.PC = 0;
      Current.Processor.SleepTime = 0;
    }

    private static void Stop()
    {
      Current.Processor.SleepTime = ulong.MaxValue;
    }

    private static void AddOutput(string line)
    {
      output.Add(line);
      Console.WriteLine(line);
      while (output.Count > 20)
        output.RemoveAt(0);
    }
  }

  public class TerminalLabel : GaugeLabelReference
  {
    public uint4 PackedText = Terminal.CharCodes[0];
    public override uint4 PackData0() => PackedText;
  }
}