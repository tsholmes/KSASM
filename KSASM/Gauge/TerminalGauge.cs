
using System;
using System.Xml.Serialization;
using KittenExtensions;
using KSA;

namespace KSASM.Gauge
{
  [KxAssetInject(typeof(GaugeCanvas), nameof(GaugeCanvas.Components), "TermComponent")]
  public class TerminalGaugeComponent : GaugeComponent
  {
    [XmlElement("TerminalText")]
    public TermConfig Config = new();

    public override void OnDataLoad(Mod mod)
    {
      if (Config.Width * Config.Height > TerminalTextUbo.MAX_SIZE)
        throw new InvalidOperationException($"{Config.Width}x{Config.Height} > {TerminalTextUbo.MAX_SIZE}");

      Rects = [new GaugeRectReference { X = 0, Y = 0, Z = 1, W = 1 }];

      base.OnDataLoad(mod);
    }

    public override void OnFrame()
    {
      TerminalTextUbo.SetConfig(Config);
      base.OnFrame();
    }

    public class TermConfig
    {
      [XmlAttribute("Width")]
      public int Width = 32;
      [XmlAttribute("Height")]
      public int Height = 16;
      [XmlAttribute("Weight")]
      public double Weight = 0f;
      [XmlAttribute("Edge")]
      public double Edge = 0.1f;
      [XmlAttribute("Background")]
      public IndexedColor Background = IndexedColor.Black;
      [XmlAttribute("Foreground")]
      public IndexedColor Foreground = IndexedColor.White;
    }
  }
}