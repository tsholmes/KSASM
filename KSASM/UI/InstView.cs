
using System;
using Brutal.ImGuiApi;

namespace KSASM.UI
{
  public class InstViewWindow(ImGuiID dock, ProcSystem ps) : DockedWindow("InstView", dock, ps)
  {
    private bool followPC = true;
    private int lastPC = -1;

    public override DockGroup Group => DockGroup.Editor;
    protected override void Draw()
    {
      if (ImGui.Checkbox("Follow PC", ref followPC))
        lastPC = -1; // scroll to PC on enable

      ImGui.Separator();
      ImGui.BeginChild("##instview", new(-float.Epsilon, -float.Epsilon), windowFlags: ImGuiWindowFlags.HorizontalScrollbar);
      var iter = ps.Symbols?.InstIter() ?? default;
      var line = new LineBuilder(stackalloc char[256]);
      var mem = ps.Processor.Memory;
      var pc = ps.Processor.PC;

      var first = true;

      Span<byte> buf = stackalloc byte[256];

      while (iter.Next(out var addr, out var label))
      {
        if (first && label == null)
          ImGui.SeparatorText("@000000");
        if (label != null)
        {
          line.Clear();
          line.Add('@');
          line.Add(addr, "X6");
          line.Sp();
          line.Add(label);
          ImGui.SeparatorText(line.Line);
        }
        if (addr == pc)
          ImGuiX.DrawRect(ImGuiX.LineRect(), AsmUi.PCHighlight);
        line.Clear();
        var inst = Instruction.Decode(mem.Read(addr, Processor.INST_TYPE).Unsigned);
        var immAddr = addr + Processor.INST_SIZE;
        var immSize = Math.Min(inst.ImmSize(), buf.Length);
        immSize = Math.Min(immSize, Processor.MAIN_MEM_SIZE - immAddr);
        if (immSize > 0)
          ps.Processor.MappedMemory.Read(buf[..immSize], immAddr);
        inst.Format(ref line, buf[..immSize]);
        ImGui.Text(line.Line);
        if (followPC && addr == pc && pc != lastPC)
          ImGui.SetScrollHereY();

        first = false;
      }
      lastPC = pc;

      ImGui.EndChild();
    }
  }
}