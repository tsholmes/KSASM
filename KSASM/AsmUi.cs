
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Brutal.ImGuiApi.Abstractions;
using System.Linq;

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

    [HarmonyPatch(typeof(Mod), nameof(Mod.LoadAssetBundles)), HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Mod_LoadAssetBundles_Transpile(IEnumerable<CodeInstruction> instructions)
    {
      var matcher = new CodeMatcher(instructions);
      matcher.MatchStartForward(CodeMatch.Calls(() => XmlLoader.Load<AssetBundle>("", null)));
      matcher.ThrowIfInvalid("Could not find LoadAssetBundles transpile point");

      matcher.RemoveInstruction();
      matcher.Insert(CodeInstruction.Call(() => XmlMergeLoader.Load("", null)));

      return matcher.Instructions();
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
      for (var i = 0; i < 256; i++)
      {
        var c = (char)i;
        if (c >= 'a' && c <= 'z')
          c -= (char)('a' - 'A');
        if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '/' || c == '_' || c == ' ')
        {
          // valid
        }
        else
          c = ' ';
        charStrings[i] = ((char)i).ToString();
      }

      new Terminal(terminalLabels, charStrings).Update();
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
    private static readonly string[] charStrings = new string[256];

    public static bool DrawUi(Vehicle vehicle, Viewport inViewport, Astronomical.UiContext uiContext)
    {
      if (vehicle != KSA.Program.ControlledVehicle)
        return false;

      if (!ImGui.Begin($"KSASM##KSASM-{vehicle.Id}", WINDOW_FLAGS))
      {
        ImGui.End();
        return false;
      }

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

      Step(vehicle);

      if (ImGui.Button("Assemble##asm"))
        Assemble();

      ImGui.SameLine();

      if (ImGui.Button("Run##run"))
        Restart();

      ImGui.SameLine();

      doStep = ImGui.Button("Step##step");

      ImGui.SameLine();

      if (ImGui.Button("Stop##stop"))
        Stop();

      ImGui.SameLine(); ImGui.Text(stats);

      foreach (var line in output)
        ImGui.Text(line);

      if ((output.Count > 0 || stats.Length > 0) && ImGui.Button("Clear##clear"))
      {
        stats = "";
        output.Clear();
      }

      ImGui.End();
      return false;
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
        Current = new(vehicle, AddOutput, new(terminalLabels, charStrings));

      var maxSteps = ProcSystem.STEPS_PER_FRAME;
      if (doStep)
      {
        Current.Processor.SleepTime = 0;
        maxSteps = 1;
      }

      try
      {
        Current.OnFrame(maxSteps);
        stats = $"@{Current.Processor.PC} {Current.LastSteps} steps in {Current.LastMs:0.##}ms";
      }
      catch (Exception ex)
      {
        AddOutput(ex.ToString());
      }

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

        Assembler.Assemble(new("script", source), Current.Processor.Memory);

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
    public uint4 PackedText = default;
    public override uint4 PackData0() => PackedText;
  }
}