using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace UnityEngine.Experimental.Rendering.HighDefinition
{
    using RTHandle = RTHandleSystem.RTHandle;
    public class HDDiffuseDenoiser
    {
#if ENABLE_RAYTRACING
        ComputeShader m_SimpleDenoiserCS;
        Texture m_OwenScrambleRGBA;

        SharedRTManager m_SharedRTManager;

        RTHandle m_HistoryValidity = null;
        RTHandle m_IntermediateBuffer0 = null;
        RTHandle m_IntermediateBuffer1 = null;

        public HDDiffuseDenoiser()
        {
        }

        public void Init(RenderPipelineResources rpResources, HDRenderPipelineRayTracingResources rpRTResources, SharedRTManager sharedRTManager)
        {
            m_SimpleDenoiserCS = rpRTResources.diffuseDenoiserCS;
            m_OwenScrambleRGBA = rpResources.textures.owenScrambledRGBATex;
            m_SharedRTManager = sharedRTManager;

            m_HistoryValidity = RTHandles.Alloc(Vector2.one, TextureXR.slices, colorFormat: GraphicsFormat.R8_UInt, dimension: TextureXR.dimension, enableRandomWrite: true, useDynamicScale: true, useMipMap: false, autoGenerateMips: false, name: "HistoryValidity");
            m_IntermediateBuffer0 = RTHandles.Alloc(Vector2.one, TextureXR.slices, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureXR.dimension, enableRandomWrite: true, useDynamicScale: true, useMipMap: false, autoGenerateMips: false, name: "IntermediateBuffer0");
            m_IntermediateBuffer1 = RTHandles.Alloc(Vector2.one, TextureXR.slices, colorFormat: GraphicsFormat.R16G16B16A16_SFloat, dimension: TextureXR.dimension, enableRandomWrite: true, useDynamicScale: true, useMipMap: false, autoGenerateMips: false, name: "IntermediateBuffer1");
        }

        public void Release()
        {
            RTHandles.Release(m_IntermediateBuffer1);
            RTHandles.Release(m_IntermediateBuffer0);
            RTHandles.Release(m_HistoryValidity);
        }

        public void DenoiseBuffer(CommandBuffer cmd, HDCamera hdCamera,
            RTHandle noisySignal, RTHandle historySignal,
            RTHandle outputSignal, 
            float kernelSize,
            bool singleChannel = true, bool useNormal = false,
            bool halfResolutionFilter = false)
        {
            // Texture dimensions
            int texWidth = hdCamera.actualWidth;
            int texHeight = hdCamera.actualHeight;

            // Evaluate the dispatch parameters
            int areaTileSize = 8;
            int numTilesX = (texWidth + (areaTileSize - 1)) / areaTileSize;
            int numTilesY = (texHeight + (areaTileSize - 1)) / areaTileSize;

            int m_KernelFilter = 0;

            // Apply a vectorized temporal filtering pass and store it back in the denoisebuffer0 with the analytic value in the third channel
            var historyScale = new Vector2(hdCamera.actualWidth / (float)historySignal.rt.width, hdCamera.actualHeight / (float)historySignal.rt.height);
            cmd.SetComputeVectorParam(m_SimpleDenoiserCS, HDShaderIDs._RTHandleScaleHistory, historyScale);

            var historyDepthBuffer = hdCamera.GetCurrentFrameRT((int)HDCameraFrameHistoryType.Depth);
            var historyNormalBuffer = hdCamera.GetCurrentFrameRT((int)HDCameraFrameHistoryType.Normal);
            if (historyDepthBuffer == null || historyNormalBuffer == null)
            {
                HDUtils.BlitCameraTexture(cmd, noisySignal, historySignal);
                return;
            }

            m_KernelFilter = m_SimpleDenoiserCS.FindKernel("ValidateHistory");
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DepthTexture, m_SharedRTManager.GetDepthStencilBuffer());
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._NormalBufferTexture, m_SharedRTManager.GetNormalBuffer());
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._HistoryDepthTexture, historyDepthBuffer);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._HistoryNormalBufferTexture, historyNormalBuffer);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._HistoryValidityBuffer, m_HistoryValidity);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._NormalHistoryCriterion, useNormal ? 1 : 0);
            cmd.DispatchCompute(m_SimpleDenoiserCS, m_KernelFilter, numTilesX, numTilesY, 1);

            m_KernelFilter = m_SimpleDenoiserCS.FindKernel(singleChannel ?  "TemporalAccumulationSingle": "TemporalAccumulationColor");
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseInputTexture, noisySignal);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._HistoryBuffer, historySignal);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DepthTexture, m_SharedRTManager.GetDepthStencilBuffer());
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseOutputTextureRW, m_IntermediateBuffer1);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._HistoryValidityBuffer, m_HistoryValidity);
            cmd.DispatchCompute(m_SimpleDenoiserCS, m_KernelFilter, numTilesX, numTilesY, 1);

            m_KernelFilter = m_SimpleDenoiserCS.FindKernel(singleChannel ? "CopyHistorySingle" : "CopyHistoryColor");
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseInputTexture, m_IntermediateBuffer1);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseOutputTextureRW, historySignal);
            cmd.DispatchCompute(m_SimpleDenoiserCS, m_KernelFilter, numTilesX, numTilesY, 1);

            m_KernelFilter = m_SimpleDenoiserCS.FindKernel(singleChannel ? "BilateralFilterSingle" : "BilateralFilterColor");
            cmd.SetGlobalTexture(HDShaderIDs._OwenScrambledRGTexture, m_OwenScrambleRGBA);
            cmd.SetComputeFloatParam(m_SimpleDenoiserCS, HDShaderIDs._DenoiserFilterRadius, kernelSize);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseInputTexture, m_IntermediateBuffer1);
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DepthTexture, m_SharedRTManager.GetDepthStencilBuffer());
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._NormalBufferTexture, m_SharedRTManager.GetNormalBuffer());
            cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseOutputTextureRW, halfResolutionFilter ? m_IntermediateBuffer0 : outputSignal);
            cmd.SetComputeIntParam(m_SimpleDenoiserCS, HDShaderIDs._HalfResolutionFilter, halfResolutionFilter ? 1 : 0);
            cmd.DispatchCompute(m_SimpleDenoiserCS, m_KernelFilter, numTilesX, numTilesY, 1);

            if (halfResolutionFilter)
            {
                m_KernelFilter = m_SimpleDenoiserCS.FindKernel(singleChannel ? "GatherSingle" : "GatherColor");
                cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseInputTexture, m_IntermediateBuffer0);
                cmd.SetComputeTextureParam(m_SimpleDenoiserCS, m_KernelFilter, HDShaderIDs._DenoiseOutputTextureRW, outputSignal);
                cmd.DispatchCompute(m_SimpleDenoiserCS, m_KernelFilter, numTilesX, numTilesY, 1);
            }
        }
#endif
    }
}
