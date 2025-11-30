
using System;
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private const int VALS_PER_LINE = 16;
    private const int VAL_LINES = 16;
    private const int TOTAL_VALS = VALS_PER_LINE * VAL_LINES;
    private static int debugAddress = 0;
    private static bool debugPC = true;
    private static bool debugShowInst = true;
    private static bool debugShowData = true;
    private static int hoverAddress = -1;
    private static InputFilter searchFilter;

    private static void DrawMemView()
    {
      searchFilter ??= new();
      Span<char> lineBuf = stackalloc char[512];
      var dline = new DataLineView(lineBuf, VALS_PER_LINE, Current.Symbols);
      var line = new LineBuilder(lineBuf);

      ImGui.BeginDisabled(debugPC);
      ImGui.SetNextItemWidth(ImGui.GetFontSize() * 8f);
      ImGui.InputInt("##debugAddress", ref debugAddress, VALS_PER_LINE, TOTAL_VALS, ImGuiInputTextFlags.CharsHexadecimal);
      var dhovered = debugPC && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);

      ImGui.SameLine();
      ImGui.SetNextItemWidth(ImGui.GetFontSize() * 16f);
      if (ImGui.BeginCombo("##debugLabel", "Goto Label", ImGuiComboFlags.HeightLargest))
      {
        if (ImGui.IsWindowAppearing())
        {
          ImGui.SetKeyboardFocusHere();
          searchFilter.Clear();
        }
        searchFilter.Draw("##debugLabelFilter", -float.Epsilon);

        var more = 0;
        var count = Current.Symbols?.LabelCount ?? 0;
        var matchCount = 0;
        const int MAX_LABELS = 14;
        Span<AddrInfo> linfo = stackalloc AddrInfo[1];
        for (var i = 0; i < count; i++)
        {
          var label = Current.Symbols.Label(i);
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
            Current.Symbols.GetAddrInfo(label.Addr, linfo, false, debugShowData);
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
      dhovered |= debugPC && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
      ImGui.EndDisabled();
      if (dhovered)
        ImGui.SetTooltip("Disable 'Follow PC' to edit address");

      debugAddress = Math.Clamp(debugAddress, 0, Processor.MAIN_MEM_SIZE - TOTAL_VALS);

      ImGui.Checkbox("Follow PC", ref debugPC);

      ImGui.SameLine();
      ImGui.Checkbox("Show Inst", ref debugShowInst);

      ImGui.SameLine();
      ImGui.Checkbox("Show Data", ref debugShowData);

      if (debugPC)
        ScrollToAddr(Current.Processor.PC, 8);

      var pcoff = Current.Processor.PC - debugAddress;

      Span<AddrInfo> infos = stackalloc AddrInfo[TOTAL_VALS];
      Current.Symbols?.GetAddrInfo(debugAddress, infos, debugShowInst, debugShowData);

      Span<byte> data = stackalloc byte[TOTAL_VALS];
      Current.Processor.MappedMemory.Read(data, debugAddress);

      dline.Clear();
      dline.Empty(7);
      for (var i = 0; i < VALS_PER_LINE; i++)
      {
        var col = (debugAddress + i) & 15;
        dline.Sp();
        dline.Add(col, "X2");
      }
      ImGui.Text(dline);

      var hoverStart = -1;
      var hoverLen = 0;

      if (hoverAddress >= debugAddress & hoverAddress < debugAddress + TOTAL_VALS)
      {
        var hoff = hoverAddress - debugAddress;
        var instOff = -1;
        var dataOff = -1;
        var dataLen = 0;
        for (var i = hoff; i >= 0; i--)
        {
          if (instOff == -1 && hoff - i < 8 && infos[i].Inst.HasValue)
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
          hoverLen = 8;
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
        var addr = debugAddress + lnum * VALS_PER_LINE;
        dline.Add(addr, "X6");
        dline.Sp();
        for (var i = 0; i < VALS_PER_LINE; i++)
        {
          if (offset == pcoff)
            dline.HighlightData(8, new(128, 16, 16));
          else if (offset == hoverStart)
            dline.HighlightData(hoverLen, new(128, 128, 128));
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
        var addr = debugAddress + hoverStart;
        var id = Current.Symbols?.ID(addr) ?? default;

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
        if (info.Inst.HasValue && hoverStart + 8 <= TOTAL_VALS)
        {
          var inst = Instruction.Decode(Encoding.Decode(data[hoverStart..], DataType.U64).Unsigned);
          line.Clear();
          inst.Format(ref line, Current.Symbols);
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

      hoverAddress = nextHoverOff + debugAddress;
    }

    private static void ScrollToAddr(int addr, int align = 1)
    {
      debugAddress -= debugAddress % align;
      debugAddress += addr % align;

      if (addr < debugAddress || addr >= debugAddress + TOTAL_VALS)
        debugAddress = addr - VALS_PER_LINE * 3;
      else if (debugAddress > addr - VALS_PER_LINE * 3)
        debugAddress -= (debugAddress - (addr - VALS_PER_LINE * 3) + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;
      else if (addr - TOTAL_VALS + VALS_PER_LINE * 4 > debugAddress)
        debugAddress += ((addr - TOTAL_VALS + VALS_PER_LINE * 4) - debugAddress + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;

      if (debugAddress < 0)
        debugAddress = 0;
      if (debugAddress > Processor.MAIN_MEM_SIZE - TOTAL_VALS)
      {
        debugAddress = Processor.MAIN_MEM_SIZE - TOTAL_VALS;
        if (addr % align != 0)
          debugAddress -= VALS_PER_LINE;
      }

      debugAddress -= debugAddress % align;
      debugAddress += addr % align;
    }
  }
}