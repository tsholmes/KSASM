
using System;
using System.Xml.Serialization;
using KittenExtensions;
using KSA;

namespace KSASM.Gauge
{
  [KxAssetInject(typeof(GaugeCanvas), nameof(GaugeCanvas.Components), "TermComponent")]
  public class TerminalGaugeComponent : GaugeComponent
  {
    [XmlElement("TermConfig")]
    public TermConfig Config = new();

    [XmlElement("Binding")]
    public TerminalBinding Binding = new();

    public override void OnDataLoad(Mod mod)
    {
      if (Config.AutoLayout)
      {
        Rects.Clear();

        for (var row = 0; row < Config.Height; row++)
        {
          var offset = 0;
          while (offset < Config.Width)
          {
            var len = Math.Min(Config.Width - offset, TerminalText.MAX_CHARS);
            Rects.Add(new TerminalText
            {
              CharCount = len,
              Weight = Config.Weight,
              Edge = Config.Edge,
              Background = Config.Background,
              Foreground = Config.Foreground,
              X = (double)offset / Config.Width,
              Z = (double)(offset + len) / Config.Width,
              Y = (double)row / Config.Height,
              W = (double)(row + 1) / Config.Height,
            });
            offset += len;
          }
        }
      }
      base.OnDataLoad(mod);
    }

    public override void OnFrame()
    {
      var data = Binding.GetData();
      foreach (var rect in Rects)
      {
        if (rect is not TerminalText text)
          continue;

        var len = Math.Min(data.Length, text.CharCount);
        text.SetData(data[..len]);
        data = data[len..];
      }

      base.OnFrame();
    }

    public class TermConfig
    {
      [XmlAttribute("Width")]
      public int Width = 32;
      [XmlAttribute("TermHeight")]
      public int Height = 16;
      [XmlAttribute("TermAutoLayout")]
      public bool AutoLayout = true;
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