
using System;
using System.Linq;
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

      if (mod == null)
      {
        AllModsLoadedPatch.Mod = this;
        new PatchClassProcessor(harmony, typeof(AllModsLoadedPatch)).Patch();
      }
    }

    [StarMapAllModsLoaded]
    public void AllModsLoaded()
    {
      var terminalCanvas = ModLibrary.Get<GaugeCanvas>("KSASMTerminal");
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
          TerminalLabel.Labels.Add(label);
        }
      }

      labelsComp.OnDataLoad(labelsComp.Mod);
    }

    [StarMapUnload]
    public void Unload()
    {
      harmony.UnpatchAll();
    }
  }

  public static class Patches
  {
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.OnDrawSelectedUi)), HarmonyPrefix]
    public static void Vehicle_OnDrawSelectedUi_Postfix(Vehicle __instance, Viewport viewport)
    {
      AsmUi.OnFrame(__instance);
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

  public static class AllModsLoadedPatch
  {
    public static KSASMMod Mod;

    [HarmonyPatch(typeof(ModLibrary), nameof(ModLibrary.LoadAll)), HarmonyPostfix]
    static void ModLibrary_LoadAll_Postfix()
    {
      Mod.AllModsLoaded();
    }
  }
}

namespace StarMap.API
{
  [AttributeUsage(AttributeTargets.Class)]
  internal class StarMapModAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal class StarMapImmediateLoadAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal class StarMapAllModsLoadedAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Method)]
  internal class StarMapUnloadAttribute : Attribute { }
}