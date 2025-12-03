
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using Brutal.ImGuiApi.Abstractions;
using System.Linq;
using System.Text;

namespace KSASM
{
  [HarmonyPatch]
  public static partial class AsmUi
  {
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.OnDrawUi)), HarmonyPrefix]
    public static void Vehicle_OnDrawUi_Postfix(ref bool __result, Vehicle __instance, Viewport inViewport)
    {
      __result |= DrawUi(__instance, inViewport);
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

    [HarmonyPatch(typeof(GaugeCanvas), nameof(GaugeCanvas.OnDrawMenuBar)), HarmonyPostfix]
    public static void GaugeCanvas_OnDrawMenuBar_Postfix()
    {
      if (ImGui.MenuItem("KSASM", default, enabled))
        enabled = !enabled;
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
      ImGuiWindowFlags.AlwaysAutoResize |
      ImGuiWindowFlags.NoSavedSettings;

    private static bool enabled = true;

    private static string stats = "";
    private static readonly List<string> output = [];
    private static bool logged = false;

    private static bool isTyping = false;
    private static bool doStep = false;

    public static ProcSystem Current { get; private set; }

    private static GaugeCanvas terminalCanvas;
    private static readonly List<TerminalLabel> terminalLabels = [];

    public static bool DrawUi(Vehicle vehicle, Viewport inViewport)
    {
      if (vehicle != KSA.Program.ControlledVehicle)
        return false;

      isTyping = false;
      Step(vehicle);

      if (!enabled)
        return false;

      var line = new LineBuilder(stackalloc char[128]);
      line.Add("KSASM##");
      line.Add(vehicle.Id);

      ImGui.SetNextWindowSizeConstraints(new(600, 400), new(1e10f, 1e10f));
      ImGui.SetNextWindowPos(ScreenReference.UvToPixels(new(0.15f, 0.1f)), ImGuiCond.Appearing);
      if (!ImGui.Begin(line.Line, ref enabled, WINDOW_FLAGS) || ImGui.IsWindowCollapsed())
      {
        ImGui.End();
        return false;
      }

      if (ImGui.BeginTabBar("##tabs"))
      {
        if (ImGui.BeginTabItem("Editor"))
        {
          DrawEditor();
          ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("MacroView"))
        {
          DrawMacroView();
          ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("InstView"))
        {
          DrawInstView();
          ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("MemView"))
        {
          DrawMemView();
          ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("MemWatch"))
        {
          DrawMemWatch();
          ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
      }

      DrawControls();

      isTyping = ImGui.GetIO().WantTextInput;

      ImGui.End();
      return false;
    }

    private static void DrawControls()
    {
      if (ImGui.GetCursorPosY() < 400)
        ImGui.SetCursorPosY(400);
      if (ImGui.Button("Run")) Restart();
      ImGui.SameLine();
      if (ImGui.Button("Resume")) Resume();
      ImGui.SameLine();
      doStep = ImGui.Button("Step");
      ImGui.SameLine();
      if (ImGui.Button("Stop")) Stop();

      ImGui.Text(stats);

      if (ImGui.BeginChild("##logs", new(-float.Epsilon, 200), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
      {
        foreach (var line in output)
          ImGui.Text(line);

        if (logged)
          ImGui.SetScrollHereY();
        logged = false;

        ImGui.EndChild();
      }

      if ((output.Count > 0 || stats.Length > 0) && ImGui.Button("Clear"))
      {
        stats = "";
        output.Clear();
      }
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

    private static void Restart()
    {
      Current.Processor.PC = 0;
      Current.Processor.SleepTime = 0;
    }

    private static void Resume()
    {
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
      while (output.Count > 100)
        output.RemoveAt(0);
      logged = true;
    }
  }

  public class TerminalLabel : GaugeLabelReference
  {
    public uint4 PackedText = Terminal.CharCodes[0];
    public override uint4 PackData0() => PackedText;
  }

  public static partial class Extensions
  {
    public static bool Contains(this in floatRect r, float2 p) =>
      p.X >= r.Min.X && p.X < r.Max.X && p.Y >= r.Min.Y && p.Y < r.Max.Y;
  }
}