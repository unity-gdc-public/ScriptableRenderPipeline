using System;
using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.HDPipeline
{
    [Serializable, VolumeComponentMenu("Ray Tracing/Global Illumination")]
    public sealed class GlobalIllumination : VolumeComponent
    {
        [Tooltip("Enable. Enable ray traced global illumination.")]
        public BoolParameter enableRayTracing = new BoolParameter(false);

        [Tooltip("Controls the length of GI rays.")]
        public ClampedFloatParameter rayLength = new ClampedFloatParameter(10f, 0.001f, 50f);

        [Tooltip("Controls the clamp of intensity.")]
        public ClampedFloatParameter clampValue = new ClampedFloatParameter(1.0f, 0.001f, 10.0f);

        [Tooltip("Enables deferred mode")]
        public BoolParameter deferredMode = new BoolParameter(false);
        
        [Tooltip("Number of samples for GI.")]
        public ClampedIntParameter numSamples = new ClampedIntParameter(1, 1, 32);

        [Tooltip("Number of bounces for GI.")]
        public ClampedIntParameter numBounces = new ClampedIntParameter(1, 1, 31);

        [Tooltip("Enable Filtering on the raytraced GI.")]
        public BoolParameter enableFilter = new BoolParameter(false);

        [Tooltip("Use the diffuse denoiser instead of the simple one.")]
        public BoolParameter diffuseDenoiser = new BoolParameter(false);

        [Tooltip("Controls the radius of GI filtering (First Pass).")]
        public ClampedFloatParameter filterRadiusFirst = new ClampedFloatParameter(0.5f, 0.001f, 6.0f);

        [Tooltip("Enable second pass.")]
        public BoolParameter enableSecondPass = new BoolParameter(false);

        [Tooltip("Controls the radius of GI filtering (Second Pass).")]
        public ClampedFloatParameter filterRadiusSecond = new ClampedFloatParameter(1.0f, 0.001f, 6.0f);

        [Tooltip("Controls the number of samples used for filtering.")]
        public ClampedIntParameter filterSampleCount = new ClampedIntParameter(16, 1, 64);
    }
}
