
namespace KSASM
{
  partial class AsmUi
  {
    private static void DrawDevView()
    {
      foreach (var device in Current.Processor.Devices)
      {
        device.OnDrawUi();
      }
    }
  }
}