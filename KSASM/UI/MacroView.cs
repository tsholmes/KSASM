
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Assembly;

namespace KSASM.UI
{
  public class MacroViewWindow(AsmUi parent) : DockedWindow("MacroView", parent)
  {
    private readonly List<MacroEntry> macroStack = [];
    private bool doScroll = false;
    private DebugSymbols lastSymbols;
    private TokenIndex[] products;
    private bool fromEnd = false;

    public override DockGroup Group => DockGroup.Editor;
    protected override void Draw()
    {
      if (lastSymbols != (lastSymbols = ps.Symbols))
      {
        macroStack.Clear();
        products = null;
      }

      if (ps.Symbols == null)
      {
        ImGui.Text("Assemble source to view macro expansions");
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 400);
        ImGui.Dummy(new());
        return;
      }

      var maxWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().WindowPadding.X;

      ImGui.PushStyleVar(ImGuiStyleVar.SelectableTextAlign, new float2(0.5f, 0.5f));
      if (ImGui.Selectable("From Source", !fromEnd, size: new(maxWidth / 2, 0f)))
      {
        fromEnd = false;
        macroStack.Clear();
      }
      ImGui.SameLine();
      if (ImGui.Selectable("From End", fromEnd, size: new(maxWidth / 2, 0f)))
      {
        fromEnd = true;
        macroStack.Clear();
      }
      ImGui.PopStyleVar();

      var debug = ps.Symbols;
      var pbuf = debug.Buffer;
      var scount = pbuf.SourceCount;

      if (macroStack.Count == 0)
        macroStack.Add(new(new(fromEnd ? -1 : 0), new(fromEnd ? 0 : -1)));

      if (products == null)
      {
        products = new TokenIndex[pbuf.TokenCount];
        void walkProducer(Token token)
        {
          if (token.Type == TokenType.EOL) return;
          var producer = debug.GetProducer(token);
          if (producer == TokenIndex.Invalid || products[producer.Index] != default)
            return;
          products[producer.Index] = token.Index;
          walkProducer(pbuf[producer]);
        }
        foreach (var token in debug.FinalTokens)
          walkProducer(token);
        for (var sindex = pbuf.SourceCount; --sindex >= 0;)
          foreach (var token in pbuf.SourceTokens(new(sindex)))
            if (products[token.Index.Index] != default)
              walkProducer(token);
      }

      var line = new LineBuilder(stackalloc char[256]);
      var mouse = ImGui.GetMousePos();
      int maxChars = 0;
      for (var i = 1; i <= 256; i++)
      {
        if (ImGuiX.TextWidths[i] <= maxWidth)
          maxChars = i;
        else
          break;
      }

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

      if (!ImGui.BeginChild("##srcLines", new(-float.Epsilon, -float.Epsilon), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
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

      var hasInst = debug.InstToken(ps.Processor.PC, out var inst);
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
          if (doScroll && token.Index == curMacro.Producer)
          {
            ImGui.SetScrollHereY();
            doScroll = false;
          }

          var rect = ImGuiX.TextRect(range, lstart);
          var urect = ImGuiX.TextUnderlineRect(range, lstart);
          if (hasInst && (inst == token.Index || inst == token.Previous))
            ImGuiX.DrawRect(ImGuiX.LineRect(lstart), AsmUi.PCHighlight);

          if (!hovering || mouse.X < rect.X || mouse.X >= rect.Z || mouse.Y < rect.Y || mouse.Y >= rect.W)
          {
            if (token.Index == curMacro.Producer)
              ImGuiX.DrawRect(rect, AsmUi.TokenHighlight);
            else if (!fromEnd && token.Index.Index >= 0 && products[token.Index.Index] != default)
              ImGuiX.DrawRect(urect, AsmUi.TokenHighlight);
            continue;
          }

          Token target = default;
          bool validTarget;
          if (fromEnd)
          {
            var producer = curMacro.Source.Index < 0 ? token.Previous : curProducer;
            validTarget = producer != TokenIndex.Invalid;
            if (validTarget)
              target = debug.Token(producer);
          }
          else
          {
            var product = token.Index.Index < 0 ? default : products[token.Index.Index];
            validTarget = product != default;
            if (validTarget)
              target = debug.Token(product);
          }

          if (validTarget)
          {
            ImGuiX.DrawRect(rect, AsmUi.TokenHoverHilight);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
              macroStack.Add(new(target.Source, target.Index));
              doScroll = true;
            }
          }
          ImGui.BeginTooltip();
          if (fromEnd)
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
              if (otok.Index.Index < 0 || products[otok.Index.Index] == default)
                break;
              otok = debug.Token(products[otok.Index.Index]);
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