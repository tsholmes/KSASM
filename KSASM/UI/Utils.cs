
using System;
using System.Linq;
using System.Linq.Expressions;
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KSASM.UI
{
  public static class ImGuiX
  {
    public static float2[] TextSizes => field ??= CalcTextSizes();
    public static float[] TextWidths => field ??= [.. TextSizes.Select(sz => sz.X)];
    private static float2[] CalcTextSizes()
    {
      Span<char> text = stackalloc char[257];
      text.Fill('X');
      var sizes = new float2[257];
      for (var i = 1; i <= 256; i++)
        sizes[i] = ImGui.CalcTextSize(text[..i]);
      return sizes;
    }

    public static unsafe ImGuiID DockSpace(
      ImGuiID dockspaceId,
      in float2? size = null,
      ImGuiDockNodeFlags flags = ImGuiDockNodeFlags.None,
      ImGuiWindowClass? windowClass = default)
    {
      ImGuiWindowClassPtr windowClassPtr = default;
      if (windowClass != null)
      {
        var cls = windowClass.Value;
        windowClassPtr = &cls;
      }
      return ImGui.DockSpace(dockspaceId, size, flags, windowClassPtr);
    }

    public static unsafe void SetNextWindowClass(ImGuiWindowClass cls)
    {
      ImGui.SetNextWindowClass(&cls);
    }

    public static ImGuiID GetID(params Span<string> parts)
    {
      var line = new LineBuilder(stackalloc char[128]);
      for (var i = 0; i < parts.Length; i++)
        line.Add(parts[i]);
      return ImGui.GetID(line.Line);
    }

    public static float4 TextRect(FixedRange text, float2? cursor = null)
    {
      var cpos = cursor ?? ImGui.GetCursorScreenPos();
      var rmin = new float2(cpos.X + TextSizes[text.Start].X, cpos.Y);
      var rmax = new float2(rmin.X + TextSizes[text.Length].X, rmin.Y + ImGui.GetTextLineHeightWithSpacing());
      return new(rmin, rmax);
    }

    public static float4 TextUnderlineRect(FixedRange text, float2? cursor = null)
    {
      var cpos = cursor ?? ImGui.GetCursorScreenPos();
      const float UNDERLINE_HEIGHT = 2f;
      var rmin = cpos + new float2(TextWidths[text.Start], ImGui.GetTextLineHeightWithSpacing() - UNDERLINE_HEIGHT);
      var rmax = rmin + new float2(TextWidths[text.Length], UNDERLINE_HEIGHT);
      return new(rmin, rmax);
    }

    public static float4 LineRect(float2? cursor = null)
    {
      var cpos = cursor ?? ImGui.GetCursorScreenPos();
      return new(cpos, cpos + new float2(10000, ImGui.GetTextLineHeightWithSpacing()));
    }

    public static void DrawRect(float4 rect, ImColor8 color) =>
      ImGui.GetWindowDrawList().AddRectFilled(rect.XY, rect.ZW, color);

    public static bool InputEnum<T>(ImString label, ref T value) where T : struct, Enum
    {
      var changed = false;
      var line = new LineBuilder(stackalloc char[16]);

      line.Clear();
      line.Add(value, "g");
      if (ImGui.BeginCombo(label, line.Line))
      {
        var cast = Cast<T, long>.F;
        var curlval = cast(value);
        var vals = Enum.GetValues<T>();

        for (var i = 0; i < vals.Length; i++)
        {
          ImGui.PushID(i);
          line.Clear();
          line.Add(vals[i], "g");
          if (ImGui.Selectable(line.Line, cast(vals[i]) == curlval))
          {
            value = vals[i];
            changed = true;
          }
          ImGui.PopID();
        }

        ImGui.EndCombo();
      }

      return changed;
    }

    private class Cast<A, B> where A : struct where B : struct
    {
      public static readonly Func<A, B> F = Make();

      private static Func<A, B> Make()
      {
        var p = Expression.Parameter(typeof(A));
        var c = Expression.Convert(p, typeof(B));
        return Expression.Lambda<Func<A, B>>(c, p).Compile();
      }
    }
  }

  public class CharGrid(int bufSize, int width, int height)
  {
    private readonly int bufSize = bufSize;
    private readonly char[] buf = new char[bufSize];
    private readonly AppendBuffer<HighlightRange> highlights = new();
    private int width = width;
    private int height = height;
    private int hovered = -1;

    public int Width => width;
    public int Height => height;

    public void NewFrame()
    {
      Array.Fill(buf, ' ');
      highlights.Length = 0;

      var relMouse = ImGui.GetMousePos() - ImGui.GetCursorScreenPos();
      var lineHeight = ImGui.GetTextLineHeightWithSpacing();

      var row = (int)Math.Floor(relMouse.Y / lineHeight);
      var col = -1;
      for (var i = 0; i < width; i++)
      {
        var start = ImGuiX.TextWidths[i];
        var end = ImGuiX.TextWidths[i + 1];
        if (relMouse.X < start)
          break;
        if (relMouse.X < end)
        {
          col = i;
          break;
        }
      }
      if (row >= 0 && row < height && col >= 0 && col < width)
        hovered = row * width + col;
      else
        hovered = -1;
    }

    public void Resize(int width, int height)
    {
      if (width * height > bufSize)
        throw new IndexOutOfRangeException($"({width}x{height}) {width * height} > {bufSize}");
      this.width = width;
      this.height = height;
    }

    public int2 HoveredPoint()
    {
      if (hovered == -1)
        return new(-1, -1);
      return new(hovered % width, hovered / width);
    }

    public void Draw()
    {
      var cursor = ImGui.GetCursorScreenPos();
      var lineHeight = ImGui.GetTextLineHeightWithSpacing();

      foreach (var hl in highlights)
      {
        var line = hl.Range.Start / width;
        var lineRange = new FixedRange(hl.Range.Start - line * width, hl.Range.Length);
        var lineCursor = cursor + new float2(0, lineHeight * line);
        var rect = hl.Highlight.Underline
          ? ImGuiX.TextUnderlineRect(lineRange, lineCursor)
          : ImGuiX.TextRect(lineRange, lineCursor);
        ImGuiX.DrawRect(rect, hl.Highlight.Color);
      }

      for (var line = 0; line < height; line++)
      {
        var chars = buf.AsSpan(line * width, width);
        ImGui.SetCursorScreenPos(cursor + new float2(0, lineHeight * line));
        ImGui.Text(chars);
      }
    }

    public View Full => Range(new(0, 0, width, height));
    public View Range(int4 bounds)
    {
      var vhovered = -1;
      if (hovered >= 0)
      {
        var hrow = hovered / width - bounds.Y;
        var hcol = hovered % width - bounds.X;
        if (hrow >= 0 && hrow < bounds.W && hcol >= 0 && hcol < bounds.Z)
          vhovered = hrow * bounds.Z + hcol;
      }
      return new(this, bounds, vhovered);
    }

    public View this[Range rows, Range cols]
    {
      get
      {
        var (roff, rlen) = rows.GetOffsetAndLength(height);
        var (coff, clen) = cols.GetOffsetAndLength(width);
        return Range(new(coff, roff, clen, rlen));
      }
    }

    private struct HighlightRange
    {
      public Highlight Highlight;
      public FixedRange Range;
    }

    public struct Highlight
    {
      public ImColor8 Color;
      public bool Underline;
    }

    public struct View(CharGrid grid, int4 bounds, int hovered)
    {
      private readonly CharGrid grid = grid;
      private readonly int4 bounds = bounds;
      private readonly int hovered = hovered;
      private int cursor = 0;

      public bool NextHovered(int length) => hovered >= cursor && hovered < cursor + length;

      public void AddHighlight(int length, ImColor8 color, bool underline = false)
      {
        var hcursor = cursor;
        var hl = new Highlight { Color = color, Underline = underline };
        while (length > 0)
        {
          var range = Next(length, hcursor);
          grid.highlights.Add(new() { Highlight = hl, Range = range });
          hcursor += range.Length;
          length -= range.Length;
        }
      }

      public Highlight? HoveredHighlight(int length, ImColor8 color, bool underline = false) =>
        NextHovered(length) ? new Highlight { Color = color, Underline = underline } : null;

      public void Add(ReadOnlySpan<char> chars, Highlight? highlight = null)
      {
        while (chars.Length > 0)
        {
          var range = Next(chars.Length, cursor);

          chars[..range.Length].CopyTo(grid.buf.AsSpan(range.Start, range.Length));
          if (highlight is Highlight hl)
            grid.highlights.Add(new() { Highlight = hl, Range = range });

          chars = chars[range.Length..];
          cursor += range.Length;
        }
      }

      private FixedRange Next(int maxLength, int cur)
      {
        var vrow = cur / bounds.Z;
        var vcol = cur % bounds.Z;
        if (vrow >= bounds.W)
          throw new IndexOutOfRangeException(
            $"{cur} ({vcol},{vrow}) >= {bounds.Z * bounds.W} ({bounds.Z}x{bounds.W})");
        var len = Math.Min(bounds.Z - vcol, maxLength);
        var idx = (vrow + bounds.Y) * grid.width + vcol + bounds.X;
        return new(idx, len);
      }

      public void AddF<T>(T val, ReadOnlySpan<char> fmt, Highlight? highlight = null)
        where T : ISpanFormattable
      {
        Span<char> chars = stackalloc char[64];
        val.TryFormat(chars, out var length, fmt, null);
        Add(chars[..length], highlight);
      }

      public void Nl() => cursor += bounds.Z - (cursor % bounds.Z);
    }
  }
}