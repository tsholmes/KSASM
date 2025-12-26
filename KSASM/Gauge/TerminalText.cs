
using System;
using Brutal.Numerics;
using KSA;

namespace KSASM.Gauge
{
  public class TerminalText : GaugeRectReference
  {
    public const int MAX_CHARS = 16;

    public int CharCount = MAX_CHARS;
    public IndexedColor Background = IndexedColor.Black;
    public IndexedColor Foreground = IndexedColor.White;
    public double Weight = 0f;
    public double Edge = 0f;

    private uint4 curData = new();

    public void SetData(ReadOnlySpan<byte> data)
    {
      var res = new uint4();
      for (var i = 0; i < MAX_CHARS; i++)
      {
        var b = i < data.Length ? data[i] : (byte)0;
        var shift = (i & 3) << 3;
        var idx = (i >> 2) & 3;
        res[idx] |= (uint)b << shift;
      }
      curData = res;
    }

    public override uint4 PackData0() => curData;
    public override uint4 PackData1() => new(Pack1X(), Pack1Y(), 0, 0);

    private uint Pack1X() => 0
      | (((uint)CharCount - 1) << 0)
      | ((uint)Background << 4)
      | ((uint)Foreground << 8);
    
    private uint Pack1Y()
    {
      var sweight = Math.Clamp((Weight + 1.0) / 2.0, 0, 1) * ushort.MaxValue;
      var sedge = Math.Clamp(Edge, 0, 1) * ushort.MaxValue;
      return 0
        | ((uint)sweight << 0)
        | ((uint)sedge << 16);
    }
  }
}