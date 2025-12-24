
using System;
using Brutal.ImGuiApi;
using Brutal.Numerics;

using IImGui = Brutal.ImGuiApi.Internal.ImGui;

namespace KSASM.UI
{
  public class AsmUi
  {
    public static ImColor8 PCHighlight => new(128, 16, 16);
    public static ImColor8 TokenHighlight => new(64, 64, 64);
    public static ImColor8 TokenHoverHilight => new(128, 128, 128);

    public static bool Enabled = true;
    public static AsmUi Instance { get; private set; }
    public static bool IsTyping => Instance?.isTyping ?? false;

    public static void OnFrame(ProcContext ctx)
    {
      if (Instance?.ps.Id != ctx.Id)
        Instance = new(ctx);

      Instance.OnFrame();
    }

    public static void OnMenu()
    {
      if (ImGui.MenuItem("KSASM", default, Enabled))
        Enabled = !Enabled;
    }

    private const ImGuiWindowFlags WINDOW_FLAGS =
      ImGuiWindowFlags.NoScrollbar |
      ImGuiWindowFlags.NoSavedSettings;

    private readonly string title;
    private readonly ImGuiID dock;
    private readonly ProcSystem ps;
    private readonly DockedWindow[] windows;

    public ImGuiID Dock => dock;
    public ProcSystem Ps => ps;

    public EditorWindow Editor { get; init; }
    public ControlsWindow Controls { get; init; }
    public MacroViewWindow MacroView { get; init; }
    public InstViewWindow InstView { get; init; }
    public StackViewWindow StackView { get; init; }
    public MemViewWindow MemView { get; init; }
    public MemWatchWindow MemWatch { get; init; }
    public DevViewWindow DevView { get; init; }

    private bool isTyping;

    private AsmUi(ProcContext ctx)
    {
      title = $"KSASM##{ctx.Id}";
      dock = ImGui.GetID($"KSASM##{ctx.Id}-dock");
      ps = new ProcSystem(ctx);

      windows = [
        Editor = new(this),
        Controls = new(this),
        MacroView = new(this),
        InstView = new(this),
        StackView = new(this),
        MemView = new(this),
        MemWatch = new(this),
        DevView = new(this),
      ];
    }

    private void OnFrame()
    {
      Step();

      // disable read debug during UI draw so it doesn't spam
      var prevDebug = MemoryAccessor.DebugRead;
      MemoryAccessor.DebugRead = false;

      DrawWindow();

      MemoryAccessor.DebugRead = prevDebug;

      isTyping = Enabled && ImGui.GetIO().WantCaptureKeyboard;
    }

    private void Step()
    {
      try
      {
        ps.OnFrame();
      }
      catch (Exception ex)
      {
        ps.Logs.Log(ex.ToString());
      }
    }

    private void DrawWindow()
    {
      ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;

      if (!Enabled)
        return;

      ImGui.SetNextWindowSizeConstraints(new(300, 300), new(1e10f, 1e10f));
      ImGui.SetNextWindowSize(new(900, 800), ImGuiCond.Appearing);
      ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new float2(0, 0));
      var hidden = !ImGui.Begin(title, ref Enabled, WINDOW_FLAGS) || ImGui.IsWindowCollapsed();
      ImGui.PopStyleVar();
      if (hidden)
      {
        ImGui.End();
        return;
      }

      if (IImGui.DockBuilderGetNode(dock).IsNull())
      {
        IImGui.DockBuilderAddNode(dock);
        IImGui.DockBuilderSplitNode(dock, ImGuiDir.Down, 0.3f, out var down, out var up);
        IImGui.DockBuilderGetNode(down).LocalFlags |= ImGuiDockNodeFlags.AutoHideTabBar;

        IImGui.DockBuilderSplitNode(up, ImGuiDir.Right, 0.57f, out var right, out var left);

        foreach (var window in windows)
        {
          IImGui.DockBuilderDockWindow(window.Title, window.Group switch
          {
            DockGroup.Editor => left,
            DockGroup.Memory => right,
            DockGroup.Logs => down,
            _ => throw new InvalidOperationException($"{window.Title} {window.Group}"),
          });
        }

        IImGui.DockBuilderFinish(dock);
      }

      ImGuiX.DockSpace(dock, windowClass: new() { ClassId = dock, DockingAllowUnclassed = false });

      ImGui.End();

      foreach (var window in windows)
      {
        window.DrawWindow();
      }

      return;
    }
  }

  public enum DockGroup { Editor, Memory, Logs, }

  public abstract class DockedWindow(string title, AsmUi parent)
  {
    protected readonly AsmUi parent = parent;
    protected readonly ProcSystem ps = parent.Ps;
    protected readonly ImGuiID dock = parent.Dock;

    private readonly string title = $"{title}##{parent.Ps.Id}";
    private readonly ImGuiWindowClass windowClass = new()
    {
      ClassId = parent.Dock,
      DockingAllowUnclassed = false,
    };

    public string Title => title;

    public void DrawWindow()
    {
      ImGuiX.SetNextWindowClass(windowClass);
      var visible = ImGui.Begin(title);

      if (IImGui.GetWindowDockNode().RootNode() != dock && !IsDragging)
      {
        ImGui.End();
        ImGui.SetNextWindowDockID(dock);
        visible = ImGui.Begin(title);
      }
      if (visible)
        Draw();
      ImGui.End();
    }

    public abstract DockGroup Group { get; }

    protected abstract void Draw();

    protected static bool IsDragging =>
      ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows) &&
      ImGui.IsMouseDragging(ImGuiMouseButton.Left) &&
      ImGui.IsItemActive();
  }
}