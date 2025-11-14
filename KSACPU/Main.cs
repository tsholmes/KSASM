
using System;
using System.IO;

namespace KSACPU
{
  public static class KSACPUMain
  {
    public static void Main(string[] args)
    {
      Assembler.Debug = false;
      Memory.DebugRead = false;
      Memory.DebugWrite = false;
      Processor.DebugOps = false;

      var source = File.ReadAllText(args.Length > 0 ? args[0] : "test.kasm");

      var proc = new Processor();

      Assembler.Assemble(source, proc.Memory);

      for (var i = 0; i < 10000 && proc.SleepTime == 0; i++)
        proc.Step();
    }
  }
}