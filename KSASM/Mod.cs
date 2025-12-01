
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

      var asmDir = Directory.GetParent(typeof(KSASMMod).Assembly.Location).FullName;
      var libCandidates = new string[] {
        Path.Join(asmDir, "Library"), Path.Join(CWD ?? Directory.GetCurrentDirectory(), "Library")
      };
      foreach (var p in libCandidates)
      {
        if (Directory.Exists(p))
        {
          Library.LibraryDir = p;
          break;
        }
      }

      Library.LibraryDir ??= Directory.GetCurrentDirectory();
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