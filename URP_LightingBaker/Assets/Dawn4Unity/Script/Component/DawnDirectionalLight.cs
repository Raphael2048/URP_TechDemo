using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace GPUBaking
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class DawnDirectionalLight : DawnBaseLight {
		[HideInInspector]
		public float LightSourceAngle = 1.0f;
		public float ShadowSpread = 0.01f;

		void Start()
		{
			type = LightType.Directional;
		}
		public void OnDrawGizmosSelected()
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
				Handles.DrawWireDisc(transform.position, transform.forward, 0.3f);

				// draw sublines
				var subline = transform.forward * 1.5f;
				Gizmos.DrawLine(transform.position + transform.up * 0.3f, transform.position + transform.up * 0.3f + subline);
				Gizmos.DrawLine(transform.position - transform.up * 0.3f, transform.position - transform.up * 0.3f + subline);
				Gizmos.DrawLine(transform.position + transform.right * 0.3f, transform.position + transform.right * 0.3f + subline);
				Gizmos.DrawLine(transform.position - transform.right * 0.3f, transform.position - transform.right * 0.3f + subline);

			}
		}

	}
}

#endif