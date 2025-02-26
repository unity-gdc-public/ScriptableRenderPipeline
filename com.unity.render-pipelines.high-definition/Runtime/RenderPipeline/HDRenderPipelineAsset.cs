using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.HighDefinition
{
    enum ShaderVariantLogLevel
    {
        Disabled,
        OnlyHDRPShaders,
        AllShaders,
    }

    // The HDRenderPipeline assumes linear lighting. Doesn't work with gamma.
    public partial class HDRenderPipelineAsset : RenderPipelineAsset
    {

        HDRenderPipelineAsset()
        {
        }

        void Reset() => OnValidate();

        protected override UnityEngine.Rendering.RenderPipeline CreatePipeline()
        {
            // safe: When we return a null render pipline it will do nothing in the rendering
            HDRenderPipeline pipeline = null;

            // We need to do catch every errors that happend during the HDRP build, when we upgrade the
            // HDRP package, some required assets are not yet imported by the package manager when the
            // pipeline is created so in that case, we just return a null pipeline. Some error may appear
            // when we upgrade the pipeline but it's better than breaking HDRP resources an causing more
            // errors.
            try
            {
                pipeline = new HDRenderPipeline(this, HDRenderPipeline.defaultAsset);
            } catch (Exception e) {
                UnityEngine.Debug.LogError(e);
            }

            return pipeline;
        }

        protected override void OnValidate()
        {
            //Do not reconstruct the pipeline if we modify other assets.
            //OnValidate is called once at first selection of the asset.
            if (GraphicsSettings.renderPipelineAsset == this)
                base.OnValidate();
        }

        [SerializeField]
        RenderPipelineResources m_RenderPipelineResources;

        internal RenderPipelineResources renderPipelineResources
        {
            get { return m_RenderPipelineResources; }
            set { m_RenderPipelineResources = value; }
        }

        [SerializeField]
        HDRenderPipelineRayTracingResources m_RenderPipelineRayTracingResources;
        internal HDRenderPipelineRayTracingResources renderPipelineRayTracingResources
        {
            get { return m_RenderPipelineRayTracingResources; }
            set { m_RenderPipelineRayTracingResources = value; }
        }

        [SerializeField] private VolumeProfile m_DefaultVolumeProfile;

        internal VolumeProfile defaultVolumeProfile
        {
            get => m_DefaultVolumeProfile;
            set => m_DefaultVolumeProfile = value;
        }

#if UNITY_EDITOR
        HDRenderPipelineEditorResources m_RenderPipelineEditorResources;


        internal HDRenderPipelineEditorResources renderPipelineEditorResources
        {
            get
            {
                //there is no clean way to load editor resources without having it serialized
                // - impossible to load them at deserialization
                // - constructor only called at asset creation
                // - cannot rely on OnEnable
                //thus fallback with lazy init for them
                if (m_RenderPipelineEditorResources == null || m_RenderPipelineEditorResources.Equals(null))
                    m_RenderPipelineEditorResources = UnityEditor.AssetDatabase.LoadAssetAtPath<HDRenderPipelineEditorResources>(HDUtils.GetHDRenderPipelinePath() + "Editor/RenderPipelineResources/HDRenderPipelineEditorResources.asset");
                return m_RenderPipelineEditorResources;
            }
            set { m_RenderPipelineEditorResources = value; }
        }
#endif

        // To be able to turn on/off FrameSettings properties at runtime for debugging purpose without affecting the original one
        // we create a runtime copy (m_ActiveFrameSettings that is used, and any parametrization is done on serialized frameSettings)
        [SerializeField]
        FrameSettings m_RenderingPathDefaultCameraFrameSettings = FrameSettings.defaultCamera;

        [SerializeField]
        FrameSettings m_RenderingPathDefaultBakedOrCustomReflectionFrameSettings = FrameSettings.defaultCustomOrBakeReflectionProbe;

        [SerializeField]
        FrameSettings m_RenderingPathDefaultRealtimeReflectionFrameSettings = FrameSettings.defaultRealtimeReflectionProbe;

        internal ref FrameSettings GetDefaultFrameSettings(FrameSettingsRenderType type)
        {
            switch(type)
            {
                case FrameSettingsRenderType.Camera:
                    return ref m_RenderingPathDefaultCameraFrameSettings;
                case FrameSettingsRenderType.CustomOrBakedReflection:
                    return ref m_RenderingPathDefaultBakedOrCustomReflectionFrameSettings;
                case FrameSettingsRenderType.RealtimeReflection:
                    return ref m_RenderingPathDefaultRealtimeReflectionFrameSettings;
                default:
                    throw new ArgumentException("Unknown FrameSettingsRenderType");
            }
        }

        internal bool frameSettingsHistory { get; set; } = false;

        internal ReflectionSystemParameters reflectionSystemParameters
        {
            get
            {
                return new ReflectionSystemParameters
                {
                    maxPlanarReflectionProbePerCamera = currentPlatformRenderPipelineSettings.lightLoopSettings.planarReflectionProbeCacheSize,
                    maxActivePlanarReflectionProbe = 512,
                    planarReflectionProbeSize = (int)currentPlatformRenderPipelineSettings.lightLoopSettings.planarReflectionTextureSize,
                    maxActiveReflectionProbe = 512,
                    reflectionProbeSize = (int)currentPlatformRenderPipelineSettings.lightLoopSettings.reflectionCubemapSize
                };
            }
        }

        // Note: having m_RenderPipelineSettings serializable allows it to be modified in editor.
        // And having it private with a getter property force a copy.
        // As there is no setter, it thus cannot be modified by code.
        // This ensure immutability at runtime.

        // Store the various RenderPipelineSettings for each platform (for now only one)
        [SerializeField, FormerlySerializedAs("renderPipelineSettings")]
        RenderPipelineSettings m_RenderPipelineSettings = RenderPipelineSettings.@default;

        // Return the current use RenderPipelineSettings (i.e for the current platform)
        internal RenderPipelineSettings currentPlatformRenderPipelineSettings => m_RenderPipelineSettings;

        [SerializeField]
        internal bool allowShaderVariantStripping = true;
        [SerializeField]
        internal bool enableSRPBatcher = true;
        [SerializeField]
        internal ShaderVariantLogLevel shaderVariantLogLevel = ShaderVariantLogLevel.Disabled;

        [SerializeField]
        [Obsolete("Use diffusionProfileSettingsList instead")]
        internal DiffusionProfileSettings diffusionProfileSettings;

        [SerializeField]
        internal DiffusionProfileSettings[] diffusionProfileSettingsList = new DiffusionProfileSettings[0];

        // HDRP use GetRenderingLayerMaskNames to create its light linking system
        // Mean here we define our name for light linking.
        [System.NonSerialized]
        string[] m_RenderingLayerNames = null;
        string[] renderingLayerNames
        {
            get
            {
                if (m_RenderingLayerNames == null)
                {
                    m_RenderingLayerNames = new string[32];

                    // By design we can't touch this one, but we can rename it
                    m_RenderingLayerNames[0] = "Light Layer default";

                    // We only support up to 7 layer + default.
                    for (int i = 1; i < 8; ++i)
                    {
                        m_RenderingLayerNames[i] = string.Format("Light Layer {0}", i);
                    }

                    // Unused
                    for (int i = 8; i < m_RenderingLayerNames.Length; ++i)
                    {
                        m_RenderingLayerNames[i] = string.Format("Unused {0}", i);
                    }
                }

                return m_RenderingLayerNames;
            }
        }

        public override string[] renderingLayerMaskNames
        {
            get
            {
                return renderingLayerNames;
            }
        }

        public override Shader defaultShader
        {
            get
            {
                return m_RenderPipelineResources.shaders.defaultPS;
            }
        }

#if UNITY_EDITOR
        public override Material defaultMaterial
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.materials.defaultDiffuseMat;
            }
        }

        // call to GetAutodeskInteractiveShaderXXX are only from within editor
        public override Shader autodeskInteractiveShader
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.shaderGraphs.autodeskInteractive;
            }
        }

        public override Shader autodeskInteractiveTransparentShader
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.shaderGraphs.autodeskInteractiveTransparent;
            }
        }

        public override Shader autodeskInteractiveMaskedShader
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.shaderGraphs.autodeskInteractiveMasked;
            }
        }

        public override Shader terrainDetailLitShader
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.shaders.terrainDetailLitShader;
            }
        }

        public override Shader terrainDetailGrassShader
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.shaders.terrainDetailGrassShader;
            }
        }

        public override Shader terrainDetailGrassBillboardShader
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.shaders.terrainDetailGrassBillboardShader;
            }
        }

        // Note: This function is HD specific
        public Material GetDefaultDecalMaterial()
        {
            return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.materials.defaultDecalMat;
        }

        // Note: This function is HD specific
        public Material GetDefaultMirrorMaterial()
        {
            return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.materials.defaultMirrorMat;
        }

        public override Material defaultTerrainMaterial
        {
            get
            {
                return renderPipelineEditorResources == null ? null : renderPipelineEditorResources.materials.defaultTerrainMat;
            }
        }

        // Array structure that allow us to manipulate the set of defines that the HD render pipeline needs
        List<string> defineArray = new List<string>();

        bool UpdateDefineList(bool flagValue, string defineMacroValue)
        {
            bool macroExists = defineArray.Contains(defineMacroValue);
            if (flagValue)
            {
                if (!macroExists)
                {
                    defineArray.Add(defineMacroValue);
                    return true;
                }
            }
            else
            {
                if (macroExists)
                {
                    defineArray.Remove(defineMacroValue);
                    return true;
                }
            }
            return false;
        }

        // This function allows us to raise or remove some preprocessing defines based on the render pipeline settings
        public void EvaluateSettings()
        {
#if REALTIME_RAYTRACING_SUPPORT
            // Grab the current set of defines and split them
            string currentDefineList = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.BuildTargetGroup.Standalone);
            defineArray.Clear();
            defineArray.AddRange(currentDefineList.Split(';'));

            // Update all the individual defines
            bool needUpdate = false;
            needUpdate |= UpdateDefineList(currentPlatformRenderPipelineSettings.supportRayTracing, "ENABLE_RAYTRACING");

            // Only set if it changed
            if (needUpdate)
            {
                UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(UnityEditor.BuildTargetGroup.Standalone, string.Join(";", defineArray.ToArray()));
            }
#endif
        }
#endif
    }
}
