using UnityEngine;
using UnityEngine.Rendering;

// Include material common properties names
using static UnityEngine.Rendering.HighDefinition.HDMaterialProperties;

namespace UnityEditor.Rendering.HighDefinition
{
    /// <summary>
    /// GUI for HDRP Fabric shader graphs
    /// </summary>
    class FabricGUI : HDShaderGUI
    {
        // For surface option shader graph we only want all unlit features but alpha clip, back then front rendering and SSR
        const SurfaceOptionUIBlock.Features   surfaceOptionFeatures = SurfaceOptionUIBlock.Features.Unlit
            ^ SurfaceOptionUIBlock.Features.AlphaCutoff
            ^ SurfaceOptionUIBlock.Features.BackThenFrontRendering
            ^ SurfaceOptionUIBlock.Features.ShowAfterPostProcessPass;

        MaterialUIBlockList uiBlocks = new MaterialUIBlockList
        {
            new SurfaceOptionUIBlock(MaterialUIBlock.Expandable.Base, features: surfaceOptionFeatures),
            new ShaderGraphUIBlock(MaterialUIBlock.Expandable.ShaderGraph),
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                uiBlocks.OnGUI(materialEditor, props);

                // Apply material keywords and pass:
                if (changed.changed)
                {
                    foreach (var material in uiBlocks.materials)
                        SetupMaterialKeywordsAndPassInternal(material);
                }
            }
        }

        public static void SetupMaterialKeywordsAndPass(Material material)
        {
            SynchronizeShaderGraphProperties(material);

            BaseLitGUI.SetupBaseLitKeywords(material);
            bool receiveSSR = material.HasProperty(kReceivesSSR) ? material.GetInt(kReceivesSSR) != 0 : false;
            bool useSplitLighting = material.HasProperty(kUseSplitLighting) ? material.GetInt(kUseSplitLighting) != 0: false;
            BaseLitGUI.SetupStencil(material, receiveSSR, useSplitLighting);
            if (material.HasProperty(kAdditionalVelocityChange))
            {
                CoreUtils.SetKeyword(material, "_ADDITIONAL_VELOCITY_CHANGE", material.GetInt(kAdditionalVelocityChange) != 0);
            }

        }

        protected override void SetupMaterialKeywordsAndPassInternal(Material material) => SetupMaterialKeywordsAndPass(material);
    }
}
