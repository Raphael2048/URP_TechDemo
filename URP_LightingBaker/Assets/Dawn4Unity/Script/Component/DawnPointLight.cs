using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class DawnPointLight : DawnBaseLight {
		public enum FalloffMode
		{
			InverseSquaredFalloff = 0,
			BakeryFalloff = 1,
			UnityFalloff = 2
		};
		public  FalloffMode falloffMode= 0;

		[Tooltip("light source point size, not the light effect range.")]
		public float sourceRadius = 0.0f;
		[HideInInspector]
		public float sourceLength = 0.0f;
		public float lightFalloffExponent = 1.0f;


        void Start()
        {
			type = LightType.Point;
		}
        public void OnDrawGizmosSelected()
		{
			if (UnityLight == null)
			{
				Gizmos.color = Color.yellow;

				// draw arrow
				Gizmos.DrawWireSphere(transform.position, range);				

			}
		}
	}
}

#endif
