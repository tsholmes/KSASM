
using Brutal.ImGuiApi;

namespace KSASM
{
  public static partial class AsmUi
  {
    private static bool instFollowPC = true;
    private static int instLastPC = -1;
    private static void DrawInstView()
    {
      if (ImGui.Checkbox("Follow PC##instfollowpc", ref instFollowPC))
        instLastPC = -1; // scroll to PC on enable

      ImGui.Separator();
      ImGui.BeginChild("##instview", new(-float.Epsilon, -float.Epsilon), windowFlags: ImGuiWindowFlags.HorizontalScrollbar);
      var iter = Current.Symbols?.InstIter() ?? default;
      var line = new LineBuilder(stackalloc char[256]);
      var mem = Current.Processor.Memory;
      var pc = Current.Processor.PC;

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
          ImGuiX.DrawRect(ImGuiX.LineRect(), PCHighlight);
        line.Clear();
        var inst = Instruction.Decode(mem.Read(addr, DataType.P24).Unsigned);
        inst.Format(ref line, Current.Symbols);
        ImGui.Text(line.Line);
        if (instFollowPC && addr == pc && pc != instLastPC)
          ImGui.SetScrollHereY();

        first = false;
      }
      instLastPC = pc;

      ImGui.EndChild();
    }
  }
}