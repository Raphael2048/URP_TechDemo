using System;
using System.Diagnostics;

namespace UnityEngine.Rendering.Universal
{
    
    [Serializable, DebuggerDisplay(k_DebuggerDisplay)]
    public class Texture3DParameter : VolumeParameter<Texture3D>
    {
        public Texture3DParameter(Texture3D value, bool overrideState = false)
            : base(value, overrideState) {}
    }
    [Serializable, VolumeComponentMenu("Custom/VolumetricFog")]
    public class VolumetricFog : VolumeComponent, IPostProcessComponent
    {
        public ColorParameter ForwardScatteringColor = new ColorParameter(Color.grey);
        public ColorParameter BackwardScatteringColor = new ColorParameter(Color.grey);
        public ColorParameter AmbientLight = new ColorParameter(new Color(0.5f, 0.5f, 0.5f));
        
        //外部线性雾
        public ClampedFloatParameter OutsideIntensity = new ClampedFloatParameter(0, 0, 10);
        public ClampedFloatParameter HeightFallOff = new ClampedFloatParameter(0, 0, 0.5f);
        public ClampedFloatParameter MaxFogDistance = new ClampedFloatParameter(300, 50, 1000);
        public ClampedFloatParameter HorizonHeight = new ClampedFloatParameter(0, -10, 10);
        
        //内部体积雾
        public ClampedFloatParameter InnerIntensity = new ClampedFloatParameter(0, 0, 10);
        public ClampedFloatParameter Distance = new ClampedFloatParameter(60f, 1, 300);
        public BoolParameter IgnoreSkybox = new BoolParameter(false);
        public Texture3DParameter Noise = new Texture3DParameter(null);
        public ClampedFloatParameter NoiseTiling = new ClampedFloatParameter(1, 0.05f, 20);
        public Vector3Parameter NoiseSpeed = new Vector3Parameter(Vector3.right);
        public Texture3DParameter Detail = new Texture3DParameter(null);
        public ClampedFloatParameter DetailTiling = new ClampedFloatParameter(1, 0.05f, 20);
        public Vector3Parameter DetailSpeed = new Vector3Parameter(Vector3.left);
        public ClampedFloatParameter DetailIntensity = new ClampedFloatParameter(0.5f, 0, 1);
        public BoolParameter EnableVolumetricLight = new BoolParameter(false);
        public ClampedIntParameter VolumetricFogQuality = new ClampedIntParameter(1, 1, 3);

        public bool IsActive()
        {
            return OutsideIntensity.value > 0 || InnerIntensity.value > 0;
        }
        
        public bool IsTileCompatible()
        {
            return false;
        }
    }
}