using UnityEngine;

namespace GPUBaking
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public abstract class  DawnBaseLight : MonoBehaviour {
        public bool UpdateWithUnity = false;
		public bool bUseTemperature = false;

		// setup some key parameters as same as Unity Light
        #region BasicLightInfo
		public enum BakeMode
        {
			Mixed = 1,
			Baked = 2,
			Realtime = 4
		}
		/// <summary>
		/// use to show bake mode in the inspector.
		/// </summary>
		public BakeMode mode = BakeMode.Baked;
		[HideInInspector]
        public LightmapBakeType lightmapBakeType = LightmapBakeType.Baked;
		public LightShadows shadows = LightShadows.None;
		public Color color = Color.white;
		public float intensity = 1.0f;
		public float indirectMultiplier = 1.0f;
		public float range = 10;
		[HideInInspector]
		public LightType type = LightType.Directional;

		#endregion


		#region DawnLightInfo
		[HideInInspector]
		public int LightIndex;
		[HideInInspector]
		public int ChannelIndex;
		[HideInInspector]
		public int LightingMask;
		[HideInInspector]
		public int BakedMode;
        #endregion
#if UNITY_EDITOR
        [HideInInspector]
		public Light UnityLight;

        protected virtual void Update()
		{
			if(UnityLight == null)
			{
				UnityLight = GetComponent<Light>();
			}
			
			if(UnityLight!=null && UpdateWithUnity)
			{
				lightmapBakeType = UnityLight.lightmapBakeType;
				mode = (BakeMode)lightmapBakeType;
				shadows = UnityLight.shadows;
				color = UnityLight.color;
				intensity = UnityLight.intensity;
				indirectMultiplier = UnityLight.bounceIntensity;				
				range = UnityLight.range;
			}
			else
            {
				UpdateWithUnity = false;
            }
		}

		#endif
	}
}