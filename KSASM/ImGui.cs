
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Assembly;

namespace KSASM
{
  public ref struct DataLineView(Span<char> line, Span<char> valbuf, DebugSymbols debug = null)
  {
    private LineBuilder line = new(line);
    private readonly Span<char> valbuf = valbuf;
    private readonly DebugSymbols debug = debug;
    private Span<char> curval = [];
    private AddrInfo info = default;
    private int dataCount = 0;
    private int infoRem = 0;
    private int infoWidthRem = 0;
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

    public void HighlightData(int count, int maxCount, ImColor8 cr)
    {
      var dcount = count;
      if (dataCount + dcount > maxCount)
        dcount = maxCount - dataCount;

      highlightRem = count - dcount;
      highlightCr = cr;

      var drawList = ImGui.GetWindowDrawList();
      var pos = ImGui.GetCursorScreenPos();
      pos.X += TextSizes[line.Length].X;

      var ccount = dcount * 3 - 1;
      drawList.AddRectFilled(pos, pos + TextSizes[ccount], cr);
    }

    public void AddData(AddrInfo info, Span<byte> data)
    {
      if (highlightRem > 0)
        HighlightData(highlightRem, highlightRem, highlightCr);
      if (infoRem == 0 && infoWidthRem > 0)
        FillDataValue(data);
      if (infoRem == 0 && info.Inst.HasValue && data.Length >= 8)
      {
        this.info = info;
        infoRem = 8;
        var val = Encoding.Decode(data, DataType.U64);
        var inst = Instruction.Decode(val.Unsigned);
        curval = valbuf[..23];
        var len = inst.Format(curval, debug);
        curval = curval[..len];
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
        return;
      }
      Add(data[0], "X2");
      Sp();
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
      int len;
      var valid = type.VMode() switch
      {
        ValueMode.Unsigned => val.Unsigned.TryFormat(valbuf, out len, type == DataType.P24 ? "X6" : "g"),
        ValueMode.Signed => val.Signed.TryFormat(valbuf, out len),
        ValueMode.Float => val.Float.TryFormat(valbuf, out len),
        _ => throw new InvalidOperationException($"{type.VMode()}"),
      };
      if (!valid)
        throw new InvalidOperationException();
      valbuf[len++] = ':';
      if (!Enum.TryFormat(type, valbuf[len..], out var elen, "g"))
        throw new InvalidOperationException();
      if (len + elen <= (type.SizeBytes() * 3 - 1))
        len += elen;
      else
        len--;
      curval = valbuf[..len];
      infoRem = type.SizeBytes();
      infoWidthRem--;
    }

    public static implicit operator ReadOnlySpan<char>(in DataLineView line) => line.Line;
    public static implicit operator ImString(in DataLineView line) => line.Line;

    private static float2[] TextSizes => field ??= CalcTextSizes();
    private static float2[] CalcTextSizes()
    {
      Span<char> text = stackalloc char[257];
      text.Fill('X');
      var sizes = new float2[257];
      for (var i = 1; i <= 256; i++)
        sizes[i] = ImGui.CalcTextSize(text[..i]);
      return sizes;
    }
  }

  public ref struct LineBuilder(Span<char> line)
  {
    private readonly Span<char> line = line;
    private int length = 0;

    public int Length => length;

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
  }
}