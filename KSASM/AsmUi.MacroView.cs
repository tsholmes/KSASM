
using Brutal.ImGuiApi;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private static void DrawMacroView()
    {
      var pbuf = Current.Symbols?.Buffer;
      var scount = pbuf?.SourceCount ?? 0;

      if (!ImGui.BeginChild("##srcLines", new(-float.Epsilon, 400f), windowFlags: ImGuiWindowFlags.HorizontalScrollbar))
      {
        ImGui.EndChild();
        return;
      }

      /*
      TODO:
      - use negative indexes for final tokens
      - build forward index of token to first product (use 0 for not visited since 0 can't have a producer)
        - iterate through final tokens
        - if producer isn't visited, mark this as its product and move to producer
      - have 2 modes:
        - start from source 0 (script). click tokens to walk forward to first product
        - start from FinalTokens. click tokens to walk backwards to producer
      - when not at start, highlight producer/product token
      - clickable breadcrumbs at top to go back (start from left on source0 mode, right on FinalTokens mode)
      */

      var line = new LineBuilder(stackalloc char[256]);
      var hovering = ImGui.IsWindowHovered();
      var mouse = ImGui.GetMousePos();

      var iter = Current.Symbols?.SourceLineIter(new(-1)) ?? default;
      var lheight = ImGui.GetTextLineHeight();
      while (iter.Next())
      {
        var lstart = ImGui.GetCursorScreenPos();
        var hoverLine = hovering && mouse.Y >= lstart.Y && mouse.Y < lstart.Y + lheight;

        var titer = iter.Tokens();
        while (hoverLine && titer.Next(out var token, out var range))
        {
          var tstart = lstart.X + ImGuiX.TextSizes[range.Start].X;
          var tend = lstart.X + ImGuiX.TextSizes[range.End].X;
          if (mouse.X < tstart || mouse.X >= tend)
            continue;

          ImGui.GetWindowDrawList().AddRectFilled(new(tstart, lstart.Y), new(tend, lstart.Y + lheight), new(128, 128, 128));
          var otok = token;
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
  }
}