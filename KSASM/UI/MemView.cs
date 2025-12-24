
using System;
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM.UI
{
  public class MemViewWindow(AsmUi parent) : DockedWindow("MemView", parent)
  {
    private const int VALS_PER_LINE = 16;
    private const int VAL_LINES = 16;
    private const int TOTAL_VALS = VALS_PER_LINE * VAL_LINES;
    private int startAddress = 0;
    private bool followPC = true;
    private bool showInst = true;
    private bool showData = true;
    private bool showChars = false;
    private ImGuiTextFilter searchFilter = new();
    private readonly CharGrid grid = new(65536, 6 + 1 + VALS_PER_LINE * 3, VAL_LINES + 1);

    public override DockGroup Group => DockGroup.Memory;
    protected override void Draw()
    {
      Span<char> lineBuf = stackalloc char[128];
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

      ImGui.BeginDisabled(showChars);

      ImGui.SameLine();
      ImGui.Checkbox("Show Inst", ref showInst);
      dhovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);

      ImGui.SameLine();
      ImGui.Checkbox("Show Data", ref showData);
      dhovered |= ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);

      ImGui.EndDisabled();

      if (showChars && dhovered)
        ImGui.SetTooltip("Disable 'Show Chars' to show instructions or data");

      ImGui.SameLine();
      ImGui.Checkbox("Show Chars", ref showChars);

      if (followPC)
        ScrollToAddr(ps.Processor.PC, Processor.INST_SIZE);

      var pcoff = ps.Processor.PC - startAddress;

      Span<AddrInfo> infos = stackalloc AddrInfo[TOTAL_VALS];
      ps.Symbols?.GetAddrInfo(startAddress, infos, showInst, showData);

      grid.NewFrame();
      var hoveredPt = grid.HoveredPoint();
      var hoveredRow = hoveredPt.Y - 1;
      var hoveredCol = (hoveredPt.X - 7) / 3;

      Span<byte> data = stackalloc byte[TOTAL_VALS];
      ps.Processor.MappedMemory.Read(data, startAddress);

      line.Clear();
      var header = grid[0..1, (6 + 1)..];
      for (var i = 0; i < VALS_PER_LINE; i++)
      {

        CharGrid.Highlight? hl = null;
        if (i == hoveredCol)
          hl = new() { Color = AsmUi.TokenHoverHilight };

        var col = (startAddress + i) & 15;

        header.Add(" ", hl);
        header.AddF(col, "X2", hl);
      }

      var addrCol = grid[1.., 0..6];
      line.Clear();
      for (var i = 0; i < VAL_LINES; i++)
      {
        CharGrid.Highlight? hl = null;
        if (i == hoveredRow)
          hl = new() { Color = AsmUi.TokenHoverHilight };

        var rowAddr = startAddress + (i * VALS_PER_LINE);
        addrCol.AddF(rowAddr, "X6", hl);
      }

      var dataView = grid[1.., 7..];

      var addr = startAddress;
      var endAddress = startAddress + TOTAL_VALS;
      var hoveredAddress = -1;
      while (addr < endAddress)
      {
        var off = addr - startAddress;
        var maxWidth = TOTAL_VALS - off;
        if (addr == ps.Processor.PC)
          dataView.AddHighlight(Math.Min(Processor.INST_SIZE, maxWidth) * 3, AsmUi.PCHighlight);

        var info = infos[addr - startAddress];

        if (showChars)
        {
          if (dataView.NextHovered(3))
            hoveredAddress = addr;

          line.Clear();
          line.AddEscaped((char)data[off]);
          line.PadLeft(3);
          dataView.Add(line.Line, dataView.HoveredHighlight(3, AsmUi.TokenHoverHilight));

          addr++;
        }
        else if (info.Inst is TokenIndex insti && maxWidth >= Processor.INST_SIZE)
        {
          var width = Processor.INST_SIZE * 3;
          var ival = Encoding.Decode(data[off..], Processor.INST_TYPE);
          var inst = Instruction.Decode(ival.Unsigned);
          line.Clear();
          inst.Format(ref line, data[(off + Processor.INST_SIZE)..]);

          if (line.Length < width)
            line.PadLeft(width);
          else if (line.Length > width)
          {
            line.Length = width - 3;
            line.Add("...");
          }

          if (dataView.NextHovered(width))
            hoveredAddress = addr;

          dataView.Add(line.Line, dataView.HoveredHighlight(width, AsmUi.TokenHoverHilight));

          addr += Processor.INST_SIZE;
        }
        else
        {
          var type = info.Type ?? DataType.U8;
          var tsize = type.SizeBytes();
          var width = tsize * 3;
          var dwidth = Math.Min(info.Width ?? 1, maxWidth / tsize);
          if (dwidth == 0)
          {
            type = DataType.U8;
            tsize = type.SizeBytes();
            width = tsize * 3;
            dwidth = 1;
          }

          if (dataView.NextHovered(width * dwidth))
          {
            hoveredAddress = addr;
            dataView.AddHighlight(width * dwidth, AsmUi.TokenHoverHilight);
          }

          for (var w = 0; w < dwidth; w++)
          {
            var val = Encoding.Decode(data[off..], type);
            line.Clear();
            line.Add(val, type);

            // if we have space on both sides, move off the edge
            if (line.Length < width - 1)
              line.Sp();

            if (line.Length < width)
              line.PadLeft(width);
            else if (line.Length > width)
            {
              line.Length = width - 3;
              line.Add("...");
            }

            dataView.Add(line.Line);

            addr += tsize;
            off += tsize;
          }
        }
      }

      grid.Draw();

      if (hoveredAddress != -1 && ImGui.IsWindowHovered())
      {
        ImGui.BeginTooltip();

        var off = hoveredAddress - startAddress;
        var info = infos[off];

        DrawDetails(hoveredAddress, infos[off], data[off..], ref line);

        ImGui.EndTooltip();
      }
    }

    private void DrawDetails(int addr, AddrInfo info, Span<byte> data, ref LineBuilder line)
    {
      if (info.Inst != null && data.Length >= Processor.INST_SIZE)
      {
        line.Clear();
        FormatAddr(ref line, addr);
        line.Add(": ");

        var inst = Instruction.Decode(Encoding.Decode(data, Processor.INST_TYPE).Unsigned);
        inst.Format(ref line, data[Processor.INST_SIZE..]);
        ImGui.Text(line.Line);
      }
      else
      {
        line.Clear();
        FormatAddr(ref line, addr);
        line.Add(": ");

        var lwidth = line.Length;

        var type = info.Type ?? DataType.U8;
        if (type.SizeBytes() > data.Length)
          type = DataType.U8;
        var tsize = type.SizeBytes();
        var dwidth = info.Width ?? 1;
        dwidth = Math.Min(dwidth, data.Length / tsize);

        Span<byte> sbuf = stackalloc byte[16];

        for (var w = 0; w < dwidth; w++)
        {
          line.PadRight(lwidth);
          var val = Encoding.Decode(data[(w * tsize)..], type);
          line.Add(val, type);
          if (info.Type != null)
            line.Add(type);

          if (type == DataType.S48)
          {
            var saddr = (int)(val.Unsigned & 0xFFFFFF);
            var slen = (int)((val.Unsigned >> 24) & 0xFFFFFF);
            var rslen = Math.Min(slen, Processor.MAIN_MEM_SIZE - saddr);
            rslen = Math.Min(rslen, 16);

            ps.Processor.MappedMemory.Read(sbuf[..rslen], saddr);

            line.Add(" \"");
            for (var i = 0; i < rslen; i++)
              line.AddEscaped((char)sbuf[i], escapeQuote: true);
            if (rslen < slen)
              line.Add("...");
            line.Add('"');
          }

          ImGui.Text(line.Line);
          line.Clear();
        }
      }
    }

    private void FormatAddr(ref LineBuilder line, int addr)
    {
      line.Add(addr, "X6");
      if (ps.Symbols?.ID(addr) is DebugSymbols.AddrId id && !string.IsNullOrEmpty(id.Label))
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