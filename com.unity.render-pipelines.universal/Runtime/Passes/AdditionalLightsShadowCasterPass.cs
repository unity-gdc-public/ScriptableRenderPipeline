using System;
using System.Collections.Generic;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
    internal class AdditionalLightsShadowCasterPass : ScriptableRenderPass
    {
        private static class AdditionalShadowsConstantBuffer
        {
            public static int _AdditionalLightsWorldToShadow;
            public static int _AdditionalShadowStrength;
            public static int _AdditionalShadowOffset0;
            public static int _AdditionalShadowOffset1;
            public static int _AdditionalShadowOffset2;
            public static int _AdditionalShadowOffset3;
            public static int _AdditionalShadowmapSize;
        }

        public static int m_AdditionalShadowsBufferId;
        public static int m_AdditionalShadowsIndicesId;
        bool m_UseStructuredBuffer;

        const int k_ShadowmapBufferBits = 16;
        private RenderTargetHandle m_AdditionalLightsShadowmap;
        RenderTexture m_AdditionalLightsShadowmapTexture;

        int m_ShadowmapWidth;
        int m_ShadowmapHeight;

        ShadowSliceData[] m_AdditionalLightSlices = null;
        List<float> m_AdditionalLightsShadowStrength = new List<float>();
        List<int> m_AdditionalShadowCastingLightIndices = new List<int>();
        List<int> m_AdditionalShadowCastingLightIndicesMap = new List<int>();
        const string m_ProfilerTag = "Render Additional Shadows";

        public AdditionalLightsShadowCasterPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;

            AdditionalShadowsConstantBuffer._AdditionalLightsWorldToShadow = Shader.PropertyToID("_AdditionalLightsWorldToShadow");
            AdditionalShadowsConstantBuffer._AdditionalShadowStrength = Shader.PropertyToID("_AdditionalShadowStrength");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset0 = Shader.PropertyToID("_AdditionalShadowOffset0");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset1 = Shader.PropertyToID("_AdditionalShadowOffset1");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset2 = Shader.PropertyToID("_AdditionalShadowOffset2");
            AdditionalShadowsConstantBuffer._AdditionalShadowOffset3 = Shader.PropertyToID("_AdditionalShadowOffset3");
            AdditionalShadowsConstantBuffer._AdditionalShadowmapSize = Shader.PropertyToID("_AdditionalShadowmapSize");
            m_AdditionalLightsShadowmap.Init("_AdditionalLightsShadowmapTexture");

            m_AdditionalShadowsBufferId = Shader.PropertyToID("_AdditionalShadowsBuffer");
            m_AdditionalShadowsIndicesId = Shader.PropertyToID("_AdditionalShadowsIndices");
            m_UseStructuredBuffer = RenderingUtils.useStructuredBuffer;
        }

        public bool Setup(ref RenderingData renderingData)
        {
            if (!renderingData.shadowData.supportsAdditionalLightShadows)
                return false;

            Clear();

            m_ShadowmapWidth = renderingData.shadowData.additionalLightsShadowmapWidth;
            m_ShadowmapHeight = renderingData.shadowData.additionalLightsShadowmapHeight;

            var visibleLights = renderingData.lightData.visibleLights;
            int additionalLightsCount = renderingData.lightData.additionalLightsCount;

            if (m_AdditionalLightSlices == null || m_AdditionalLightSlices.Length < additionalLightsCount)
                m_AdditionalLightSlices = new ShadowSliceData[additionalLightsCount];

            int shadowCastingLightsCount = 0;

            // Loops through all visible lights and cache the valid shadow caster ones. 
            for (int i = 0; i < visibleLights.Length && shadowCastingLightsCount < additionalLightsCount; ++i)
            {
                if (IsValidShadowCastingLight(ref renderingData.lightData, i))
                {
                    VisibleLight shadowLight = visibleLights[i];

                    if (renderingData.cullResults.GetShadowCasterBounds(i, out var bounds))
                    {
                        bool success = ShadowUtils.ExtractSpotLightMatrix(ref renderingData.cullResults,
                            ref renderingData.shadowData,
                            i,
                            out m_AdditionalLightSlices[shadowCastingLightsCount].shadowTransform,
                            out m_AdditionalLightSlices[shadowCastingLightsCount].viewMatrix,
                            out m_AdditionalLightSlices[shadowCastingLightsCount].projectionMatrix);

                        if (success)
                        {
                            m_AdditionalLightsShadowStrength.Add(shadowLight.light.shadowStrength);
                            m_AdditionalShadowCastingLightIndicesMap.Add(shadowCastingLightsCount);
                            m_AdditionalShadowCastingLightIndices.Add(i);
                            shadowCastingLightsCount++;
                            continue;
                        }
                    }
                }

                // Skip main light index
                if (i != renderingData.lightData.mainLightIndex)
                {
                    // -1 means this light doesn't cast shadows so there's no shadow casted slot in the shadowmap atlas
                    m_AdditionalShadowCastingLightIndicesMap.Add(-1);
                }
            }

            if (shadowCastingLightsCount == 0)
                return false;

            // TODO: Add support to point light shadows. We make a simplification here that only works
            // for spot lights and with max spot shadows per pass.
            int atlasWidth = renderingData.shadowData.additionalLightsShadowmapWidth;
            int atlasHeight = renderingData.shadowData.additionalLightsShadowmapHeight;
            int sliceResolution = ShadowUtils.GetMaxTileResolutionInAtlas(atlasWidth, atlasHeight, shadowCastingLightsCount);

            int shadowSlicesPerRow = (atlasWidth / sliceResolution);
            float oneOverAtlasWidth = 1.0f / m_ShadowmapWidth;
            float oneOverAtlasHeight = 1.0f / m_ShadowmapHeight;

            // We bake scale and bias to each shadowmap in the atlas in the matrix.
            // saves some instructions in shader.
            for (int i = 0; i < shadowCastingLightsCount; ++i)
            {
                m_AdditionalLightSlices[i].offsetX = (i % shadowSlicesPerRow) * sliceResolution;
                m_AdditionalLightSlices[i].offsetY = (i / shadowSlicesPerRow) * sliceResolution;
                m_AdditionalLightSlices[i].resolution = sliceResolution;

                Matrix4x4 sliceTransform = Matrix4x4.identity;
                sliceTransform.m00 = sliceResolution * oneOverAtlasWidth;
                sliceTransform.m11 = sliceResolution * oneOverAtlasHeight;
                sliceTransform.m03 = m_AdditionalLightSlices[i].offsetX * oneOverAtlasWidth;
                sliceTransform.m13 = m_AdditionalLightSlices[i].offsetY * oneOverAtlasHeight;

                // Apply shadow slice scale and offset
                m_AdditionalLightSlices[i].shadowTransform = sliceTransform * m_AdditionalLightSlices[i].shadowTransform;
            }

            return true;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            m_AdditionalLightsShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(m_ShadowmapWidth, m_ShadowmapHeight, k_ShadowmapBufferBits);
            ConfigureTarget(new RenderTargetIdentifier(m_AdditionalLightsShadowmapTexture));
            ConfigureClear(ClearFlag.All, Color.black);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.shadowData.supportsAdditionalLightShadows)
                RenderAdditionalShadowmapAtlas(ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_AdditionalLightsShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(m_AdditionalLightsShadowmapTexture);
                m_AdditionalLightsShadowmapTexture = null;
            }
        }

        void Clear()
        {
            m_AdditionalLightsShadowStrength.Clear();
            m_AdditionalShadowCastingLightIndices.Clear();
            m_AdditionalShadowCastingLightIndicesMap.Clear();
            m_AdditionalLightsShadowmapTexture = null;
        }

        void RenderAdditionalShadowmapAtlas(ref ScriptableRenderContext context, ref CullingResults cullResults, ref LightData lightData, ref ShadowData shadowData)
        {
            NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

            bool additionalLightHasSoftShadows = false;
            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingSample(cmd, m_ProfilerTag))
            {
                int shadowSlicesCount = m_AdditionalShadowCastingLightIndices.Count;
                for (int i = 0; i < shadowSlicesCount; ++i)
                {
                    // Index of the VisibleLight
                    int shadowLightIndex = m_AdditionalShadowCastingLightIndices[i];
                    VisibleLight shadowLight = visibleLights[shadowLightIndex];

                    ShadowSliceData shadowSliceData = m_AdditionalLightSlices[i];

                    var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex);
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex,
                        ref shadowData, shadowSliceData.projectionMatrix, shadowSliceData.resolution);
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    ShadowUtils.RenderShadowSlice(cmd, ref context, ref shadowSliceData, ref settings);
                    additionalLightHasSoftShadows |= shadowLight.light.shadows == LightShadows.Soft;
                }

                // We share soft shadow settings for main light and additional lights to save keywords.
                // So we check here if pipeline supports soft shadows and either main light or any additional light has soft shadows
                // to enable the keyword.
                // TODO: In PC and Consoles we can upload shadow data per light and branch on shader. That will be more likely way faster.
                bool mainLightHasSoftShadows = shadowData.supportsMainLightShadows &&
                                               lightData.mainLightIndex != -1 &&
                                               visibleLights[lightData.mainLightIndex].light.shadows == LightShadows.Soft;

                bool softShadows = shadowData.supportsSoftShadows && (mainLightHasSoftShadows || additionalLightHasSoftShadows);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightShadows, true);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, softShadows);

                SetupAdditionalLightsShadowReceiverConstants(cmd, ref shadowData, softShadows);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        void SetupAdditionalLightsShadowReceiverConstants(CommandBuffer cmd, ref ShadowData shadowData, bool softShadows)
        {
            int shadowLightsCount = m_AdditionalShadowCastingLightIndices.Count;
            
            float invShadowAtlasWidth = 1.0f / shadowData.additionalLightsShadowmapWidth;
            float invShadowAtlasHeight = 1.0f / shadowData.additionalLightsShadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;

            cmd.SetGlobalTexture(m_AdditionalLightsShadowmap.id, m_AdditionalLightsShadowmapTexture);

            if (m_UseStructuredBuffer)
            {
                NativeArray<ShaderData.ShadowData> shadowBufferData = new NativeArray<ShaderData.ShadowData>(shadowLightsCount, Allocator.Temp);
                for (int i = 0; i < shadowLightsCount; ++i)
                {
                    ShaderData.ShadowData data;
                    data.worldToShadowMatrix = m_AdditionalLightSlices[i].shadowTransform;
                    data.shadowStrength = m_AdditionalLightsShadowStrength[i];
                    shadowBufferData[i] = data;
                }

                var shadowBuffer = ShaderData.instance.GetShadowDataBuffer(shadowLightsCount);
                shadowBuffer.SetData(shadowBufferData);

                var shadowIndicesMapBuffer = ShaderData.instance.GetShadowIndicesBuffer(m_AdditionalShadowCastingLightIndicesMap.Count);
                shadowIndicesMapBuffer.SetData(m_AdditionalShadowCastingLightIndicesMap, 0, 0,
                    m_AdditionalShadowCastingLightIndicesMap.Count);

                cmd.SetGlobalBuffer(m_AdditionalShadowsBufferId, shadowBuffer);
                cmd.SetGlobalBuffer(m_AdditionalShadowsIndicesId, shadowIndicesMapBuffer);
                shadowBufferData.Dispose();
            }
            else
            {
                NativeArray<Matrix4x4> additionalLightShadowMatrices = new NativeArray<Matrix4x4>(m_AdditionalLightSlices.Length, Allocator.Temp);
                for (int i = 0; i < shadowLightsCount; ++i)
                    additionalLightShadowMatrices[i] = m_AdditionalLightSlices[i].shadowTransform;

                cmd.SetGlobalMatrixArray(AdditionalShadowsConstantBuffer._AdditionalLightsWorldToShadow, additionalLightShadowMatrices.ToArray());
                cmd.SetGlobalFloatArray(AdditionalShadowsConstantBuffer._AdditionalShadowStrength, m_AdditionalLightsShadowStrength);

                additionalLightShadowMatrices.Dispose();
            }

            if (softShadows)
            {
                // Currently only used when SHADER_API_MOBILE
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset0,
                    new Vector4(-invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset1,
                    new Vector4(invHalfShadowAtlasWidth, -invHalfShadowAtlasHeight, 0.0f, 0.0f));
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset2,
                    new Vector4(-invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowOffset3,
                    new Vector4(invHalfShadowAtlasWidth, invHalfShadowAtlasHeight, 0.0f, 0.0f));

                // Currently only used when !SHADER_API_MOBILE
                cmd.SetGlobalVector(AdditionalShadowsConstantBuffer._AdditionalShadowmapSize, new Vector4(invShadowAtlasWidth, invShadowAtlasHeight,
                    shadowData.additionalLightsShadowmapWidth, shadowData.additionalLightsShadowmapHeight));
            }
        }

        bool IsValidShadowCastingLight(ref LightData lightData, int i)
        {
            if (i == lightData.mainLightIndex)
                return false;

            VisibleLight shadowLight = lightData.visibleLights[i];
            if (shadowLight.lightType == LightType.Directional)
                return false;

            Light light = shadowLight.light;

            return (shadowLight.lightType == LightType.Spot) && light != null && light.shadows != LightShadows.None;
        }
    }
}
