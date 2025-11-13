
using System;
using System.IO;

namespace KSACPU
{
  public static class KSACPUMain
  {
    public static void Main(string[] args)
    {
      var asm = new Assembler();
      asm.LoadSource(File.ReadAllText("test.kasm"));

      var proc = new Processor();

      asm.Assemble(proc.Memory);

      for (var i = 0; i < 3; i++)
        proc.Step();
    }
  }
}