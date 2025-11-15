
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace KSASM
{
  public static class Program
  {
    private const string KSADir = @"C:\Program Files\Kitten Space Agency";

    public static int Main(string[] args)
    {
      Assembler.Debug = false;
      Memory.DebugRead = false;
      Memory.DebugWrite = false;
      Processor.DebugOps = false;

      Library.LibraryDir = Path.Join(Directory.GetCurrentDirectory(), "Library");

      Directory.SetCurrentDirectory(KSADir);

      AppDomain.CurrentDomain.AssemblyResolve += FindAssembly;

      if (args.Any(arg => arg.ToLowerInvariant() == "--game"))
      {
        RunPatches();
        return RunGame(args);
      }

      var fname = args.Length > 0 ? args[0] : "test";
      var source = Library.LoadImport(fname);

      var proc = new Processor()
      {
        OnDevWrite = (devID, val) => Console.WriteLine($"{devID}> {val}")
      };

      var stopwatch = Stopwatch.StartNew();

      Assembler.Assemble(source, proc.Memory);

      var asmTime = stopwatch.Elapsed.Milliseconds;

      for (var i = 0; i < 10000 && proc.SleepTime == 0; i++)
        proc.Step();

      var runTime = stopwatch.Elapsed.Milliseconds - asmTime;

      Console.WriteLine($"asm: {asmTime:0.##}ms, run: {runTime:0.##}ms");

      return 0;
    }

    private static Assembly FindAssembly(object sender, ResolveEventArgs args)
    {
      var name = new AssemblyName(args.Name).Name + ".dll";
      var path = Path.Join(KSADir, name);
      if (File.Exists(path))
        return Assembly.LoadFrom(path);
      return null;
    }

    private static void RunPatches()
    {
      var harmony = new Harmony("KSASM");
      new PatchClassProcessor(harmony, typeof(AsmUi)).Patch();
    }

    private static int RunGame(string[] args)
    {
      var main = typeof(KSA.Program).GetMethod(
        "Main", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate<Func<string[], int>>();
      return main(args);
    }
  }
}