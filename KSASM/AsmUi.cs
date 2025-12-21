
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using Brutal.ImGuiApi.Abstractions;
using System.Linq;
using System.Text;

using Internal = Brutal.ImGuiApi.Internal;
using IImGui = Brutal.ImGuiApi.Internal.ImGui;

namespace KSASM
{
  [HarmonyPatch]
  public static partial class AsmUi
  {
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.OnDrawUi)), HarmonyPrefix]
    public static void Vehicle_OnDrawUi_Postfix(ref bool __result, Vehicle __instance, Viewport inViewport)
    {
      __result |= AsmStep(__instance, inViewport);
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

    private static object _nextWindowClass = null;
    [HarmonyPatch(typeof(ImGui), nameof(ImGui.SetNextWindowClass)), HarmonyPostfix]
    public static void ImGui_SetNextWindowClass_Postfix(ImGuiWindowClassPtr windowClass)
    {
      _nextWindowClass = windowClass;
    }

    [HarmonyPatch(typeof(ImGui), nameof(ImGui.Begin))]
    [HarmonyPatch(
      [typeof(ImString), typeof(bool), typeof(ImGuiWindowFlags)],
      [ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal])]
    [HarmonyPrefix]
    public static void ImGui_Begin_Prefix(ImString name, ref bool pOpen, ref ImGuiWindowFlags flags)
    {
      if (_nextWindowClass is not ImGuiWindowClassPtr wclass || wclass.DockingAllowUnclassed)
        flags |= ImGuiWindowFlags.NoDocking;
      _nextWindowClass = null;
    }

    [HarmonyPatch(typeof(ImGui), nameof(ImGui.Begin), typeof(ImString), typeof(ImGuiWindowFlags))]
    [HarmonyPrefix]
    public static void ImGui_Begin_Prefix(ImString name, ref ImGuiWindowFlags flags)
    {
      if (_nextWindowClass is not ImGuiWindowClassPtr wclass || wclass.DockingAllowUnclassed)
        flags |= ImGuiWindowFlags.NoDocking;
      _nextWindowClass = null;
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
      ImGuiWindowFlags.NoScrollbar |
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

    private static ImColor8 PCHighlight => new(128, 16, 16);
    private static ImColor8 TokenHighlight => new(64, 64, 64);
    private static ImColor8 TokenHoverHilight => new(128, 128, 128);

    public static bool AsmStep(Vehicle vehicle, Viewport inViewport)
    {
      if (vehicle != KSA.Program.ControlledVehicle)
        return false;
      Step(vehicle);

      // disable read debug during UI draw so it doesn't spam
      var prevDebug = MemoryAccessor.DebugRead;
      MemoryAccessor.DebugRead = false;

      var res = DrawUi(vehicle, inViewport);

      MemoryAccessor.DebugRead = prevDebug;

      return res;
    }

    private static bool DrawUi(Vehicle vehicle, Viewport inViewport)
    {

      isTyping = false;

      ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;

      if (!enabled)
        return false;

      var initPos = ScreenReference.UvToPixels(new(0.17f, 0.08f)) + ImGui.GetMainViewport().Pos;
      ImGui.SetNextWindowSizeConstraints(new(300, 300), new(1e10f, 1e10f));
      ImGui.SetNextWindowSize(new(525, 800), ImGuiCond.Appearing);
      ImGui.SetNextWindowPos(initPos, ImGuiCond.Appearing);
      ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(0, 0));
      var hidden = !ImGui.Begin($"KSAMM##{vehicle.Id}", ref enabled, WINDOW_FLAGS) || ImGui.IsWindowCollapsed();
      ImGui.PopStyleVar();
      if (hidden)
      {
        ImGui.End();
        return false;
      }

      var dockID = ImGuiX.GetID("KSASM##", vehicle.Id, "-dock");

      if (IImGui.DockBuilderGetNode(dockID).IsNull())
      {
        IImGui.DockBuilderAddNode(dockID);
        IImGui.DockBuilderSplitNode(dockID, ImGuiDir.Down, 0.3f, out var downID, out var upID);
        IImGui.DockBuilderGetNode(downID).LocalFlags |= ImGuiDockNodeFlags.AutoHideTabBar;
        IImGui.DockBuilderDockWindow($"Editor##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"MacroView##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"InstView##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"MemView##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"StackView##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"MemWatch##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"DevView##{vehicle.Id}", upID);
        IImGui.DockBuilderDockWindow($"Controls##{vehicle.Id}", downID);
        IImGui.DockBuilderFinish(dockID);
      }

      var windowClass = new ImGuiWindowClass
      {
        ClassId = dockID,
        DockingAllowUnclassed = false,
      };

      ImGuiX.DockSpace(dockID, windowClass: windowClass);

      ImGui.End();

      void drawWindow(string title, Action draw)
      {
        ImGuiX.SetNextWindowClass(windowClass);
        var visible = ImGui.Begin($"{title}##{vehicle.Id}");

        if (IImGui.GetWindowDockNode().RootNode() != dockID &&
          !(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && ImGui.IsItemActive()))
        {
          ImGui.End();
          ImGui.SetNextWindowDockID(dockID);
          visible = ImGui.Begin($"{title}##{vehicle.Id}");
        }
        if (visible)
          draw();
        ImGui.End();
      }

      drawWindow("Editor", DrawEditor);
      drawWindow("MacroView", DrawMacroView);
      drawWindow("InstView", DrawInstView);
      drawWindow("MemView", DrawMemView);
      drawWindow("StackView", DrawStackView);
      drawWindow("MemWatch", DrawMemWatch);
      drawWindow("DevView", DrawDevView);
      drawWindow("Controls", DrawControls);

      isTyping = ImGui.GetIO().WantTextInput;
      return false;
    }

    private static void DrawControls()
    {
      ImGui.PushStyleVarX(ImGuiStyleVar.ItemSpacing, 1);
      if (ImGui.Button("Run")) Restart();
      ImGui.SameLine();
      if (ImGui.Button("Resume")) Resume();
      ImGui.SameLine();
      doStep = ImGui.Button("Step");
      ImGui.SameLine();
      if (ImGui.Button("Stop")) Stop();
      ImGui.SameLine();
      if ((output.Count > 0 || stats.Length > 0) && ImGui.Button("Clear"))
      {
        stats = "";
        output.Clear();
      }
      ImGui.PopStyleVar();
      ImGui.SameLine();
      ImGui.Text(stats);

      ImGui.Separator();
      ImGui.BeginChild("##logs", new(-float.Epsilon, -float.Epsilon), windowFlags: ImGuiWindowFlags.HorizontalScrollbar);
      foreach (var line in output)
        ImGui.Text(line);

      if (logged)
        ImGui.SetScrollHereY();
      logged = false;

      ImGui.EndChild();
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

    public static ImGuiID RootNode(this Internal.ImGuiDockNodePtr node)
    {
      if (node.IsNull())
        return 0;
      if (node.ParentNode.IsNull())
        return node.ID;
      return node.ParentNode.RootNode();
    }
  }
}