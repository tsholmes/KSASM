
using System;
using System.Collections.Generic;
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public class TerminalDeviceDefinition : DeviceDefinition<Terminal, TerminalDeviceDefinition>
  {
    public override ulong GetId(Terminal device) => 4;

    public override IDeviceFieldBuilder<Terminal> Build(RootDeviceFieldBuilder<Terminal> b) => b
      .Raw(
        "data",
        Terminal.TOTAL_SIZE,
        (ref v) => v.Data,
        (ref v, data) => v.Update());
  }

  public class Terminal(List<TerminalLabel> labels)
  {
    public static uint4[] CharCodes { get; } = GenCharCodes();

    public const int X_CHARS = 32;
    public const int Y_CHARS = 16;
    public const int TOTAL_SIZE = X_CHARS * Y_CHARS;

    public readonly byte[] Data = new byte[TOTAL_SIZE];

    public void Update()
    {
      for (var i = 0; i < TOTAL_SIZE; i++)
        labels[i].PackedText = CharCodes[Data[i]];
    }

    private static uint4[] GenCharCodes()
    {
      var codes = new uint4[256];
      Array.Fill(codes, new(0xFFFFFFu, 0xFFFFFFu, 0xFFFFFFu, 0xFFFFFFu));
      for (var c = '0'; c <= '9'; c++)
        codes[c].X = (uint)(c - '0');
      for (var c = 'A'; c <= 'Z'; c++)
        codes[c].X = codes[c + 'a' - 'A'].X = (uint)(c - 'A' + 10);
      codes['-'].X = 36;
      codes['.'].X = 37;
      codes['/'].X = 38;
      codes['_'].X = codes[' '].X = 39;
      for (var i = 0; i < 256; i++)
        codes[i].X |= 0xFFFFC0u;
      return codes;
    }
  }

  public class TerminalLabel : GaugeLabelReference
  {
    public static List<TerminalLabel> Labels = [];

    public uint4 PackedText = Terminal.CharCodes[0];
    public override uint4 PackData0() => PackedText;
  }
}