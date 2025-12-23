
using Brutal.ImGuiApi;
using Brutal.Numerics;

namespace KSASM.UI
{
  public class EditorWindow(ImGuiID dock, ProcSystem ps) : DockedWindow("Editor", dock, ps)
  {
    private const string DEFAULT_SCRIPT = """
      @0
      main:
        debug "Hello World!":s48
        sleep (-1):u64
        jump main
      """;
    private static string defaultScript = null;

    public static void SetDefaultExample(string name)
    {
      defaultScript = Library.LoadExample(name).Source;
    }

    private readonly ImInputString source = new(65536, defaultScript ?? DEFAULT_SCRIPT);

    public override DockGroup Group => DockGroup.Editor;

    protected override void Draw()
    {
      if (ImGui.Button("Assemble"))
        ps.Assemble(source.ToString());

      ImGui.SameLine();
      ImGui.SetNextItemWidth(-float.Epsilon);
      if (ImGui.BeginCombo("##Examples", "Load Example Script"))
      {
        for (var i = 0; i < Library.Examples.Count; i++)
        {
          var name = Library.Examples[i];
          ImGui.PushID(i);
          if (ImGui.Selectable(name))
            LoadExample(name);
          ImGui.PopID();
        }
        ImGui.EndCombo();
      }

      ImGui.InputTextMultiline(
        "###source",
        source,
        new float2(-float.Epsilon, -float.Epsilon),
        ImGuiInputTextFlags.None
      );
    }

    private void LoadExample(string name)
    {
      var example = Library.LoadExample(name);
      source.SetValue(example.Source);
    }
  }
}