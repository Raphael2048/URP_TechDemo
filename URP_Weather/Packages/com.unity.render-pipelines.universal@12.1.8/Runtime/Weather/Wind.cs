using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenuForRenderPipeline("Weather/Wind", typeof(UniversalRenderPipeline))]
    public sealed class Wind : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter WindDirection = new ClampedFloatParameter(0, 0, 360);

        public ClampedFloatParameter WindIntensity = new ClampedFloatParameter(0, 0, 1);
        
        public bool IsActive() => WindIntensity.value > 0;

        public bool IsTileCompatible() => false;
    }

}