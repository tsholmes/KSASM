
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
        var inst = Instruction.Decode(mem.Read(addr, DataType.P24).Unsigned);
        inst.Format(ref line, ps.Symbols);
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