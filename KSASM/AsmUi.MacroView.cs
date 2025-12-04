
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

      var linesHeight = linesBottom - ImGui.GetCursorScreenPos().Y;
      if (linesHeight < 200)
        linesHeight = 200;

      if (!ImGui.BeginChild("##srcLines", new(-float.Epsilon, linesHeight), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
      {
        ImGui.EndChild();
        return;
      }

      var curMacro = macroStack[^1];
      var curProducer = curMacro.Source.Index > 0
        ? pbuf.Source(curMacro.Source).Producer
        : TokenIndex.Invalid;
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
          var rect = ImGuiX.TextRect(lstart, range);
          if (mouse.X < rect.X.X || mouse.X >= rect.Y.X || mouse.Y < rect.X.Y || mouse.Y >= rect.Y.Y)
          {
            if (token.Index == curMacro.Producer)
              ImGui.GetWindowDrawList().AddRectFilled(rect.X, rect.Y, new(64, 64, 64));
            else if (!macroFromEnd && token.Index.Index >= 0 && macroProducts[token.Index.Index] != default)
              ImGui.GetWindowDrawList().AddRectFilled(new(rect.X.X, rect.Y.Y - 2), rect.Y, new(64, 64, 64));
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
            ImGui.GetWindowDrawList().AddRectFilled(rect.X, rect.Y, new(128, 128, 128));
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
              macroStack.Add(new(target.Source, target.Index));
              doMacroScroll = true;
            }
          }
          ImGui.BeginTooltip();
          if (macroFromEnd)
          {
            var otok = curMacro.Source.Index < 0 ? pbuf[token.Previous] : token;
            while (true)
            {
              var src = pbuf.Source(otok.Source);
              line.Clear();
              line.Add(src.Name);
              line.Add(": ");
              line.Add(debug.SourceLine(otok.Index, out _, out _));
              ImGui.Text(line.Line);
              if (src.Producer == TokenIndex.Invalid)
                break;
              otok = pbuf[src.Producer];
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