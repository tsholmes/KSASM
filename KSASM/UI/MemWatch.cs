
using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM.UI
{
  public class MemWatchWindow(ImGuiID dock, ProcSystem ps) : DockedWindow("MemWatch", dock, ps)
  {
    private readonly List<(int, DataType)> watches = [];
    private readonly ImInputString addText = new(1024);
    private DataType addType = DataType.U8;
    private int lastAddr = -1;

    public override DockGroup Group => DockGroup.Memory;
    protected override void Draw()
    {
      var mem = ps.Processor.Memory;
      var line = new LineBuilder(stackalloc char[128]);
      bool active;

      var fontSize = ImGui.GetFontSize();
      var spacing = ImGui.GetStyle().ItemSpacing.X;

      ImGui.SetNextItemWidth(-fontSize * 4 - spacing * 2);
      ImGui.InputTextWithHint("##watchAddr", "label or address", addText);
      if (active = ImGui.IsItemActive())
        DrawMemWatchFilterTooltip();
      ImGui.SameLine();
      ImGui.SetNextItemWidth(fontSize * 3);
      ImGuiX.InputEnum("##watchType", ref addType);

      var addr = -1;
      var label = ps.Symbols?.FindLabel(addText.ToString());
      if (label != null)
        addr = label.Value.Addr;
      else if (Values.TryParseUnsigned(addText.ToString(), out var parsedAddr))
        addr = (int)parsedAddr;

      ImGui.SameLine();
      ImGui.BeginDisabled(addr == -1);
      ImGui.SetNextItemWidth(fontSize * 1);
      if (ImGui.Button("+##watchAdd"))
        watches.Add((addr, addType));
      ImGui.EndDisabled();

      if (addr != -1 && addr != lastAddr)
      {
        lastAddr = addr;
        Span<AddrInfo> linfo = stackalloc AddrInfo[1];
        ps.Symbols?.GetAddrInfo(addr, linfo, false, true);
        var itype = linfo[0].Type;
        if (itype != null)
          addType = itype.Value;
      }

      ImGui.BeginChild("##watches", new(-float.Epsilon, -float.Epsilon));

      var toRemove = -1;
      for (var i = 0; i < watches.Count; i++)
      {
        ImGui.PushID(i);
        var (waddr, wtype) = watches[i];

        if (ImGui.Button("X##watchRemove"))
          toRemove = i;
        ImGui.SameLine();
        line.Clear();
        line.AddAddr(waddr, ps.Symbols);
        line.Add(": ");
        var centerWidth = ImGuiX.TextWidths[line.Length];
        if (waddr + wtype.SizeBytes() < Processor.MAIN_MEM_SIZE)
          line.Add(mem.Read(waddr, wtype), wtype);

        var maxWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X;
        var width = ImGuiX.TextWidths[line.Length];
        if (centerWidth < maxWidth / 2 && width < maxWidth)
          ImGui.SetCursorPosX(ImGui.GetCursorPosX() + maxWidth / 2 - centerWidth);

        ImGui.Text(line.Line);

        ImGui.PopID();
      }
      if (toRemove != -1)
        watches.RemoveAt(toRemove);

      ImGui.EndChild();
    }

    private void DrawMemWatchFilterTooltip()
    {
      var line = new LineBuilder(stackalloc char[128]);
      var filter = new ImGuiTextFilter(addText);
      var any = false;
      var lcount = ps.Symbols?.LabelCount ?? 0;
      for (var i = 0; i < lcount && !any; i++)
      {
        if (addText.Length == 0 || filter.PassFilter(ps.Symbols.Label(i).Label))
          any = true;
      }
      if (!any)
        return;

      var left = ImGui.GetItemRectMin().X;
      var bottom = ImGui.GetItemRectMax().Y;
      ImGui.SetNextWindowPos(new(left, bottom));
      ImGui.BeginTooltip();
      var more = 0;
      const int MAX_LABELS = 15;
      var matched = 0;
      for (var i = 0; i < lcount; i++)
      {
        var label = ps.Symbols.Label(i);
        if (addText.Length > 0 && !filter.PassFilter(label.Label))
          continue;
        if (matched == MAX_LABELS)
        {
          more++;
          continue;
        }
        matched++;
        line.Clear();
        line.Add(label.Addr, "X6");
        line.Sp();
        line.Add(label.Label);
        ImGui.Text(line.Line);
      }
      if (more > 0)
      {
        line.Clear();
        line.Add('+');
        line.Add(more, "g");
        line.Add(" more");
        ImGui.Text(line.Line);
      }
      ImGui.EndTooltip();
    }
  }
}