
using System;
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM.UI
{
  public class MemViewWindow(ImGuiID dock, ProcSystem ps) : DockedWindow("MemView", dock, ps)
  {
    private const int VALS_PER_LINE = 16;
    private const int VAL_LINES = 16;
    private const int TOTAL_VALS = VALS_PER_LINE * VAL_LINES;
    private int startAddress = 0;
    private bool followPC = true;
    private bool showInst = true;
    private bool showData = true;
    private int hoverAddress = -1;
    private ImGuiTextFilter searchFilter = new();

    public override DockGroup Group => DockGroup.Memory;
    protected override void Draw()
    {
      Span<char> lineBuf = stackalloc char[512];
      var dline = new DataLineView(lineBuf, VALS_PER_LINE, ps.Symbols);
      var line = new LineBuilder(lineBuf);

      ImGui.BeginDisabled(followPC);
      ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8f);
      ImGui.InputInt("##debugAddress", ref startAddress, VALS_PER_LINE, TOTAL_VALS, ImGuiInputTextFlags.CharsHexadecimal);
      var dhovered = followPC && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);

      ImGui.SameLine();
      ImGui.SetNextItemWidth(-float.Epsilon);
      if (ImGui.BeginCombo("##debugLabel", "Goto Label", ImGuiComboFlags.HeightLargest))
      {
        if (ImGui.IsWindowAppearing())
        {
          ImGui.SetKeyboardFocusHere();
          searchFilter.Clear();
        }
        searchFilter.Draw("##debugLabelFilter", -float.Epsilon);

        var more = 0;
        var count = ps.Symbols?.LabelCount ?? 0;
        var matchCount = 0;
        const int MAX_LABELS = 14;
        Span<AddrInfo> linfo = stackalloc AddrInfo[1];
        for (var i = 0; i < count; i++)
        {
          var label = ps.Symbols.Label(i);
          if (!searchFilter.PassFilter(label.Label))
            continue;
          if (matchCount == MAX_LABELS)
          {
            more++;
            continue;
          }
          matchCount++;
          line.Clear();
          line.Add(label.Addr, "X6");
          line.Sp();
          line.Add(label.Label);
          ImGui.PushID(i);
          if (ImGui.Selectable(line.Line))
          {
            ps.Symbols.GetAddrInfo(label.Addr, linfo, false, showData);
            ScrollToAddr(label.Addr, linfo[0].Type?.SizeBytes() ?? 1);
          }
          ImGui.PopID();
        }
        if (more > 0)
        {
          line.Clear();
          line.Add('+');
          line.Add(more, "g");
          line.Add(" more");
          ImGui.Text(line.Line);
        }

        ImGui.EndCombo();
      }
      dhovered |= followPC && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
      ImGui.EndDisabled();
      if (dhovered)
        ImGui.SetTooltip("Disable 'Follow PC' to edit address");

      startAddress = Math.Clamp(startAddress, 0, Processor.MAIN_MEM_SIZE - TOTAL_VALS);

      ImGui.Checkbox("Follow PC", ref followPC);

      ImGui.SameLine();
      ImGui.Checkbox("Show Inst", ref showInst);

      ImGui.SameLine();
      ImGui.Checkbox("Show Data", ref showData);

      if (followPC)
        ScrollToAddr(ps.Processor.PC, Processor.INST_SIZE);

      var pcoff = ps.Processor.PC - startAddress;

      Span<AddrInfo> infos = stackalloc AddrInfo[TOTAL_VALS];
      ps.Symbols?.GetAddrInfo(startAddress, infos, showInst, showData);

      Span<byte> data = stackalloc byte[TOTAL_VALS];
      ps.Processor.MappedMemory.Read(data, startAddress);

      dline.Clear();
      dline.Empty(7);
      for (var i = 0; i < VALS_PER_LINE; i++)
      {
        var col = (startAddress + i) & 15;
        dline.Sp();
        dline.Add(col, "X2");
      }
      ImGui.Text(dline);

      var hoverStart = -1;
      var hoverLen = 0;

      if (hoverAddress >= startAddress & hoverAddress < startAddress + TOTAL_VALS)
      {
        var hoff = hoverAddress - startAddress;
        var instOff = -1;
        var dataOff = -1;
        var dataLen = 0;
        for (var i = hoff; i >= 0; i--)
        {
          if (instOff == -1 && hoff - i < Processor.INST_SIZE && infos[i].Inst.HasValue)
            instOff = i;
          if (dataOff == -1 && infos[i].Type.HasValue)
          {
            var dlen = infos[i].Type.Value.SizeBytes() * infos[i].Width.Value;
            if (hoff - i < dlen)
            {
              dataOff = i;
              dataLen = dlen;
            }
          }
        }
        if (instOff >= 0)
        {
          hoverStart = instOff;
          hoverLen = Processor.INST_SIZE;
        }
        else if (dataOff >= 0)
        {
          hoverStart = dataOff;
          hoverLen = dataLen;
        }
        else
        {
          hoverStart = hoff;
          hoverLen = 1;
        }
      }

      var mouse = ImGui.GetMousePos();

      var offset = 0;
      var nextHoverOff = -1;
      for (var lnum = 0; lnum < VAL_LINES; lnum++)
      {
        dline.Clear();
        var addr = startAddress + lnum * VALS_PER_LINE;
        dline.Add(addr, "X6");
        dline.Sp();
        for (var i = 0; i < VALS_PER_LINE; i++)
        {
          if (offset == pcoff)
            dline.HighlightData(Processor.INST_SIZE, AsmUi.PCHighlight);
          else if (offset == hoverStart)
            dline.HighlightData(hoverLen, AsmUi.TokenHoverHilight);
          dline.AddData(infos[offset], data[offset..], out var rect);
          if (rect.Contains(mouse))
            nextHoverOff = offset;
          offset++;
        }
        ImGui.Text(dline);
      }

      // dont draw hover tooltip if search combo is open
      if (!ImGui.IsWindowFocused())
        nextHoverOff = -1;

      if (nextHoverOff >= hoverStart && nextHoverOff < hoverStart + hoverLen && ImGui.BeginTooltip())
      {
        var addr = startAddress + hoverStart;
        var id = ps.Symbols?.ID(addr) ?? default;

        line.Clear();
        line.Add(addr, "X6");
        if (!string.IsNullOrEmpty(id.Label))
        {
          line.Add(": ");
          line.Add(id.Label);
          if (id.Offset != 0)
          {
            line.Add('+');
            line.Add(id.Offset, "g");
          }
        }
        ImGui.Text(dline);

        var info = infos[hoverStart];
        if (info.Inst.HasValue && hoverStart + Processor.INST_SIZE <= TOTAL_VALS)
        {
          var inst = Instruction.Decode(Encoding.Decode(data[hoverStart..], Processor.INST_TYPE).Unsigned);
          line.Clear();
          inst.Format(ref line, ps.Symbols);
          ImGui.Text(line.Line);
        }
        else if (info.Type.HasValue)
        {
          var type = info.Type.Value;
          var width = info.Width.Value;
          line.Clear();
          line.Add(type);
          line.Add('*');
          line.Add(width, "g");
          ImGui.Text(line.Line);

          var doff = hoverStart;

          for (var i = 0; i < width && doff + type.SizeBytes() <= TOTAL_VALS; i++, doff += type.SizeBytes())
          {
            line.Clear();
            var val = Encoding.Decode(data[doff..], type);
            line.Add(val, type);
            if (type == DataType.P24 && ps.Symbols != null)
            {
              id = ps.Symbols.ID((int)val.Unsigned);
              if (id.Label != null)
              {
                line.Sp();
                line.Add(id.Label);
                if (id.Offset != 0)
                {
                  line.Add('+');
                  line.Add(id.Offset, "g");
                }
              }
            }
            ImGui.Text(line.Line);
          }
        }
        else
        {
          line.Clear();
          line.Add(data[hoverStart], "X2");
          ImGui.Text(line.Line);
        }

        ImGui.EndTooltip();
      }

      hoverAddress = nextHoverOff + startAddress;
    }

    private void ScrollToAddr(int addr, int align = 1)
    {
      startAddress -= startAddress % align;
      startAddress += addr % align;

      if (addr < startAddress || addr >= startAddress + TOTAL_VALS)
        startAddress = addr - VALS_PER_LINE * 3;
      else if (startAddress > addr - VALS_PER_LINE * 3)
        startAddress -= (startAddress - (addr - VALS_PER_LINE * 3) + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;
      else if (addr - TOTAL_VALS + VALS_PER_LINE * 4 > startAddress)
        startAddress += ((addr - TOTAL_VALS + VALS_PER_LINE * 4) - startAddress + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;

      if (startAddress < 0)
        startAddress = 0;
      if (startAddress > Processor.MAIN_MEM_SIZE - TOTAL_VALS)
      {
        startAddress = Processor.MAIN_MEM_SIZE - TOTAL_VALS;
        if (addr % align != 0)
          startAddress -= VALS_PER_LINE;
      }

      startAddress -= startAddress % align;
      startAddress += addr % align;
    }
  }
}