using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.HighDefinition
{
    public partial class HDRenderPipeline
    {
        static void DrawOpaqueRendererList(in RenderGraphContext context, in FrameSettings frameSettings, in RendererList rendererList)
        {
            DrawOpaqueRendererList(context.renderContext, context.cmd, frameSettings, rendererList);
        }

        static void DrawTransparentRendererList(in RenderGraphContext context, in FrameSettings frameSettings, RendererList rendererList)
        {
            DrawTransparentRendererList(context.renderContext, context.cmd, frameSettings, rendererList);
        }

        static int SampleCountToPassIndex(MSAASamples samples)
        {
            switch (samples)
            {
                case MSAASamples.None:
                    return 0;
                case MSAASamples.MSAA2x:
                    return 1;
                case MSAASamples.MSAA4x:
                    return 2;
                case MSAASamples.MSAA8x:
                    return 3;
            };
            return 0;
        }

        bool NeedClearColorBuffer(HDCamera hdCamera)
        {
            if (hdCamera.clearColorMode == HDAdditionalCameraData.ClearColorMode.Color ||
                // If the luxmeter is enabled, the sky isn't rendered so we clear the background color
                m_CurrentDebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter ||
                // If the matcap view is enabled, the sky isn't updated so we clear the background color
                m_CurrentDebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.MatcapView ||
                // If we want the sky but the sky don't exist, still clear with background color
                (hdCamera.clearColorMode == HDAdditionalCameraData.ClearColorMode.Sky && !m_SkyManager.IsVisualSkyValid()) ||
                // Special handling for Preview we force to clear with background color (i.e black)
                // Note that the sky use in this case is the last one setup. If there is no scene or game, there is no sky use as reflection in the preview
                HDUtils.IsRegularPreviewCamera(hdCamera.camera))
            {
                return true;
            }

            return false;
        }

        Color GetColorBufferClearColor(HDCamera hdCamera)
        {
            Color clearColor = hdCamera.backgroundColorHDR;

            // We set the background color to black when the luxmeter is enabled to avoid picking the sky color
            if (m_CurrentDebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.LuxMeter ||
                m_CurrentDebugDisplaySettings.data.lightingDebugSettings.debugLightingMode == DebugLightingMode.MatcapView)
                clearColor = Color.black;

            return clearColor;
        }

        // XR Specific
        class StereoRenderingPassData
        {
            public Camera camera;
            public XRPass xr;
        }

        void StartLegacyStereo(RenderGraph renderGraph, HDCamera hdCamera)
        {
            if (hdCamera.xr.enabled)
            {
                using (var builder = renderGraph.AddRenderPass<StereoRenderingPassData>("Start Stereo Rendering", out var passData))
                {
                    passData.camera = hdCamera.camera;
                    passData.xr = hdCamera.xr;

                    builder.SetRenderFunc(
                    (StereoRenderingPassData data, RenderGraphContext context) =>
                    {
                        data.xr.StartSinglePass(context.cmd, data.camera, context.renderContext);
                    });
                }
            }
        }

        void StopLegacyStereo(RenderGraph renderGraph, HDCamera hdCamera)
        {
            if (hdCamera.xr.enabled && hdCamera.camera.stereoEnabled)
            {
                using (var builder = renderGraph.AddRenderPass<StereoRenderingPassData>("Stop Stereo Rendering", out var passData))
                {
                    passData.camera = hdCamera.camera;
                    passData.xr = hdCamera.xr;

                    builder.SetRenderFunc(
                    (StereoRenderingPassData data, RenderGraphContext context) =>
                    {
                        data.xr.StopSinglePass(context.cmd, data.camera, context.renderContext);
                    });
                }
            }
        }

        class EndCameraXRPassData
        {
            public HDCamera hdCamera;
        }

        void EndCameraXR(RenderGraph renderGraph, HDCamera hdCamera)
        {
            if (hdCamera.xr.enabled && hdCamera.camera.stereoEnabled)
            {
                using (var builder = renderGraph.AddRenderPass<EndCameraXRPassData>("End Camera", out var passData))
                {
                    passData.hdCamera = hdCamera;

                    builder.SetRenderFunc(
                    (EndCameraXRPassData data, RenderGraphContext ctx) =>
                    {
                        data.hdCamera.xr.EndCamera(ctx.cmd, data.hdCamera, ctx.renderContext);
                    });
                }
            }
        }
    }
}
