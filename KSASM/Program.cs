
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using KSASM.Assembly;

namespace KSASM
{
  public static class Program
  {
    private const string KSADir = @"C:\Program Files\Kitten Space Agency";

    public static int Main(string[] args)
    {
      var pargs = ProgramArgs.Parse(args);

      Assembler.Debug = pargs.HasFlag("debug", "asm");
      MacroParser.DebugMacros = pargs.HasFlag("debug", "macro");
      MemoryAccessor.DebugRead = pargs.HasFlag("debug", "read") || pargs.HasFlag("debug", "mem");
      MemoryAccessor.DebugWrite = pargs.HasFlag("debug", "write") || pargs.HasFlag("debug", "mem");
      Processor.DebugOps = pargs.HasFlag("debug", "ops");

      KSASMMod.CWD = Directory.GetCurrentDirectory();

      Directory.SetCurrentDirectory(KSADir);

      AppDomain.CurrentDomain.AssemblyResolve += FindAssembly;

      if (pargs.HasFlag("game"))
      {
        var mod = new KSASMMod();
        mod.ImmediateLoad(null);

        if (pargs.Positional(0, out var name))
          AsmUi.LoadExample(name);
        return RunGame(args);
      }

      Library.Init(KSASMMod.CWD);
      Library.RefreshIndex();

      var scriptName = pargs.Positional(0, out var sname) ? sname : "hello_world";

      var source = Library.LoadExample(scriptName);

      var proc = new Processor
      {
        OnDebug = (A, B) => Console.WriteLine($"> {A} {B}"),
        OnDebugStr = str => Console.WriteLine($"> {str}"),
      };

      var stopwatch = Stopwatch.StartNew();

      // if (true)
      // {
      //   for (var i = 0; i < 100; i++)
      //     Assembler.Assemble(source, proc.Memory);
      //   Console.WriteLine($"{stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
      //   return 0;
      // }

      Assembler.Assemble(source, proc.Memory);

      var asmTime = stopwatch.Elapsed.TotalMilliseconds;

      for (var i = 0; i < 10000000 && proc.SleepTime == 0; i++)
        proc.Step();

      var runTime = stopwatch.Elapsed.TotalMilliseconds - asmTime;

      Console.WriteLine($"asm: {asmTime:0.##}ms, run: {runTime:0.##}ms");

      return 0;
    }

    private static System.Reflection.Assembly FindAssembly(object sender, ResolveEventArgs args)
    {
      var name = new AssemblyName(args.Name).Name + ".dll";
      var path = Path.Join(KSADir, name);
      if (File.Exists(path))
        return System.Reflection.Assembly.LoadFrom(path);
      return null;
    }

    private static int RunGame(string[] args)
    {
      var main = typeof(KSA.Program).GetMethod(
        "Main", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate<Func<string[], int>>();
      return main(args);
    }
  }
}