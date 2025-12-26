
using System;

namespace KSASM.Gauge
{
  public class TerminalBinding
  {
    // just a single global terminal for now
    private static readonly byte[] curData = new byte[16 * 32];

    public static void SetData(ReadOnlySpan<byte> data)
    {
      Array.Fill(curData, (byte)0);
      data.CopyTo(curData);
    }

    public ReadOnlySpan<byte> GetData() => curData;
  }
}