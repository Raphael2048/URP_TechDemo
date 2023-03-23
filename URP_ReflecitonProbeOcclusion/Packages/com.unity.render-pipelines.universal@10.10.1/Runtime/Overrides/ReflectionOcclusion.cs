using System;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Custom/ReflectionProbeOcclusion")]
    public sealed class ReflectionProbeOcclusion : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Enable")]
        public BoolParameter enabled = new BoolParameter(false);

        [Tooltip("Roughness Start")]
        public ClampedFloatParameter roughnessStart = new ClampedFloatParameter(0.1f, 0f, 1f);
        
        [Tooltip("Roughness End")]
        public ClampedFloatParameter roughnessEnd = new ClampedFloatParameter(0.3f, 0f, 1f);
        
        [Tooltip("Max Weight")]
        public ClampedFloatParameter maxWeight = new ClampedFloatParameter(1f, 0f, 1f);
        
        // [Tooltip("Reflection Intensity")]
        // public ClampedFloatParameter reflectionIntensity = new ClampedFloatParameter(1f, 0f, 2f);

        public bool IsActive() => enabled.value && (maxWeight.value > 0);

        public bool IsTileCompatible() => false;
    }
}