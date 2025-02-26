Shader "Hidden/HDRP/Sky/PbrSky"
{
    HLSLINCLUDE

    #pragma vertex Vert

    // #pragma enable_d3d11_debug_symbols
    #pragma target 4.5
    #pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightDefinition.cs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/PhysicallyBasedSky/PhysicallyBasedSkyCommon.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/AtmosphericScattering/AtmosphericScattering.hlsl"

    int _HasGroundAlbedoTexture;    // bool...
    int _HasGroundEmissionTexture;  // bool...
    int _HasSpaceEmissionTexture;   // bool...

    // Sky framework does not set up global shader variables (even per-view ones),
    // so they can contain garbage. It's very difficult to not include them, however,
    // since the sky framework includes them internally in many header files.
    // Just don't use them. Ever.
    float3   _WorldSpaceCameraPos1;
    float4x4 _ViewMatrix1;

    // 3x3, but Unity can only set 4x4...
    float4x4 _PlanetRotation;
    float4x4 _SpaceRotation;

    TEXTURECUBE(_GroundAlbedoTexture);
    TEXTURECUBE(_GroundEmissionTexture);
    TEXTURECUBE(_SpaceEmissionTexture);

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID, UNITY_RAW_FAR_CLIP_VALUE);
        return output;
    }

    // TODO: we must write depth for the planet!
    // What about depth prepass??
    float4 RenderSky(Varyings input)
    {
        const float  R = _PlanetaryRadius;
        // TODO: Not sure it's possible to precompute cam rel pos since variables
        // in the two constant buffers may be set at a different frequency?
        const float3 O = _WorldSpaceCameraPos1 * 0.001 - _PlanetCenterPosition; // Convert m to km
        const float3 V = GetSkyViewDirWS(input.positionCS.xy);

        float3 N; float r; // These params correspond to the entry point
        float tEntry = IntersectAtmosphere(O, V, N, r);

        float NdotV  = dot(N, V);
        float cosChi = -NdotV;
        float cosHor = ComputeCosineOfHorizonAngle(r);

        bool rayIntersectsAtmosphere = (tEntry >= 0);
        bool lookAboveHorizon        = (cosChi > cosHor);

        float3 gN = 0, gBrdf = 0, transm = 1;

        float3 totalRadiance = 0;

        if (rayIntersectsAtmosphere)
        {
            if (!lookAboveHorizon) // See the ground?
            {
                float tExit = tEntry + IntersectSphere(R, cosChi, r).x;

                float3 gP = O + tExit * -V;
                       gN = normalize(gP);

                float3 albedo;

                if (_HasGroundAlbedoTexture)
                {
                    albedo = SAMPLE_TEXTURECUBE(_GroundAlbedoTexture, s_trilinear_clamp_sampler, mul(gN, (float3x3)_PlanetRotation));
                }
                else
                {
                    albedo = _GroundAlbedo;
                }

                gBrdf = INV_PI * albedo;
            }

            if (!lookAboveHorizon) // See the ground?
            {
                for (uint i = 0; i < _DirectionalLightCount; i++)
                {
                    if (!_DirectionalLightDatas[i].interactsWithSky) continue;

                    float3 L             = -_DirectionalLightDatas[i].forward.xyz;
                    float3 lightRadiance =  _DirectionalLightDatas[i].color.rgb;

                    float3 radiance = 0;

                    float3 irradiance = SampleGroundIrradianceTexture(dot(gN, L));
                    radiance += gBrdf * irradiance;
                    radiance *= lightRadiance;      // Globally scale the intensity

                    totalRadiance += radiance;
                }
            }
        }

        float3 emission = 0;

        if (rayIntersectsAtmosphere && !lookAboveHorizon) // See the ground?
        {
            if (_HasGroundEmissionTexture)
            {
                emission = SAMPLE_TEXTURECUBE(_GroundEmissionTexture, s_trilinear_clamp_sampler, mul(gN, (float3x3)_PlanetRotation));
            }
        }
        else // See the space?
        {
            if (_HasSpaceEmissionTexture)
            {
                // V points towards the camera.
                emission = SAMPLE_TEXTURECUBE(_SpaceEmissionTexture, s_trilinear_clamp_sampler, mul(-V, (float3x3)_SpaceRotation));
            }
        }

        totalRadiance += emission;

        float3 skyColor, skyOpacity;

        // Evaluate the sky at infinity.
        EvaluatePbrAtmosphere(V, FLT_INF, UNITY_RAW_FAR_CLIP_VALUE,
                              _WorldSpaceCameraPos1, _ViewMatrix1,
                              skyColor, skyOpacity);

        skyColor += totalRadiance * (1 - skyOpacity);
        skyColor *= _IntensityMultiplier * GetCurrentExposureMultiplier();

        return float4(skyColor, 1.0);
    }

    float4 FragBaking(Varyings input) : SV_Target
    {
        return RenderSky(input);
    }

    float4 FragRender(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        float4 color = RenderSky(input);
        color.rgb *= GetCurrentExposureMultiplier();
        return color;
    }

    ENDHLSL

    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragBaking
            ENDHLSL

        }

        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FragRender
            ENDHLSL
        }

    }
    Fallback Off
}
