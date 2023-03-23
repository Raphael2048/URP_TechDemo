using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;

namespace GPUBaking
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class DawnRectLight : DawnBaseLight {
		public float AttenuationRadius = 0;
		[HideInInspector]
		public float barnDoorLength = 0.2f;
		[HideInInspector]
		public float barnDoorAngle = 88;
		public float lightFalloffExponent = 1.0f;

		public float Height = 1;
		public float Width = 1;


		void Start()
		{
			type = LightType.Rectangle;
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

				// draw rectangle
				float halfHeight = Height / 2;
				float halfWidth = Width / 2;
				Vector3 leftUpPoint = transform.position + transform.up * halfHeight - transform.right * halfWidth;
				Vector3 rightUpPoint = transform.position + transform.up * halfHeight + transform.right * halfWidth;
				Vector3 leftDownPoint = transform.position - transform.up * halfHeight - transform.right * halfWidth;
				Vector3 rightDownPoint = transform.position - transform.up * halfHeight + transform.right * halfWidth;

				Gizmos.DrawLine(leftUpPoint, rightUpPoint);
				Gizmos.DrawLine(rightUpPoint, rightDownPoint);
				Gizmos.DrawLine(rightDownPoint, leftDownPoint);
				Gizmos.DrawLine(leftDownPoint, leftUpPoint);
			}
		}
	}


}

#endif