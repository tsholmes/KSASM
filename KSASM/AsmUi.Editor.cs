
using System;
using System.Diagnostics;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using KSASM.Assembly;

namespace KSASM
{
  public static partial class AsmUi
  {
    private const string DEFAULT_SCRIPT = """
      .region CONST 65536
      .region maindata 1024

      :u8 hello: "Hello World!" hello.end:

      @0
      main:
        debugstr:p24 $(hello), $(hello.end-hello)
        sleep:u64 _, $(-1)
        jump:p24 _, main
      """;
    private static InputString editorInput;
    private static void DrawEditor()
    {
      editorInput ??= new(65536, DEFAULT_SCRIPT);
      if (ImGui.Button("Assemble##asm"))
        Assemble();
      ImGui.SameLine();
      ImGui.SetNextItemWidth(-float.Epsilon);
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
        editorInput.Input,
        new float2(-float.Epsilon, -float.Epsilon),
        ImGuiInputTextFlags.None
      );
    }

    private static void Assemble()
    {
      var input = editorInput.Input;
      Current.Reset();
      var stopwatch = Stopwatch.StartNew();
      try
      {
        Current.Symbols = Assembler.Assemble(new("script", input.ToString()), Current.Processor.Memory);

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
      editorInput = new(65536, source.Source);
    }
  }
}