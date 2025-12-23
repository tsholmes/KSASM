
using Brutal.ImGuiApi;

namespace KSASM.UI
{
  public class DevViewWindow(ImGuiID dock, ProcSystem ps) : DockedWindow("DevView", dock, ps)
  {
    public override DockGroup Group => DockGroup.Memory;
    protected override void Draw()
    {
      foreach (var device in ps.Processor.Devices)
        device.OnDrawUi();
    }
  }
}