
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
        var proc = new Processor { OnDevWrite = OnDevWrite, OnDevRead = OnDevRead };

        var len = buffer.IndexOf((byte)0);
        var source = System.Text.Encoding.ASCII.GetString(buffer, 0, len);

        var ldTime = stopwatch.Elapsed.Milliseconds;

        Assembler.Assemble(new("script", source), proc.Memory);

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
      switch (devId)
      {
        case 1: FCWrite(data); break;
        default: AddOutput($"{devId}> {data}"); break;
      }
    }

    private static void OnDevRead(ulong devId, ValArray data)
    {
      switch (devId)
      {
        case 1: FCRead(data); break;
        default: break;
      }
    }

    private static ulong FCReadOp = 0;

    private static void FCWrite(ValArray data)
    {
      var opv = data.Values[0];
      opv.Convert(data.Mode, ValueMode.Unsigned);

      var valv = data.Values[1];

      switch (opv.Unsigned)
      {
        case 0: // set read mode
          valv.Convert(data.Mode, ValueMode.Unsigned);
          FCReadOp = valv.Unsigned;
          break;
        case 1: // set reference frame
          valv.Convert(data.Mode, ValueMode.Unsigned);
          Program.ControlledVehicle?.SetNavBallFrame((VehicleReferenceFrame)(valv.Unsigned % 4));
          break;
      }
    }

    private static void FCRead(ValArray data)
    {
      switch (FCReadOp)
      {
        case 1: // read reference frame
          data.Values[0].Unsigned =
            (ulong)(Program.ControlledVehicle?.NavBallData.Frame ?? VehicleReferenceFrame.EclBody);
          break;
      }
    }

    private static void AddOutput(string line) =>
      output.Add(line);
  }
}