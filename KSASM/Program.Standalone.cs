
using System;
using Brutal.GlfwApi;
using Brutal.ImGuiApi;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Brutal.VulkanApi.Abstractions;
using Core;
using KSA;
using RenderCore;

namespace KSASM
{
  public static class StandaloneImGui
  {
    private static GlfwWindow window;
    private static Renderer renderer;
    private static RenderPassState rstate;
    private static Action OnDrawUi;

    public static void Run(Action onDrawUi)
    {
      OnDrawUi = onDrawUi;
      Init();

      while (!window.ShouldClose)
        OnFrame();
    }

    private static void Init()
    {
      Glfw.Init();

      Glfw.WindowHint(GlfwWindowHint.ClientApi, 0);
      Glfw.WindowHint(GlfwWindowHint.AutoIconify, 0);
      Glfw.WindowHint(GlfwWindowHint.FocusOnShow, 1);
      window = Glfw.CreateWindow(new()
      {
        Title = "ImGui Test",
        Size = new int2(1200, 800),
      });

      renderer = new Renderer(window, VkFormat.D32SFloat, VkPresentModeKHR.FifoKHR, VulkanHelpers.Api.VERSION_1_3);

      rstate = new RenderPassState
      {
        Pass = renderer.MainRenderPass,
        SampleCount = VkSampleCountFlags._1Bit,
        ClearValues = [
          new VkClearColorValue() { Float32 = Color.Black.AsFloat4 },
          new VkClearDepthStencilValue() { Depth = 0 },
        ]
      };

      ImGui.CreateContext();
      var io = ImGui.GetIO();
      io.ConfigDpiScaleFonts = true;
      io.ConfigDpiScaleViewports = true;

      ImGuiBackend.Initialize(window, renderer);

      // This requires the working directory to be set to the KSA install (or the cwd having a Content folder with at least one ttf)
      KSA.Program.ConsoleWindow = new(); // required so fontmanager doesn't throw
      FontManager.Initialize(renderer.Device);
    }

    private static void OnFrame()
    {
      Glfw.PollEvents();
      ImGuiBackend.NewFrame();
      ImGui.NewFrame();
      ImGuiHelper.StartFrame();

      OnDrawUi();

      ImGui.Render();
      var (result, frame) = renderer.TryAcquireNextFrame();
      if (result != FrameResult.Success)
      {
        RebuildRenderer();
        (result, frame) = renderer.TryAcquireNextFrame();
      }
      if (result != FrameResult.Success)
        throw new InvalidOperationException($"{result}");

      var (resources, commandBuffer) = frame;
      var begin = new VkRenderPassBeginInfo()
      {
        RenderPass = renderer.MainRenderPass,
        Framebuffer = resources.Framebuffer,
        RenderArea = new(renderer.Extent),
        ClearValues = rstate.ClearValues.Ptr,
        ClearValueCount = 2,
      };

      commandBuffer.Reset();
      commandBuffer.Begin(VkCommandBufferUsageFlags.OneTimeSubmitBit);
      commandBuffer.BeginRenderPass(in begin, VkSubpassContents.Inline);
      ImGuiBackend.Vulkan.RenderDrawData(commandBuffer);
      commandBuffer.EndRenderPass();
      commandBuffer.End();

      var frameResult = renderer.TrySubmitFrame();
      if (frameResult != FrameResult.Success)
        RebuildRenderer();
    }

    private static void RebuildRenderer()
    {
      renderer.Rebuild(VkPresentModeKHR.FifoKHR);
      renderer.Device.WaitIdle();
      rstate.Pass = renderer.MainRenderPass;
    }
  }
}