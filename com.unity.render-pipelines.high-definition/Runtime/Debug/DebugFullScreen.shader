Shader "Hidden/HDRP/DebugFullScreen"
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
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.cs.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Builtin/BuiltinData.hlsl"

            CBUFFER_START (UnityDebug)
            float _FullScreenDebugMode;
            CBUFFER_END

            TEXTURE2D_X(_DebugFullScreenTexture);

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

            // Motion vector debug utilities
            float DistanceToLine(float2 p, float2 p1, float2 p2)
            {
                float2 center = (p1 + p2) * 0.5;
                float len = length(p2 - p1);
                float2 dir = (p2 - p1) / len;
                float2 rel_p = p - center;
                return dot(rel_p, float2(dir.y, -dir.x));
            }

            float DistanceToSegment(float2 p, float2 p1, float2 p2)
            {
                float2 center = (p1 + p2) * 0.5;
                float len = length(p2 - p1);
                float2 dir = (p2 - p1) / len;
                float2 rel_p = p - center;
                float dist1 = abs(dot(rel_p, float2(dir.y, -dir.x)));
                float dist2 = abs(dot(rel_p, dir)) - 0.5 * len;
                return max(dist1, dist2);
            }

            void ColorWidget(
                int2 positionSS,
                float4 rect,
                float3 borderColor,
                float3 innerColor,
                inout float4 debugColor,
                inout float4 backgroundColor
            )
            {
                const float4 distToRects = float4(rect.zw - positionSS,  positionSS - rect.xy);
                if (all(distToRects > 0))
                {
                    const float distToRect = min(min(distToRects.x, distToRects.y), min(distToRects.z, distToRects.w));
                    const float sdf = clamp(distToRect * 0.5, 0, 1);
                    debugColor = float4(
                        lerp(borderColor, innerColor, sdf),
                        1.0
                    );
                    backgroundColor.a = 0;
                }
            }

            float DrawArrow(float2 texcoord, float body, float head, float height, float linewidth, float antialias)
            {
                float w = linewidth / 2.0 + antialias;
                float2 start = -float2(body / 2.0, 0.0);
                float2 end = float2(body / 2.0, 0.0);

                // Head: 3 lines
                float d1 = DistanceToLine(texcoord, end, end - head * float2(1.0, -height));
                float d2 = DistanceToLine(texcoord, end - head * float2(1.0, height), end);
                float d3 = texcoord.x - end.x + head;

                // Body: 1 segment
                float d4 = DistanceToSegment(texcoord, start, end - float2(linewidth, 0.0));

                float d = min(max(max(d1, d2), -d3), d4);
                return d;
            }

            // return motion vector in NDC space [0..1]
            float2 SampleMotionVectors(float2 coords)
            {
                float2 motionVectorNDC;
                DecodeMotionVector(SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, coords), motionVectorNDC);

                return motionVectorNDC;
            }
            // end motion vector utilties

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Note: If the single shadow debug mode is enabled, we don't render other full screen debug modes
                // and the value of _FullScreenDebugMode is forced to 0
                if (_DebugShadowMapMode == SHADOWMAPDEBUGMODE_SINGLE_SHADOW)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);
                    return color;
                }
                // SSAO
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_SSAO)
                {
                    return 1.0f - SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord).xxxx;
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_NAN_TRACKER)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);

                    if (AnyIsNaN(color) || AnyIsInf(color))
                    {
                        color = float4(1.0, 0.0, 0.0, 1.0);
                    }
                    else
                    {
                        color.rgb = Luminance(color.rgb).xxx;
                    }

                    return color;
                }
                if( _FullScreenDebugMode == FULLSCREENDEBUGMODE_LIGHT_CLUSTER)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);
                    return color;
                }
                if( _FullScreenDebugMode == FULLSCREENDEBUGMODE_INDIRECT_DIFFUSE)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);
                    return color;
                }
                if( _FullScreenDebugMode == FULLSCREENDEBUGMODE_PRIMARY_VISIBILITY)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);
                    return color;
                }
                if ( _FullScreenDebugMode == FULLSCREENDEBUGMODE_SCREEN_SPACE_SHADOWS)
                {
                    float4 color = LOAD_TEXTURE2D_X(_DebugFullScreenTexture, (uint2)input.positionCS.xy);
                    return color;
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_MOTION_VECTORS)
                {
                    float2 mv = SampleMotionVectors(input.texcoord);

                    // Background color intensity - keep this low unless you want to make your eyes bleed
                    const float kIntensity = 0.15;

                    // Map motion vector direction to color wheel (hue between 0 and 360deg)
                    float phi = atan2(mv.x, mv.y);
                    float hue = (phi / PI + 1.0) * 0.5;
                    float r = abs(hue * 6.0 - 3.0) - 1.0;
                    float g = 2.0 - abs(hue * 6.0 - 2.0);
                    float b = 2.0 - abs(hue * 6.0 - 4.0);

                    float3 color = saturate(float3(r, g, b) * kIntensity);

                    // Grid subdivisions - should be dynamic
                    const float kGrid = 64.0;

                    // Arrow grid (aspect ratio is kept)
                    float aspect = _ScreenSize.y * _ScreenSize.z;
                    float rows = floor(kGrid * aspect);
                    float cols = kGrid;
                    float2 size = _ScreenSize.xy / float2(cols, rows);
                    float body = min(size.x, size.y) / sqrt(2.0);
                    float2 positionSS = input.texcoord.xy / _RTHandleScale.xy;
                    positionSS *= _ScreenSize.xy;
                    float2 center = (floor(positionSS / size) + 0.5) * size;
                    positionSS -= center;

                    // Sample the center of the cell to get the current arrow vector
                    float2 arrow_coord = center * _ScreenSize.zw;

                    arrow_coord *= _RTHandleScale.xy;

                    float2 mv_arrow = SampleMotionVectors(arrow_coord);
                    mv_arrow.y *= -1;

                    // Skip empty motion
                    float d = 0.0;
                    if (any(mv_arrow))
                    {
                        // Rotate the arrow according to the direction
                        mv_arrow = normalize(mv_arrow);
                        float2x2 rot = float2x2(mv_arrow.x, -mv_arrow.y, mv_arrow.y, mv_arrow.x);
                        positionSS = mul(rot, positionSS);

                        d = DrawArrow(positionSS, body, 0.25 * body, 0.5, 2.0, 1.0);
                        d = 1.0 - saturate(d);
                    }

                    return float4(color + d.xxx, 1.0);
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_COLOR_LOG)
                {
                    float4 color = LOAD_TEXTURE2D_X(_DebugFullScreenTexture, (uint2)input.positionCS.xy);
                    return color;
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_CONTACT_SHADOWS)
                {
                    uint contactShadowData = LOAD_TEXTURE2D_X(_ContactShadowTexture, input.texcoord * _ScreenSize.xy).r;

                    // when the index is -1 we display all contact shadows
                    uint mask = (_DebugContactShadowLightIndex == -1) ? -1 : 1 << _DebugContactShadowLightIndex;
                    float lightContactShadow = (contactShadowData & mask) != 0;

                    return float4(1.0 - lightContactShadow.xxx, 0.0);
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_CONTACT_SHADOWS_FADE)
                {
                    uint contactShadowData = LOAD_TEXTURE2D_X(_ContactShadowTexture, input.texcoord * _ScreenSize.xy).r;
                    float fade = float((contactShadowData >> 24)) / 255.0;

                    return float4(fade.xxx, 0.0);
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_SCREEN_SPACE_REFLECTIONS)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);
                    return float4(color.rgb, 1.0f);
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_PRE_REFRACTION_COLOR_PYRAMID
                    || _FullScreenDebugMode == FULLSCREENDEBUGMODE_FINAL_COLOR_PYRAMID)
                {
                    float4 color = SAMPLE_TEXTURE2D_X(_DebugFullScreenTexture, s_point_clamp_sampler, input.texcoord);
                    return float4(color.rgb, 1.0);
                }
                if (_FullScreenDebugMode == FULLSCREENDEBUGMODE_DEPTH_PYRAMID)
                {
                    // Reuse depth display function from DebugViewMaterial
                    int2 mipOffset = _DebugDepthPyramidOffsets[_DebugDepthPyramidMip];
                    uint2 pixCoord = (uint2)input.positionCS.xy >> _DebugDepthPyramidMip;
                    float depth = LOAD_TEXTURE2D_X(_CameraDepthTexture, pixCoord + mipOffset).r;
                    PositionInputs posInput = GetPositionInput(input.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
                    float linearDepth = frac(posInput.linearDepth * 0.1);
                    return float4(linearDepth.xxx, 1.0);
                }

                return float4(0.0, 0.0, 0.0, 0.0);
            }

            ENDHLSL
        }

    }
    Fallback Off
}
