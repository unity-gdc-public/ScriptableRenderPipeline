Shader "Hidden/Light2d-Point-Volumetric"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma prefer_hlslcc gles
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local USE_POINT_LIGHT_COOKIES __
            #pragma multi_compile_local LIGHT_QUALITY_FAST __

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD0;
            };

            struct Varyings
            {
                float4  positionCS      : SV_POSITION;
                float2  uv              : TEXCOORD0;
                float2	screenUV        : TEXCOORD1;
                float2	lookupUV        : TEXCOORD2;  // This is used for light relative direction
                float2	lookupNoRotUV   : TEXCOORD3;  // This is used for screen relative direction of a light

#if LIGHT_QUALITY_FAST
                float4	lightDirection	: TEXCOORD4;
#else
                float4	positionWS : TEXCOORD4;
#endif
            };

#if USE_POINT_LIGHT_COOKIES
            TEXTURE2D(_PointLightCookieTex);
            SAMPLER(sampler_PointLightCookieTex);
#endif

            TEXTURE2D(_FalloffLookup);
            SAMPLER(sampler_FalloffLookup);
            float _FalloffIntensity;

            TEXTURE2D(_LightLookup);
            SAMPLER(sampler_LightLookup);
            float4 _LightLookup_TexelSize;

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            half4	_LightColor;
            float   _VolumeOpacity;
            float4	_LightPosition;
            half4x4	_LightInvMatrix;
            half4x4	_LightNoRotInvMatrix;
            half	_LightZDistance;
            half	_OuterAngle;			// 1-0 where 1 is the value at 0 degrees and 1 is the value at 180 degrees
            half	_InnerAngleMult;			// 1-0 where 1 is the value at 0 degrees and 1 is the value at 180 degrees
            half	_InnerRadiusMult;			// 1-0 where 1 is the value at the center and 0 is the value at the outer radius
            half	_InverseHDREmulationScale;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.texcoord;

                float4 worldSpacePos;
                worldSpacePos.xyz = TransformObjectToWorld(input.positionOS);
                worldSpacePos.w = 1;

                float4 lightSpacePos = mul(_LightInvMatrix, worldSpacePos);
                float4 lightSpaceNoRotPos = mul(_LightNoRotInvMatrix, worldSpacePos);
                float halfTexelOffset = 0.5 * _LightLookup_TexelSize.x;
                output.lookupUV = 0.5 * (lightSpacePos.xy + 1) + halfTexelOffset;
                output.lookupNoRotUV = 0.5 * (lightSpaceNoRotPos.xy + 1) + halfTexelOffset;

#if LIGHT_QUALITY_FAST
                output.lightDirection.xy = _LightPosition.xy - worldSpacePos.xy;
                output.lightDirection.z = _LightZDistance;
                output.lightDirection.w = 0;
                output.lightDirection.xyz = normalize(output.lightDirection.xyz);
#else
                output.positionWS = worldSpacePos;
#endif

                float4 clipVertex = output.positionCS / output.positionCS.w;
                output.screenUV = ComputeScreenPos(clipVertex).xy;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 normal = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.screenUV);
                half4 lookupValueNoRot = SAMPLE_TEXTURE2D(_LightLookup, sampler_LightLookup, input.lookupNoRotUV);  // r = distance, g = angle, b = x direction, a = y direction
                half4 lookupValue = SAMPLE_TEXTURE2D(_LightLookup, sampler_LightLookup, input.lookupUV);  // r = distance, g = angle, b = x direction, a = y direction

                // Inner Radius
                half attenuation = saturate(_InnerRadiusMult * lookupValueNoRot.r);   // This is the code to take care of our inner radius

                // Spotlight
                half  spotAttenuation = saturate((_OuterAngle - lookupValue.g) * _InnerAngleMult);
                attenuation = attenuation * spotAttenuation;

                half2 mappedUV;
                mappedUV.x = attenuation;
                mappedUV.y = _FalloffIntensity;
                attenuation = SAMPLE_TEXTURE2D(_FalloffLookup, sampler_FalloffLookup, mappedUV).r;

#if USE_POINT_LIGHT_COOKIES
                half4 cookieColor = SAMPLE_TEXTURE2D(_PointLightCookieTex, sampler_PointLightCookieTex, input.lookupUV);
                half4 lightColor = cookieColor * _LightColor * attenuation;
#else
                half4 lightColor = _LightColor * attenuation;
#endif

                return _VolumeOpacity * lightColor * _InverseHDREmulationScale;
            }
            ENDHLSL
        }
    }
}
