
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace KSACPU
{
  [HarmonyPatch]
  public static class AsmUi
  {
    [HarmonyPatch(typeof(FlightComputer), nameof(FlightComputer.DrawUi)), HarmonyPostfix]
    public static void FlightComputer_DrawUi_Postfix(
      FlightComputer __instance,
      ref bool __result,
      Viewport inViewport,
      Astronomical.UiContext uiContext)
    {
      __result |= DrawUi(__instance, inViewport, uiContext);
    }

    private const ImGuiWindowFlags WINDOW_FLAGS =
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoScrollbar |
      ImGuiWindowFlags.NoCollapse |
      ImGuiWindowFlags.AlwaysAutoResize |
      ImGuiWindowFlags.NoSavedSettings;

    private static byte[] buffer = new byte[65536];
    private static string stats = "";
    private static List<string> output = [];

    public static bool DrawUi(FlightComputer fc, Viewport inViewport, Astronomical.UiContext uiContext)
    {
      if (fc != Program.ControlledVehicle?.FlightComputer)
        return false;

      if (!ImGui.Begin($"KSASM##KSASM-{Program.ControlledVehicle.Id}", WINDOW_FLAGS))
        return false;

      ImGui.InputTextMultiline(
        "###source",
        buffer.AsSpan(),
        new float2(600, 400),
        ImGuiInputTextFlags.None
      );

      if (ImGui.Button("Execute##exec"))
        Exec();

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

    private static void Exec()
    {
      var stopwatch = Stopwatch.StartNew();
      try
      {
        var proc = new Processor { OnDevWrite = OnDevWrite };

        var len = buffer.IndexOf((byte)0);
        var source = System.Text.Encoding.ASCII.GetString(buffer, 0, len);

        var ldTime = stopwatch.Elapsed.Milliseconds;

        Assembler.Assemble(source, proc.Memory);

        var asmTime = stopwatch.Elapsed.Milliseconds - ldTime;

        for (var i = 0; i < 10000 && proc.SleepTime == 0; i++)
          proc.Step();

        var runTime = stopwatch.Elapsed.Milliseconds - asmTime - ldTime;

        stats = $"ld: {ldTime:0.##}ms, asm: {asmTime:0.##}ms, run: {runTime:0.##}ms";
      }
      catch (Exception ex)
      {
        stats = ex.Message;
      }
      stopwatch.Stop();
    }

    private static void OnDevWrite(ulong devId, ValArray data)
    {
      AddOutput($"{devId}> {data}");
    }

    private static void AddOutput(string line) =>
      output.Add(line);
  }
}