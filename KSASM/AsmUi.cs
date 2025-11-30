
using HarmonyLib;
using KSA;
using Brutal.ImGuiApi;
using System;
using Brutal.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using Brutal.ImGuiApi.Abstractions;
using System.Linq;
using KSASM.Assembly;
using System.Text;

namespace KSASM
{
  [HarmonyPatch]
  public static class AsmUi
  {
    [HarmonyPatch(typeof(Vehicle), nameof(Vehicle.OnDrawUi)), HarmonyPrefix]
    public static void Vehicle_OnDrawUi_Postfix(ref bool __result, Vehicle __instance, Viewport inViewport)
    {
      __result |= DrawUi(__instance, inViewport);
    }

    [HarmonyPatch(typeof(ConsoleWindowEx), nameof(ConsoleWindowEx.OnKey)), HarmonyPrefix]
    public static bool ConsoleWindowEx_OnKey_Prefix(ref bool __result, ConsoleWindow console)
    {
      if (!console.IsOpen && isTyping)
      {
        __result = true;
        return false;
      }
      return true;
    }

    [HarmonyPatch(typeof(ModLibrary), nameof(ModLibrary.LoadAll)), HarmonyPostfix]
    public static void ModLibrary_LoadAll_Suffix()
    {
      terminalCanvas = ModLibrary.Get<GaugeCanvas>("KSASMTerminal");
      var labelsComp = terminalCanvas.Components.First(c => c.Name == "Labels");

      const float EDGE_PAD = 0.025f;
      const float SCALE = 0.9f;

      static float pos(int i, int len) => EDGE_PAD + (1 - 2 * EDGE_PAD) * ((float)i / (float)len);

      for (var cy = 0; cy < Terminal.Y_CHARS; cy++)
      {
        var y0 = pos(cy, Terminal.Y_CHARS);
        var y1 = pos(cy + 1, Terminal.Y_CHARS);
        for (var cx = 0; cx < Terminal.X_CHARS; cx++)
        {
          var x0 = pos(cx, Terminal.X_CHARS);
          var x1 = pos(cx + 1, Terminal.X_CHARS);

          var label = new TerminalLabel
          {
            X = x0,
            Y = y0,
            Width = x1 - x0,
            Height = y1 - y0,
            TextColor = IndexedColor.White,
            BackgroundColor = IndexedColor.DarkGrey,
            TextScale = SCALE,
            Label = " "
          };
          labelsComp.Rects.Add(label);
          terminalLabels.Add(label);
        }
      }

      labelsComp.OnDataLoad(labelsComp.Mod);
    }

    private const ImGuiWindowFlags WINDOW_FLAGS =
      ImGuiWindowFlags.NoResize |
      ImGuiWindowFlags.NoScrollbar |
      ImGuiWindowFlags.NoCollapse |
      ImGuiWindowFlags.AlwaysAutoResize |
      ImGuiWindowFlags.NoSavedSettings;

    private static readonly byte[] buffer = new byte[65536];
    private static string stats = "";
    private static readonly List<string> output = [];

    private static bool isTyping = false;
    private static bool doStep = false;

    public static ProcSystem Current { get; private set; }

    private static GaugeCanvas terminalCanvas;
    private static readonly List<TerminalLabel> terminalLabels = [];

    public static bool DrawUi(Vehicle vehicle, Viewport inViewport)
    {
      if (vehicle != KSA.Program.ControlledVehicle)
        return false;

      isTyping = false;
      Step(vehicle);

      if (!ImGui.Begin($"KSASM##KSASM-{vehicle.Id}", WINDOW_FLAGS))
      {
        ImGui.End();
        return false;
      }

      if (ImGui.BeginTabBar("##tabs"))
      {
        if (ImGui.BeginTabItem("Editor##editor"))
        {
          DrawEditor();
          ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("Debug##debug"))
        {
          DrawDebug();
          ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
      }

      DrawControls();

      ImGui.End();
      return false;
    }

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

    private const int VALS_PER_LINE = 16;
    private const int VAL_LINES = 16;
    private const int TOTAL_VALS = VALS_PER_LINE * VAL_LINES;
    private static int debugAddress = 0;
    private static bool debugPC = true;
    private static int hoverAddress = -1;
    private static void ScrollToAddr(int addr, int align = 1)
    {
      debugAddress -= debugAddress % align;
      debugAddress += addr % align;

      if (addr < debugAddress || addr >= debugAddress + TOTAL_VALS)
        debugAddress = addr - VALS_PER_LINE * 3;
      else if (debugAddress > addr - VALS_PER_LINE * 3)
        debugAddress -= (debugAddress - (addr - VALS_PER_LINE * 3) + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;
      else if (addr - TOTAL_VALS + VALS_PER_LINE * 4 > debugAddress)
        debugAddress += ((addr - TOTAL_VALS + VALS_PER_LINE * 4) - debugAddress + VALS_PER_LINE - 1) / VALS_PER_LINE * VALS_PER_LINE;

      if (debugAddress < 0)
        debugAddress = 0;
      if (debugAddress > Processor.MAIN_MEM_SIZE - TOTAL_VALS)
      {
        debugAddress = Processor.MAIN_MEM_SIZE - TOTAL_VALS;
        if (addr % align != 0)
          debugAddress -= VALS_PER_LINE;
      }

      debugAddress -= debugAddress % align;
      debugAddress += addr % align;
    }
    private static void DrawDebug()
    {
      if (debugPC)
        ScrollToAddr(Current.Processor.PC, 8);

      var pcoff = Current.Processor.PC - debugAddress;

      Span<char> lineBuf = stackalloc char[512];
      var dline = new DataLineView(lineBuf, VALS_PER_LINE, Current.Symbols);
      var line = new LineBuilder(lineBuf);

      Span<AddrInfo> infos = stackalloc AddrInfo[TOTAL_VALS];
      Current.Symbols?.GetAddrInfo(debugAddress, infos);

      Span<byte> data = stackalloc byte[TOTAL_VALS];
      Current.Processor.MappedMemory.Read(data, debugAddress);

      dline.Clear();
      dline.Empty(7);
      for (var i = 0; i < VALS_PER_LINE; i++)
      {
        dline.Sp();
        dline.Add(i, "X2");
      }
      ImGui.Text(dline);

      var hoverStart = -1;
      var hoverLen = 0;

      if (hoverAddress >= debugAddress & hoverAddress < debugAddress + TOTAL_VALS)
      {
        var hoff = hoverAddress - debugAddress;
        var instOff = -1;
        var dataOff = -1;
        var dataLen = 0;
        for (var i = hoff; i >= 0; i--)
        {
          if (instOff == -1 && hoff - i < 8 && infos[i].Inst.HasValue)
            instOff = i;
          if (dataOff == -1 && infos[i].Type.HasValue)
          {
            var dlen = infos[i].Type.Value.SizeBytes() * infos[i].Width.Value;
            if (hoff - i < dlen)
            {
              dataOff = i;
              dataLen = dlen;
            }
          }
        }
        if (instOff >= 0)
        {
          hoverStart = instOff;
          hoverLen = 8;
        }
        else if (dataOff >= 0)
        {
          hoverStart = dataOff;
          hoverLen = dataLen;
        }
        else
        {
          hoverStart = hoff;
          hoverLen = 1;
        }
      }

      var mouse = ImGui.GetMousePos();

      var offset = 0;
      var nextHoverOff = -1;
      for (var lnum = 0; lnum < VAL_LINES; lnum++)
      {
        dline.Clear();
        var addr = debugAddress + lnum * VALS_PER_LINE;
        dline.Add(addr, "X6");
        dline.Sp();
        for (var i = 0; i < VALS_PER_LINE; i++)
        {
          if (offset == pcoff)
            dline.HighlightData(8, new(128, 16, 16));
          else if (offset == hoverStart)
            dline.HighlightData(hoverLen, new(128, 128, 128));
          dline.AddData(infos[offset], data[offset..], out var rect);
          if (rect.Contains(mouse))
            nextHoverOff = offset;
          offset++;
        }
        ImGui.Text(dline);
      }

      if (nextHoverOff >= hoverStart && nextHoverOff < hoverStart + hoverLen && ImGui.BeginTooltip())
      {
        var addr = debugAddress + hoverStart;
        var id = Current.Symbols?.ID(addr) ?? default;

        line.Clear();
        line.Add(addr, "X6");
        if (!string.IsNullOrEmpty(id.Label))
        {
          line.Add(": ");
          line.Add(id.Label);
          if (id.Offset != 0)
          {
            line.Add('+');
            line.Add(id.Offset, "g");
          }
        }
        ImGui.Text(dline);

        var info = infos[hoverStart];
        if (info.Inst.HasValue && hoverStart + 8 <= TOTAL_VALS)
        {
          var inst = Instruction.Decode(Encoding.Decode(data[hoverStart..], DataType.U64).Unsigned);
          line.Clear();
          inst.Format(ref line, Current.Symbols);
          ImGui.Text(line.Line);
        }
        else if (info.Type.HasValue)
        {
          var type = info.Type.Value;
          var width = info.Width.Value;
          line.Clear();
          line.Add(type);
          line.Add('*');
          line.Add(width, "g");
          ImGui.Text(line.Line);

          var doff = hoverStart;

          for (var i = 0; i < width && doff + type.SizeBytes() <= TOTAL_VALS; i++, doff += type.SizeBytes())
          {
            line.Clear();
            var val = Encoding.Decode(data[doff..], type);
            line.Add(val, type);
            ImGui.Text(line.Line);
          }
        }
        else
        {
          line.Clear();
          line.Add(data[hoverStart], "X2");
          ImGui.Text(line.Line);
        }

        ImGui.EndTooltip();
      }

      hoverAddress = nextHoverOff + debugAddress;
    }

    private static void DrawControls()
    {
      if (ImGui.Button("Run##run")) Restart();
      ImGui.SameLine();
      if (ImGui.Button("Resume##resume")) Resume();
      ImGui.SameLine();
      doStep = ImGui.Button("Step##step");
      ImGui.SameLine();
      if (ImGui.Button("Stop##stop")) Stop();

      ImGui.Text(stats);

      foreach (var line in output)
        ImGui.Text(line);

      if ((output.Count > 0 || stats.Length > 0) && ImGui.Button("Clear##clear"))
      {
        stats = "";
        output.Clear();
      }
    }

    public static void LoadLibrary(string name)
    {
      var source = Library.LoadImport(name);
      var sbytes = System.Text.Encoding.ASCII.GetBytes(source.Source);
      sbytes.CopyTo(buffer);
      buffer[sbytes.Length] = 0;
    }

    private static void Step(Vehicle vehicle)
    {
      if (vehicle != Current?.Vehicle)
      {
        Current = new(vehicle, AddOutput, new(terminalLabels));
        Current.Terminal.Update();
      }

      var maxSteps = ProcSystem.STEPS_PER_FRAME;
      if (doStep)
      {
        Current.Processor.SleepTime = 0;
        maxSteps = 1;
      }

      try
      {
        Current.OnFrame(maxSteps);
      }
      catch (Exception ex)
      {
        AddOutput(ex.ToString());
      }

      var sb = new StringBuilder();
      var pc = Current.Processor.PC;
      if (Current.Symbols != null && Current.Symbols.InstToken(pc, out var itoken))
      {
        itoken = Current.Symbols.RootToken(itoken);
        var sname = Current.Symbols.SourceName(itoken);
        var loc = Current.Symbols.SourceLine(itoken, out var lnum, out var loff);
        var id = Current.Symbols.ID(pc);

        sb.Append('@').Append(pc).Append(": ");
        sb.Append(sname).Append(':').Append(lnum + 1).Append(':').Append(loff).Append(' ');
        sb.Append(id.Label).Append('+').Append(id.Offset).AppendLine();
        sb.Append(loc).AppendLine();
      }
      else
      {
        sb.Append('@').Append(pc).AppendLine();
      }
      sb.Append(Current.LastSteps).Append(" steps in ");
      sb.AppendFormat("{0:0.##}", Current.LastMs).Append("ms");
      stats = sb.ToString();

      if (doStep)
        Stop();

      doStep = false;
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

    private static void Restart()
    {
      Current.Processor.PC = 0;
      Current.Processor.SleepTime = 0;
    }

    private static void Resume()
    {
      Current.Processor.SleepTime = 0;
    }

    private static void Stop()
    {
      Current.Processor.SleepTime = ulong.MaxValue;
    }

    private static void AddOutput(string line)
    {
      output.Add(line);
      Console.WriteLine(line);
      while (output.Count > 20)
        output.RemoveAt(0);
    }
  }

  public class TerminalLabel : GaugeLabelReference
  {
    public uint4 PackedText = Terminal.CharCodes[0];
    public override uint4 PackData0() => PackedText;
  }

  public static partial class Extensions
  {
    public static bool Contains(this in floatRect r, float2 p) =>
      p.X >= r.Min.X && p.X < r.Max.X && p.Y >= r.Min.Y && p.Y < r.Max.Y;
  }
}