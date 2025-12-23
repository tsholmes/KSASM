
using Brutal.ImGuiApi;

namespace KSASM.UI
{
  public class ControlsWindow(ImGuiID dock, ProcSystem ps) : DockedWindow("Controls", dock, ps)
  {
    private int lastTotal = 0;

    public override DockGroup Group => DockGroup.Logs;

    protected override void Draw()
    {
      ImGui.PushStyleVarX(ImGuiStyleVar.ItemSpacing, 1);
      if (ImGui.Button("Run")) ps.Restart();
      ImGui.SameLine();
      if (ImGui.Button("Resume")) ps.Resume();
      ImGui.SameLine();
      if (ImGui.Button("Step")) ps.SingleStep = true;
      ImGui.SameLine();
      if (ImGui.Button("Stop")) ps.Stop();
      ImGui.SameLine();

      ImGui.BeginDisabled(ps.Logs.Count == 0);
      if (ImGui.Button("Clear"))
        ps.Logs.Clear();
      ImGui.EndDisabled();

      ImGui.PopStyleVar();
      ImGui.SameLine();

      ImGui.Text($"{ps.LastSteps} steps in {ps.LastMs:0.##}ms");

      ImGui.Separator();
      ImGui.BeginChild(
        "##logs",
        new(-float.Epsilon, -float.Epsilon),
        windowFlags: ImGuiWindowFlags.HorizontalScrollbar);

      foreach (var line in ps.Logs)
        ImGui.Text(line);

      if (ps.Logs.Total > lastTotal)
        ImGui.SetScrollHereY();
      lastTotal = ps.Logs.Total;

      ImGui.Text($"{ps.Logs.Count}");

      ImGui.EndChild();
    }
  }
}