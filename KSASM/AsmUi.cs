
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using System.Diagnostics;

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

    private const ImGuiWindowFlags WINDOW_FLAGS =
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoScrollbar |
      ImGuiWindowFlags.NoCollapse |
      ImGuiWindowFlags.AlwaysAutoResize |
      ImGuiWindowFlags.NoSavedSettings;

    private static readonly byte[] buffer = new byte[65536];
    private static string stats = "";
    private static readonly List<string> output = [];

    private static bool doStep = false;

    private static ProcSystem Current;

    public static bool DrawUi(Vehicle vehicle, Viewport inViewport, Astronomical.UiContext uiContext)
    {
      if (vehicle != KSA.Program.ControlledVehicle)
        return false;

      if (!ImGui.Begin($"KSASM##KSASM-{vehicle.Id}", WINDOW_FLAGS))
        return false;

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
        Current = new(vehicle, AddOutput);

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
}