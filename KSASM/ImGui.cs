
using System;
using System.Linq;
using System.Linq.Expressions;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Assembly;

namespace KSASM
{
  public ref struct DataLineView(Span<char> buffer, int dataPerLine, DebugSymbols debug = null)
  {
    private LineBuilder line = new(buffer[..128]);
    private LineBuilder valLine = new(buffer[128..]);
    private readonly int dataPerLine = dataPerLine;
    private readonly DebugSymbols debug = debug;
    private Span<char> curval = [];
    private AddrInfo info = default;
    private int dataCount = 0;
    private int infoRem = 0;
    private int infoWidthRem = 0;
    private int highlightNext = 0;
    private int highlightRem = 0;
    private ImColor8 highlightCr = default;

    public Span<char> Line => line.Line;

    public void Clear()
    {
      line.Clear();
      dataCount = 0;
    }

    public void Add<T>(T val, ReadOnlySpan<char> format) where T : ISpanFormattable =>
      line.Add(val, format);
    public void Add(ReadOnlySpan<char> chars) => line.Add(chars);
    public void Add(char c) => line.Add(c);
    public void Empty(int len) => line.Empty(len);
    public void Sp() => line.Sp();

    public void HighlightData(int count, ImColor8 cr)
    {
      if (highlightRem > 0)
        return;
      var dcount = count;
      if (dataCount + dcount > dataPerLine)
        dcount = dataPerLine - dataCount;

      highlightNext = count - dcount;
      highlightCr = cr;

      ImGuiX.DrawRect(ImGuiX.TextRect(new(line.Length, dcount * 3)), cr);
      highlightRem = dcount;
    }

    public void AddData(AddrInfo info, Span<byte> data, out floatRect rect)
    {
      var rmin = ImGui.GetCursorScreenPos() + new float2(ImGuiX.TextSizes[line.Length].X, 0);
      var rmax = rmin + ImGuiX.TextSizes[3];
      rect = new() { Min = rmin, Max = rmax };

      if (highlightNext > 0 && dataCount == 0)
        HighlightData(highlightNext, highlightCr);
      if (highlightRem > 0)
        highlightRem--;
      if (infoRem == 0 && infoWidthRem > 0)
        FillDataValue(data);
      if (infoRem == 0 && info.Inst.HasValue && data.Length >= 8)
      {
        this.info = info;
        infoRem = 8;
        var val = Encoding.Decode(data, DataType.U64);
        var inst = Instruction.Decode(val.Unsigned);
        valLine.Clear();
        inst.Format(ref valLine, debug);
        if (valLine.Length > 24)
        {
          valLine.Length = 21;
          valLine.Add("...");
        }
        else
          valLine.PadLeft(24);
        curval = valLine.Line;
      }
      else if (infoRem == 0 && info.Type.HasValue && data.Length >= info.Type.Value.SizeBytes())
      {
        this.info = info;
        infoWidthRem = info.Width.Value;
        FillDataValue(data);
      }
      if (infoRem > 0)
      {
        var dlen = curval.Length;
        if (dlen > 3) dlen = 3;
        line.Add(curval[..dlen]);
        if (dlen < 3) line.Empty(3 - dlen);
        infoRem--;
        curval = curval[dlen..];
      }
      else
      {
        Sp();
        Add(data[0], "X2");
      }
      dataCount++;
    }

    private void FillDataValue(Span<byte> data)
    {
      var type = info.Type.Value;
      if (data.Length < type.SizeBytes())
      {
        infoRem = 0;
        infoWidthRem = 0;
        return;
      }
      var val = Encoding.Decode(data, type);
      valLine.Clear();
      valLine.Add(val, type);
      var len = valLine.Length;
      valLine.Add(type);
      if (valLine.Length > type.SizeBytes() * 3)
        valLine.Length = len;
      valLine.PadLeft(type.SizeBytes() * 3);
      curval = valLine.Line;
      infoRem = type.SizeBytes();
      infoWidthRem--;
    }

    public static implicit operator ReadOnlySpan<char>(in DataLineView line) => line.Line;
    public static implicit operator ImString(in DataLineView line) => line.Line;
  }

  public ref struct LineBuilder(Span<char> line)
  {
    private readonly Span<char> line = line;
    private int length = 0;

    public int Length
    {
      get => length;
      set => length = value >= 0 && value <= length ? value : throw new IndexOutOfRangeException($"{value}");
    }

    public Span<char> Line => line[..length];

    public void Clear()
    {
      line.Fill(' ');
      length = 0;
    }

    public void Add<T>(T val, ReadOnlySpan<char> format) where T : ISpanFormattable
    {
      if (!val.TryFormat(line[length..], out int written, format, null))
        throw new InvalidOperationException();
      length += written;
    }

    public void Add(Value val, DataType type)
    {
      switch (type.VMode())
      {
        case ValueMode.Unsigned: Add(val.Unsigned, type == DataType.P24 ? "X6" : "g"); break;
        case ValueMode.Signed: Add(val.Signed, "g"); break;
        case ValueMode.Float: Add(val.Float, "g"); break;
        default: throw new InvalidOperationException($"{type.VMode()}");
      }
    }

    public void Add(DataType type)
    {
      Add(':');
      Add(type, "g");
    }

    public void Add(ReadOnlySpan<char> chars)
    {
      chars.CopyTo(line[length..]);
      length += chars.Length;
    }

    public void Add(char c) => line[length++] = c;

    public void Empty(int len) => line[length..(length += len)].Fill(' ');

    public void Sp() => Add(' ');

    public void AddAddr(int addr, DebugSymbols debug)
    {
      if (debug == null)
      {
        Add(addr, "X6");
        return;
      }

      var id = debug.ID(addr);
      if (string.IsNullOrEmpty(id.Label))
      {
        Add(addr, "X6");
        return;
      }
      Add(id.Label);
      if (id.Offset > 0)
      {
        Add('+');
        Add(id.Offset, "g");
      }
    }

    public void PadLeft(int len)
    {
      var pad = len - length;
      if (pad <= 0)
        return;
      line[..length].CopyTo(line[pad..]);
      line[..pad].Fill(' ');
      length = len;
    }
  }

  public static class ImGuiX
  {
    public static float2[] TextSizes => field ??= CalcTextSizes();
    public static float[] TextWidths => field ??= TextSizes.Select(sz => sz.X).ToArray();
    private static float2[] CalcTextSizes()
    {
      Span<char> text = stackalloc char[257];
      text.Fill('X');
      var sizes = new float2[257];
      for (var i = 1; i <= 256; i++)
        sizes[i] = ImGui.CalcTextSize(text[..i]);
      return sizes;
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

  public class InputFilter
  {
    private ImGuiTextFilter filter;
    public void Clear() => filter.Clear();
    public void Draw(ImString label = default, float width = default) => filter.Draw(label, width);
    public bool PassFilter(ImString text) => filter.PassFilter(text);
  }

  public class InputString(int capacity, ReadOnlySpan<char> initialContent = default)
  {
    public readonly ImInputString Input = new(capacity, initialContent);
    public ReadOnlySpan<char> AsString() => (ImString)Input;
  }
}