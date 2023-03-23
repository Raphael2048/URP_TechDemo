using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace GPUBaking
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class DawnSpotLight : DawnPointLight {

		[Range(1.0f, 179.0f)]
		public float innerConeAngle = 1.0f;

		void Start()
		{
			type = LightType.Spot;
		}

        private new void Update()
        {
			if(UnityLight != null && UpdateWithUnity)
            {
				base.Update();
				innerConeAngle = UnityLight.spotAngle;
			}					
        }
        public new void OnDrawGizmosSelected()
		{
			if (UnityLight == null)
			{
				Gizmos.color = Color.yellow;

				// draw arrow
				var endPoint = transform.position + transform.forward * 2;
				Gizmos.DrawLine(transform.position, endPoint);
				Gizmos.DrawLine(endPoint, endPoint + (transform.position + transform.right - endPoint).normalized * 0.3f);
				Gizmos.DrawLine(endPoint, endPoint + (transform.position - transform.right - endPoint).normalized * 0.3f);
				Gizmos.DrawLine(endPoint, endPoint + (transform.position + transform.up - endPoint).normalized * 0.3f);
				Gizmos.DrawLine(endPoint, endPoint + (transform.position - transform.up - endPoint).normalized * 0.3f);

				// draw circle
				Handles.color = Color.yellow;
				float radius = Mathf.Tan((innerConeAngle / 2 * Mathf.PI) / 180) * range;
				Vector3 circleCenter = transform.position + transform.forward * range;
				Handles.DrawWireDisc(circleCenter, transform.forward, radius);

				// draw subline
				Gizmos.DrawLine(transform.position, circleCenter + transform.up * radius);
				Gizmos.DrawLine(transform.position, circleCenter - transform.up * radius);
				Gizmos.DrawLine(transform.position, circleCenter + transform.right * radius);
				Gizmos.DrawLine(transform.position, circleCenter - transform.right * radius);
			}
		}
	}
}

#endif
