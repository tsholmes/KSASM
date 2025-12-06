
using System;
using System.IO;
using Brutal.Logging;
using HarmonyLib;
using KSA;
using StarMap.API;

namespace KSASM
{
  [StarMapMod]
  public class KSASMMod
  {
    public static string CWD;
    private Harmony harmony;

    [StarMapImmediateLoad]
    public void ImmediateLoad(Mod _)
    {
      DefaultCategory.Log.Debug("Loading KSASM");

      Library.Init(CWD);
      Library.RefreshIndex();

      harmony = new("KSASM");
      new PatchClassProcessor(harmony, typeof(AsmUi)).Patch();
    }

    [StarMapUnload]
    public void Unload()
    {
      harmony.UnpatchAll();
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
  internal class StarMapUnloadAttribute : Attribute { }
}