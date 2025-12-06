
using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private static readonly List<(int, DataType)> watches = [];
    private static InputString watchInput;
    private static DataType watchType = DataType.U8;
    private static int lastWatchAddr = -1;
    private static void DrawMemWatch()
    {
      var mem = Current.Processor.Memory;
      var line = new LineBuilder(stackalloc char[128]);
      bool active;

      var fontSize = ImGui.GetFontSize();
      var spacing = ImGui.GetStyle().ItemSpacing.X;

      watchInput ??= new(1024);
      var input = watchInput.Input;
      ImGui.SetNextItemWidth(-fontSize * 4 - spacing * 2);
      ImGui.InputTextWithHint("##watchAddr", "label or address", input);
      if (active = ImGui.IsItemActive())
        DrawMemWatchFilterTooltip();
      ImGui.SameLine();
      ImGui.SetNextItemWidth(fontSize * 3);
      ImGuiX.InputEnum("##watchType", ref watchType);

      var addr = -1;
      var label = Current.Symbols?.FindLabel(watchInput.AsString());
      if (label != null)
        addr = label.Value.Addr;
      else if (Values.TryParseUnsigned(watchInput.AsString(), out var parsedAddr))
        addr = (int)parsedAddr;

      ImGui.SameLine();
      ImGui.BeginDisabled(addr == -1);
      ImGui.SetNextItemWidth(fontSize * 1);
      if (ImGui.Button("+##watchAdd"))
        watches.Add((addr, watchType));
      ImGui.EndDisabled();

      if (addr != -1 && addr != lastWatchAddr)
      {
        lastWatchAddr = addr;
        Span<AddrInfo> linfo = stackalloc AddrInfo[1];
        Current.Symbols?.GetAddrInfo(addr, linfo, false, true);
        var itype = linfo[0].Type;
        if (itype != null)
          watchType = itype.Value;
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
        line.AddAddr(waddr, Current.Symbols);
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

    private static void DrawMemWatchFilterTooltip()
    {
      var input = watchInput.Input;
      var line = new LineBuilder(stackalloc char[128]);
      var filter = new ImGuiTextFilter(input);
      var any = false;
      var lcount = Current.Symbols?.LabelCount ?? 0;
      for (var i = 0; i < lcount && !any; i++)
      {
        if (input.Length == 0 || filter.PassFilter(Current.Symbols.Label(i).Label))
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
        var label = Current.Symbols.Label(i);
        if (input.Length > 0 && !filter.PassFilter(label.Label))
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