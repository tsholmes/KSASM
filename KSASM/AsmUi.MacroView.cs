
using System.Collections.Generic;
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private static readonly List<MacroEntry> macroStack = [];
    private static bool doMacroScroll = false;
    private static DebugSymbols macroLastSymbols;
    private static void DrawMacroView()
    {
      var pbuf = Current.Symbols?.Buffer;
      var scount = pbuf?.SourceCount ?? 0;

      if (macroLastSymbols != (macroLastSymbols = Current.Symbols))
        macroStack.Clear();

      if (macroStack.Count == 0)
        macroStack.Add(new(new(-1), new(default, default, default, default, TokenIndex.Invalid)));

      /*
      TODO:
      - build forward index of token to first product (use 0 for not visited since 0 can't have a producer)
        - iterate through final tokens
        - if producer isn't visited, mark this as its product and move to producer
      - have 2 modes:
        - start from source 0 (script). click tokens to walk forward to first product
        X start from FinalTokens. click tokens to walk backwards to producer
      */

      var line = new LineBuilder(stackalloc char[256]);
      var hovering = ImGui.IsWindowHovered();
      var mouse = ImGui.GetMousePos();
      var maxWidth = ImGui.GetContentRegionAvail().X;
      int maxChars = 0;
      for (var i = 1; i <= 256; i++)
      {
        if (ImGuiX.TextWidths[i] <= maxWidth)
          maxChars = i - 1;
        else
          break;
      }

      var trimTo = -1;
      for (var i = 0; i < macroStack.Count && Current.Symbols != null; i++)
      {
        line.Clear();
        if (i + 1 < macroStack.Count)
          line.Add(Current.Symbols.SourceLine(macroStack[i + 1].Producer.Index, out _, out _));
        var macro = macroStack[i];
        var sname = macro.Source.Index < 0 ? "final" : pbuf.SourceName(macro.Source);
        if (line.Length > maxChars - sname.Length - 1)
        {
          line.Length = maxChars - sname.Length - 4;
          line.Add("...");
        }
        line.Empty(maxChars - line.Length - sname.Length);

        if (i < macroStack.Count - 1)
        {
          var cursor = ImGui.GetCursorScreenPos();
          var rect = ImGuiX.TextRect(cursor, new(line.Length, sname.Length));
          ImGui.SetCursorScreenPos(rect.X);
          ImGui.PushID(i);
          if (ImGui.Button("##macroStack", rect.Y - rect.X))
            trimTo = i + 1;
          ImGui.PopID();
          ImGui.SetCursorScreenPos(cursor);
        }

        line.Add(sname);
        ImGui.Text(line.Line);
      }
      ImGui.Separator();

      while (trimTo > 0 && macroStack.Count > trimTo)
        macroStack.RemoveAt(macroStack.Count - 1);

      if (!ImGui.BeginChild("##srcLines", new(-float.Epsilon, 400f), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
      {
        ImGui.EndChild();
        return;
      }

      var curMacro = macroStack[^1];
      var curProducer = curMacro.Source.Index > 0
        ? Current.Symbols?.Buffer.Source(curMacro.Source).Producer ?? TokenIndex.Invalid
        : TokenIndex.Invalid;
      var iter = Current.Symbols?.SourceLineIter(curMacro.Source) ?? default;
      while (iter.Next(out var lnum))
      {
        var lstart = ImGui.GetCursorScreenPos();
        var titer = iter.Tokens();
        while (titer.Next(out var token, out var range))
        {
          if (doMacroScroll && token.Index == curMacro.Producer.Previous)
          {
            ImGui.SetScrollHereY();
            doMacroScroll = false;
          }
          var rect = ImGuiX.TextRect(lstart, range);
          if (mouse.X < rect.X.X || mouse.X >= rect.Y.X || mouse.Y < rect.X.Y || mouse.Y >= rect.Y.Y)
          {
            if (token.Index == curMacro.Producer.Previous)
              ImGui.GetWindowDrawList().AddRectFilled(rect.X, rect.Y, new(64, 64, 64));
            continue;
          }

          var producer = curMacro.Source.Index < 0 ? token.Previous : curProducer;
          if (producer == TokenIndex.Invalid)
            continue;

          if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
          {
            macroStack.Add(new(pbuf[producer].Source, token));
            doMacroScroll = true;
          }

          ImGui.GetWindowDrawList().AddRectFilled(rect.X, rect.Y, new(128, 128, 128));
          var otok = curMacro.Source.Index < 0 ? pbuf[token.Previous] : token;
          ImGui.BeginTooltip();
          while (true)
          {
            var src = pbuf.Source(otok.Source);
            line.Clear();
            line.Add(src.Name);
            line.Add(": ");
            line.Add(Current.Symbols.SourceLine(otok.Index, out _, out _));
            ImGui.Text(line.Line);
            if (src.Producer == TokenIndex.Invalid)
              break;
            otok = pbuf[src.Producer];
          }
          ImGui.EndTooltip();
        }
        line.Clear();
        iter.Build(ref line);
        ImGui.Text(line.Line);
      }

      ImGui.EndChild();
    }

    private readonly struct MacroEntry(SourceIndex source, Token producer)
    {
      public readonly SourceIndex Source = source;
      public readonly Token Producer = producer;
    }
  }
}