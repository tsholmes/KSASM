
using System;
using System.Collections.Generic;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSA;

namespace KSASM
{
  public class TerminalDeviceDefinition : DeviceDefinition<ITerminal, TerminalDeviceDefinition>
  {
    public override ulong GetId(ITerminal device) => 4;

    public override IDeviceFieldBuilder<ITerminal> Build(RootDeviceFieldBuilder<ITerminal> b) => b
      .Raw(
        "data",
        ITerminal.TOTAL_SIZE,
        (ref v) => v.Data,
        (ref v, data) => v.Update());
  }

  public interface ITerminal
  {
    public const int X_CHARS = 32;
    public const int Y_CHARS = 16;
    public const int TOTAL_SIZE = X_CHARS * Y_CHARS;

    public byte[] Data { get; }
    public void Update();
  }

  public class ImGuiTerminal() : ITerminal
  {
    public byte[] Data { get; } = new byte[ITerminal.TOTAL_SIZE];
    public void Update() { }

    public void DrawUi()
    {
      ImGui.SetNextWindowPos(new float2(1000, 200), ImGuiCond.Appearing);
      ImGui.Begin("Terminal",
        ImGuiWindowFlags.NoResize |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoDocking);

      const char SPACE = ' ';

      Span<char> line = stackalloc char[ITerminal.X_CHARS];
      for (var row = 0; row < ITerminal.Y_CHARS; row++)
      {
        for (var col = 0; col < ITerminal.X_CHARS; col++)
        {
          var c = (char)Data[row * ITerminal.X_CHARS + col];
          line[col] = c switch
          {
            _ when char.IsWhiteSpace(c) => SPACE,
            _ when char.IsControl(c) => SPACE,
            (char)0 => SPACE,
            _ => c,
          };
        }
        ImGui.Text(line);
      }

      ImGui.End();
    }
  }

  public class GaugeTerminal(List<TerminalLabel> labels) : ITerminal
  {
    public static uint4[] CharCodes { get; } = GenCharCodes();

    public byte[] Data { get; } = new byte[ITerminal.TOTAL_SIZE];

    public void Update()
    {
      for (var i = 0; i < ITerminal.TOTAL_SIZE; i++)
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

    public uint4 PackedText = GaugeTerminal.CharCodes[0];
    public override uint4 PackData0() => PackedText;
  }
}