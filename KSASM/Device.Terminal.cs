
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Gauge;

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

  public class GaugeTerminal : ITerminal
  {
    public byte[] Data { get; } = new byte[ITerminal.TOTAL_SIZE];

    public void Update()
    {
      TerminalBinding.SetData(Data);
    }
  }
}