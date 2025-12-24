
namespace KSASM.UI
{
  public class DevViewWindow(AsmUi parent) : DockedWindow("DevView", parent)
  {
    public override DockGroup Group => DockGroup.Memory;
    protected override void Draw()
    {
      foreach (var device in ps.Processor.Devices)
        device.OnDrawUi();
    }
  }
}