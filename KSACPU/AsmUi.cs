
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;

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

      foreach (var line in output)
        ImGui.Text(line);

      if (output.Count > 0 && ImGui.Button("Clear##clear"))
        output.Clear();

      ImGui.End();
      return false;
    }

    private static void Exec()
    {
      try
      {
        var proc = new Processor { OnDevWrite = OnDevWrite };

        var len = buffer.IndexOf((byte)0);

        Assembler.Assemble(System.Text.Encoding.ASCII.GetString(buffer, 0, len), proc.Memory);

        for (var i = 0; i < 10000 && proc.SleepTime == 0; i++)
          proc.Step();
      }
      catch (Exception ex)
      {
        output.Add(ex.Message);
      }
    }

    private static void OnDevWrite(ulong devId, ValArray data)
    {
      output.Add($"{devId}> {data}");
    }
  }
}