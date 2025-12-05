
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private static readonly List<MacroEntry> macroStack = [];
    private static bool doMacroScroll = false;
    private static DebugSymbols macroLastSymbols;
    private static TokenIndex[] macroProducts;
    private static bool macroFromEnd = false;
    private static void DrawMacroView()
    {
      if (macroLastSymbols != (macroLastSymbols = Current.Symbols))
      {
        macroStack.Clear();
        macroProducts = null;
      }

      if (Current.Symbols == null)
      {
        ImGui.Text("Assemble source to view macro expansions");
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 400);
        return;
      }

      ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new float2(0.5f, 0.5f));
      if (ImGui.Selectable("From Source", !macroFromEnd, size: new(300f, 0f)))
      {
        macroFromEnd = false;
        macroStack.Clear();
      }
      ImGui.SameLine();
      if (ImGui.Selectable("From End", macroFromEnd, size: new(300f, 0f)))
      {
        macroFromEnd = true;
        macroStack.Clear();
      }
      ImGui.PopStyleVar();

      var debug = Current.Symbols;
      var pbuf = debug.Buffer;
      var scount = pbuf.SourceCount;

      if (macroStack.Count == 0)
        macroStack.Add(new(new(macroFromEnd ? -1 : 0), new(macroFromEnd ? 0 : -1)));

      if (macroProducts == null)
      {
        macroProducts = new TokenIndex[pbuf.TokenCount];
        void walkProducer(Token token)
        {
          if (token.Type == TokenType.EOL) return;
          var producer = debug.GetProducer(token);
          if (producer == TokenIndex.Invalid || macroProducts[producer.Index] != default)
            return;
          macroProducts[producer.Index] = token.Index;
          walkProducer(pbuf[producer]);
        }
        foreach (var token in debug.FinalTokens)
          walkProducer(token);
        for (var sindex = pbuf.SourceCount; --sindex >= 0;)
          foreach (var token in pbuf.SourceTokens(new(sindex)))
            if (macroProducts[token.Index.Index] != default)
              walkProducer(token);
      }

      var line = new LineBuilder(stackalloc char[256]);
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

      var linesBottom = ImGui.GetCursorScreenPos().Y + ImGui.GetTextLineHeightWithSpacing() + 400f;

      var trimTo = -1;
      for (var i = 0; i < macroStack.Count; i++)
      {
        line.Clear();
        if (i + 1 < macroStack.Count)
          line.Add(debug.SourceLine(macroStack[i + 1].Producer, out _, out _));
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
          var rect = ImGuiX.TextRect(new(line.Length, sname.Length), cursor);
          ImGui.SetCursorScreenPos(rect.XY);
          ImGui.PushID(i);
          if (ImGui.Button("##macroStack", rect.ZW - rect.XY))
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

      var linesHeight = linesBottom - ImGui.GetCursorScreenPos().Y;
      if (linesHeight < 200)
        linesHeight = 200;

      if (!ImGui.BeginChild("##srcLines", new(-float.Epsilon, linesHeight), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
      {
        ImGui.EndChild();
        return;
      }
      var hovering = ImGui.IsWindowHovered();
      maxWidth = ImGui.GetContentRegionAvail().X;

      var curMacro = macroStack[^1];
      var curProducer = curMacro.Source.Index > 0
        ? pbuf.Source(curMacro.Source).Producer
        : TokenIndex.Invalid;

      var hasInst = debug.InstToken(Current.Processor.PC, out var inst);
      if (hasInst && curMacro.Source.Index >= 0)
      {
        while (inst != TokenIndex.Invalid)
        {
          var itoken = pbuf[inst];
          if (itoken.Source.Index == curMacro.Source.Index)
            break;
          inst = debug.GetProducer(itoken);
        }
        hasInst = inst != TokenIndex.Invalid;
      }

      var iter = debug.SourceLineIter(curMacro.Source);
      while (iter.Next(out var lnum))
      {
        var lstart = ImGui.GetCursorScreenPos();
        var titer = iter.Tokens();
        while (titer.Next(out var token, out var range))
        {
          if (doMacroScroll && token.Index == curMacro.Producer)
          {
            ImGui.SetScrollHereY();
            doMacroScroll = false;
          }

          var rect = ImGuiX.TextRect(range, lstart);
          var urect = ImGuiX.TextUnderlineRect(range, lstart);
          if (hasInst && (inst == token.Index || inst == token.Previous))
            ImGuiX.DrawRect(ImGuiX.LineRect(lstart), PCHighlight);

          if (!hovering || mouse.X < rect.X || mouse.X >= rect.Z || mouse.Y < rect.Y || mouse.Y >= rect.W)
          {
            if (token.Index == curMacro.Producer)
              ImGuiX.DrawRect(rect, TokenHighlight);
            else if (!macroFromEnd && token.Index.Index >= 0 && macroProducts[token.Index.Index] != default)
              ImGuiX.DrawRect(urect, TokenHighlight);
            continue;
          }

          Token target = default;
          bool validTarget;
          if (macroFromEnd)
          {
            var producer = curMacro.Source.Index < 0 ? token.Previous : curProducer;
            validTarget = producer != TokenIndex.Invalid;
            if (validTarget)
              target = debug.Token(producer);
          }
          else
          {
            var product = token.Index.Index < 0 ? default : macroProducts[token.Index.Index];
            validTarget = product != default;
            if (validTarget)
              target = debug.Token(product);
          }

          if (validTarget)
          {
            ImGuiX.DrawRect(rect, TokenHoverHilight);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
              macroStack.Add(new(target.Source, target.Index));
              doMacroScroll = true;
            }
          }
          ImGui.BeginTooltip();
          if (macroFromEnd)
          {
            var otok = token;
            while (true)
            {
              line.Clear();
              line.Add(otok.Source.Index < 0 ? "final" : pbuf.SourceName(otok.Source));
              line.Add(": ");
              line.Add(debug.SourceLine(otok.Index, out _, out _));
              ImGui.Text(line.Line);
              var prod = debug.GetProducer(otok);
              if (prod == TokenIndex.Invalid)
                break;
              otok = pbuf[prod];
            }
          }
          else
          {
            var otok = token;
            while (true)
            {
              line.Clear();
              line.Add(otok.Source.Index < 0 ? "final" : pbuf.SourceName(otok.Source));
              line.Add(": ");
              line.Add(debug.SourceLine(otok.Index, out _, out _));
              ImGui.Text(line.Line);
              if (otok.Index.Index < 0 || macroProducts[otok.Index.Index] == default)
                break;
              otok = debug.Token(macroProducts[otok.Index.Index]);
            }
          }
          ImGui.EndTooltip();
        }
        line.Clear();
        iter.Build(ref line);
        ImGui.Text(line.Line);
      }

      ImGui.EndChild();
    }

    private readonly struct MacroEntry(SourceIndex source, TokenIndex producer)
    {
      public readonly SourceIndex Source = source;
      public readonly TokenIndex Producer = producer;
    }
  }
}