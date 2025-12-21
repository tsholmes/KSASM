
using System;
using Brutal.ImGuiApi;

namespace KSASM
{
  public partial class AsmUi
  {
    private static void DrawStackView()
    {
      var FP = Current.Processor.FP;
      var SP = Current.Processor.SP;

      if (FP == 0) FP = Processor.MAIN_MEM_SIZE;
      if (SP == 0) SP = Processor.MAIN_MEM_SIZE;

      var start = Math.Clamp(Math.Min(FP, SP), 0, Processor.MAIN_MEM_SIZE);
      var end = Math.Clamp(Math.Max(FP, SP) + 128, 0, Processor.MAIN_MEM_SIZE);

      if (end - start > 256)
        start = end - 256;

      var line = new LineBuilder(stackalloc char[128]);
      Span<DataType?> stack = stackalloc DataType?[256];
      var stackCount = 0;

      var addr = start;
      while (addr < end)
      {
        var type = stack[stackCount++] = Current.TypeMem.Read(addr);
        addr += type?.SizeBytes() ?? 1;
      }

      while (--stackCount >= 0)
      {
        var type = stack[stackCount];
        var sz = type?.SizeBytes() ?? 1;
        addr -= sz;
        DrawStackVal(ref line, addr, type ?? DataType.U8, type != null);
      }
    }

    private static void DrawStackVal(ref LineBuilder line, int addr, DataType type, bool known)
    {
      Span<byte> buf = stackalloc byte[16];
      // read from underlying memory so we don't trigger read hooks
      Current.Processor.MappedMemory.Read(buf[..type.SizeBytes()], addr);

      var val = Encoding.Decode(buf, type);

      var fp = Current.Processor.FP;
      if (fp == 0 && addr >= 1000) fp = Processor.MAIN_MEM_SIZE;
      var sp = Current.Processor.SP;
      if (sp == 0 && addr >= 1000) sp = Processor.MAIN_MEM_SIZE;

      line.Clear();
      line.Add(addr, "X6");
      line.Add(" FP");
      line.Add(addr - fp, "+0;-0");
      line.PadRight(14);
      if (known)
        line.Add(type);
      else
        line.Add(":??");
      line.PadRight(20);
      line.Add(val, type);
      line.PadRight(40);
      line.Add("SP");
      line.Add(addr - sp, "+0;-0");

      ImGui.Text(line.Line);
    }
  }
}