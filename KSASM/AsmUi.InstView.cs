
using Brutal.ImGuiApi;
using Brutal.Numerics;

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

      if (ImGui.BeginChild("##instview", new(-float.Epsilon, 400), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
      {
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
          {
            var rmin = ImGui.GetCursorScreenPos();
            var avail = ImGui.GetContentRegionAvail();
            var rmax = rmin + new float2(avail.X, ImGui.GetTextLineHeight());
            ImGui.GetWindowDrawList().AddRectFilled(rmin, rmax, new(128, 16, 16));
          }
          line.Clear();
          var inst = Instruction.Decode(mem.Read(addr, DataType.U64).Unsigned);
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
}