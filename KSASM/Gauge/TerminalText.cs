
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using KittenExtensions;
using KSA;

namespace KSASM.Gauge
{
  [KxUniformBuffer("TerminalText")]
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct TerminalTextUbo
  {
    public const int MAX_SIZE = 32 * 16;
    [KxUniformBufferLookup]
    public static KxPtrLookup<TerminalTextUbo> Lookup;

    private static unsafe TerminalTextUbo* ptr;

    public static unsafe void SetConfig(TerminalGaugeComponent.TermConfig config)
    {
      if (ptr == null)
        ptr = Lookup(KeyHash.Make("TerminalText"));

      ptr->Width = (uint)config.Width;
      ptr->Height = (uint)config.Height;
      ptr->Weight = (uint)config.Weight;
      ptr->Edge = (uint)config.Edge;
      ptr->Background = (uint)config.Background;
      ptr->Foreground = (uint)config.Foreground;
    }

    public static unsafe void SetData(ReadOnlySpan<byte> data)
    {
      if (ptr == null)
        ptr = Lookup(KeyHash.Make("TerminalText"));

      data.CopyTo(ptr->Data);
    }

    public CharData Data;
    public uint Width;
    public uint Height;
    public float Weight;
    public float Edge;
    public uint Background;
    public uint Foreground;

    [InlineArray(MAX_SIZE)]
    public struct CharData { private byte _element; }
  }
}