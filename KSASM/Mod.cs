
using Brutal.ImGuiApi;
using Brutal.ImGuiApi.Abstractions;
using Brutal.Logging;
using HarmonyLib;
using KSA;
using KSASM.UI;
using StarMap.API;

namespace KSASM
{
  [StarMapMod]
  public class KSASMMod
  {
    public static string CWD;
    private Harmony harmony;

    [StarMapImmediateLoad]
    public void ImmediateLoad(Mod mod)
    {
      DefaultCategory.Log.Debug("Loading KSASM");

      Library.Init(CWD);
      Library.RefreshIndex();

      harmony = new("KSASM");
      new PatchClassProcessor(harmony, typeof(Patches)).Patch();
    }

    [StarMapUnload]
    public void Unload()
    {
      harmony.UnpatchAll();
    }
  }

  public static class Patches
  {
    private static ProcContext ctx;
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.OnDrawSelectedUi)), HarmonyPrefix]
    public static void Vehicle_OnDrawSelectedUi_Postfix(Vehicle __instance, Viewport viewport)
    {
      if (__instance != KSA.Program.ControlledVehicle)
        return;
      if (ctx?.Id != __instance.Id)
        ctx = new VehicleProcContext(__instance);
      AsmUi.OnFrame(ctx);
    }

    [HarmonyPatch(typeof(ConsoleWindowEx), nameof(ConsoleWindowEx.OnKey)), HarmonyPrefix]
    public static bool ConsoleWindowEx_OnKey_Prefix(ref bool __result, ConsoleWindow console)
    {
      if (!console.IsOpen && AsmUi.IsTyping)
      {
        __result = true;
        return false;
      }
      return true;
    }

    [HarmonyPatch(typeof(GaugeCanvas), nameof(GaugeCanvas.OnDrawMenuBar)), HarmonyPostfix]
    public static void GaugeCanvas_OnDrawMenuBar_Postfix()
    {
      AsmUi.OnMenu();
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
  }
}