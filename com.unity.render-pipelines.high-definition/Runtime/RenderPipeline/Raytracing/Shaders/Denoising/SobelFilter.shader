Shader "Hidden/HDRP/SobelFilter"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/NormalBuffer.hlsl"

            TEXTURE2D_X(_DepthTexture);
            TEXTURE2D_X(_NormalTexture);
            SAMPLER(sampler_LinearClamp);

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.texcoord = GetNormalizedFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float3 FetchLinearDepth(float2 uvPos)
            {
                return LinearEyeDepth(SAMPLE_TEXTURE2D_X(_DepthTexture, sampler_LinearClamp, uvPos).x, _ZBufferParams);
            }

            float3 FetchNormal(float2 uvPos)
            {
                NormalData normalData;
                DecodeFromNormalBuffer(SAMPLE_TEXTURE2D_X(_NormalTexture, sampler_LinearClamp, uvPos), normalData);
                return normalData.normalWS;
            }
            float Frag2(Varyings input) : SV_Target
            {
                float hr = 0.0;
                float vt = 0.0;
                
                hr += FetchLinearDepth(input.texcoord + float2(-1.0, -1.0) * _ScreenSize.zw) *  1.0;
                hr += FetchLinearDepth(input.texcoord + float2( 0.0, -1.0) * _ScreenSize.zw) *  0.0;
                hr += FetchLinearDepth(input.texcoord + float2( 1.0, -1.0) * _ScreenSize.zw) * -1.0;
                hr += FetchLinearDepth(input.texcoord + float2(-1.0,  0.0) * _ScreenSize.zw) *  2.0;
                hr += FetchLinearDepth(input.texcoord + float2( 0.0,  0.0) * _ScreenSize.zw) *  0.0;
                hr += FetchLinearDepth(input.texcoord + float2( 1.0,  0.0) * _ScreenSize.zw) * -2.0;
                hr += FetchLinearDepth(input.texcoord + float2(-1.0,  1.0) * _ScreenSize.zw) *  1.0;
                hr += FetchLinearDepth(input.texcoord + float2( 0.0,  1.0) * _ScreenSize.zw) *  0.0;
                hr += FetchLinearDepth(input.texcoord + float2( 1.0,  1.0) * _ScreenSize.zw) * -1.0;
                
                vt += FetchLinearDepth(input.texcoord + float2(-1.0, -1.0) * _ScreenSize.zw) *  1.0;
                vt += FetchLinearDepth(input.texcoord + float2( 0.0, -1.0) * _ScreenSize.zw) *  2.0;
                vt += FetchLinearDepth(input.texcoord + float2( 1.0, -1.0) * _ScreenSize.zw) *  1.0;
                vt += FetchLinearDepth(input.texcoord + float2(-1.0,  0.0) * _ScreenSize.zw) *  0.0;
                vt += FetchLinearDepth(input.texcoord + float2( 0.0,  0.0) * _ScreenSize.zw) *  0.0;
                vt += FetchLinearDepth(input.texcoord + float2( 1.0,  0.0) * _ScreenSize.zw) *  0.0;
                vt += FetchLinearDepth(input.texcoord + float2(-1.0,  1.0) * _ScreenSize.zw) * -1.0;
                vt += FetchLinearDepth(input.texcoord + float2( 0.0,  1.0) * _ScreenSize.zw) * -2.0;
                vt += FetchLinearDepth(input.texcoord + float2( 1.0,  1.0) * _ScreenSize.zw) * -1.0;

                return sqrt(hr * hr + vt * vt) > 0.8;
            }

            float Frag(Varyings input) : SV_Target
            {
                float3 hr = 0.0;
                float3 vt = 0.0;
                
                hr += FetchNormal(input.texcoord + float2(-1.0, -1.0) * _ScreenSize.zw) *  1.0;
                hr += FetchNormal(input.texcoord + float2( 0.0, -1.0) * _ScreenSize.zw) *  0.0;
                hr += FetchNormal(input.texcoord + float2( 1.0, -1.0) * _ScreenSize.zw) * -1.0;
                hr += FetchNormal(input.texcoord + float2(-1.0,  0.0) * _ScreenSize.zw) *  2.0;
                hr += FetchNormal(input.texcoord + float2( 0.0,  0.0) * _ScreenSize.zw) *  0.0;
                hr += FetchNormal(input.texcoord + float2( 1.0,  0.0) * _ScreenSize.zw) * -2.0;
                hr += FetchNormal(input.texcoord + float2(-1.0,  1.0) * _ScreenSize.zw) *  1.0;
                hr += FetchNormal(input.texcoord + float2( 0.0,  1.0) * _ScreenSize.zw) *  0.0;
                hr += FetchNormal(input.texcoord + float2( 1.0,  1.0) * _ScreenSize.zw) * -1.0;
                
                vt += FetchNormal(input.texcoord + float2(-1.0, -1.0) * _ScreenSize.zw) *  1.0;
                vt += FetchNormal(input.texcoord + float2( 0.0, -1.0) * _ScreenSize.zw) *  2.0;
                vt += FetchNormal(input.texcoord + float2( 1.0, -1.0) * _ScreenSize.zw) *  1.0;
                vt += FetchNormal(input.texcoord + float2(-1.0,  0.0) * _ScreenSize.zw) *  0.0;
                vt += FetchNormal(input.texcoord + float2( 0.0,  0.0) * _ScreenSize.zw) *  0.0;
                vt += FetchNormal(input.texcoord + float2( 1.0,  0.0) * _ScreenSize.zw) *  0.0;
                vt += FetchNormal(input.texcoord + float2(-1.0,  1.0) * _ScreenSize.zw) * -1.0;
                vt += FetchNormal(input.texcoord + float2( 0.0,  1.0) * _ScreenSize.zw) * -2.0;
                vt += FetchNormal(input.texcoord + float2( 1.0,  1.0) * _ScreenSize.zw) * -1.0;

                return length(sqrt(hr * hr + vt * vt));
            }

            ENDHLSL
        }

    }
    Fallback Off
}
