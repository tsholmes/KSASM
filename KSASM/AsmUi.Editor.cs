
using System;
using System.Diagnostics;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private static readonly byte[] buffer = new byte[65536];

    private static void DrawEditor()
    {
      if (ImGui.BeginCombo("##Library", "Load Library Script"))
      {
        for (var i = 0; i < Library.Index.Count; i++)
        {
          var name = Library.Index[i];
          ImGui.PushID(i);
          if (ImGui.Selectable(name))
            LoadLibrary(name);
          ImGui.PopID();
        }
        ImGui.EndCombo();
      }

      ImGui.InputTextMultiline(
        "###source",
        buffer.AsSpan(),
        new float2(600, 400),
        ImGuiInputTextFlags.None
      );
      isTyping = ImGui.IsItemActive();

      if (ImGui.Button("Assemble##asm"))
        Assemble();
    }

    private static void Assemble()
    {
      Current.Reset();
      var stopwatch = Stopwatch.StartNew();
      try
      {
        var len = buffer.IndexOf((byte)0);
        var source = System.Text.Encoding.ASCII.GetString(buffer, 0, len);

        Current.Symbols = Assembler.Assemble(new("script", source), Current.Processor.Memory);

        stopwatch.Stop();
        AddOutput($"Assembled in {stopwatch.Elapsed.Milliseconds:0.##}ms");
      }
      catch (Exception ex)
      {
        AddOutput(ex.ToString());
      }
    }

    public static void LoadLibrary(string name)
    {
      var source = Library.LoadImport(name);
      var sbytes = System.Text.Encoding.ASCII.GetBytes(source.Source);
      sbytes.CopyTo(buffer);
      buffer[sbytes.Length] = 0;
    }
  }
}