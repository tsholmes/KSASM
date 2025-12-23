
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Brutal.ImGuiApi;
using KSASM.Assembly;
using KSASM.UI;

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
      Processor.DebugOps = pargs.HasFlag("debug", "ops") || pargs.HasFlag("debug", "operands");
      Processor.DebugOperands = pargs.HasFlag("debug", "operands");

      KSASMMod.CWD = Directory.GetCurrentDirectory();

      Directory.SetCurrentDirectory(KSADir);

      AppDomain.CurrentDomain.AssemblyResolve += FindAssembly;

      if (pargs.HasFlag("standalone"))
        return RunStandalone(pargs);

      if (pargs.HasFlag("game"))
        return RunGame(args, pargs);

      Library.Init(KSASMMod.CWD);
      Library.RefreshIndex();

      var limit = 10000000;
      if (pargs.Val("limit", out var slimit))
        limit = int.Parse(slimit);

      var maxsleep = 0ul;
      if (pargs.Val("maxsleep", out var smaxsleep))
        maxsleep = ulong.Parse(smaxsleep);

      var scriptName = pargs.Positional(0, out var sname) ? sname : "hello_world";

      var source = Library.LoadExample(scriptName);

      var proc = new Processor
      {
        OnDebug = A => Console.WriteLine($"> {A}"),
        OnDebugStr = str => Console.WriteLine($"> {str}"),
      };

      var types = new TypeMemory();
      proc.Memory.OnWrite = types.Write;

      var stopwatch = Stopwatch.StartNew();

      // if (true)
      // {
      //   for (var i = 0; i < 100; i++)
      //     Assembler.Assemble(source, proc.Memory);
      //   Console.WriteLine($"{stopwatch.Elapsed.TotalMilliseconds:0.##}ms");
      //   return 0;
      // }

      var debug = Assembler.Assemble(source, proc.Memory);

      var asmTime = stopwatch.Elapsed.TotalMilliseconds;

      for (var i = 0; i < limit && proc.SleepTime <= maxsleep; i++)
        proc.Step();

      var runTime = stopwatch.Elapsed.TotalMilliseconds - asmTime;

      Console.WriteLine($"asm: {asmTime:0.##}ms, run: {runTime:0.##}ms");

      if (pargs.HasFlag("debug", "source"))
      {
        var line = new LineBuilder(stackalloc char[256]);
        var iter = debug.SourceLineIter(new(-1));
        while (iter.Next(out _))
        {
          line.Clear();
          iter.Build(ref line);
          Console.WriteLine(line.Line);
        }
      }

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

    private static int RunStandalone(ProgramArgs pargs)
    {
      if (pargs.Positional(0, out var name))
        EditorWindow.SetDefaultExample(name);

      StandaloneImGui.Run(() =>
      {
        ImGui.Begin("Test");
        ImGui.Text("TESTING 123");
        ImGui.End();
      });
      return 0;
    }

    private static int RunGame(string[] args, ProgramArgs pargs)
    {
      var mod = new KSASMMod();
      mod.ImmediateLoad(null);

      if (pargs.Positional(0, out var name))
        EditorWindow.SetDefaultExample(name);

      var main = typeof(KSA.Program).GetMethod(
        "Main", BindingFlags.Static | BindingFlags.NonPublic).CreateDelegate<Func<string[], int>>();
      return main(args);
    }
  }
}