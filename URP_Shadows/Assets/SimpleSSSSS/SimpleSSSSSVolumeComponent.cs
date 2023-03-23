using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
namespace UnityEngine.Rendering.Universal
{
    [Serializable, VolumeComponentMenu("Custom/SimpleSSSSS")]
    public class SimpleSSSSSVolumeComponent : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter Enabled = new BoolParameter(false);
        public ClampedFloatParameter Threshold = new ClampedFloatParameter(0.16f, 0, 1);
        public ClampedFloatParameter Intensity = new ClampedFloatParameter(0.4f, 0, 3);
        public ColorParameter MaxColor = new ColorParameter(new Color(2, 2, 2), true, false, false);

        public bool IsActive()
        {
            return Enabled.value && Intensity.value > 0;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }
}

