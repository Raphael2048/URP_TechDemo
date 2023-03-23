using System;
using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class DawnMeshComponent : MonoBehaviour{
		[HideInInspector]
		[SerializeField]
        private float SurfaceArea = -1.0f;

        /// <summary>
        /// xy: Min UV
        /// zw: Max UV
        /// </summary>
        public Vector4 UVBounds = new Vector4(1, 1, 0, 0);
        public float GetCachedSurfaceArea(MeshFilter InMeshFilter)
		{
			if(SurfaceArea < 0 || InMeshFilter.GetComponentInParent<Transform>().hasChanged == true)
            {
				SurfaceArea = CalculateSurfaceArea(InMeshFilter);
            }
            return SurfaceArea;
		}

        public void ClearCachedSurfaceArea()
        {
            SurfaceArea = -1.0f;
        }

		float CalculateSurfaceArea(MeshFilter InMeshFilter)
		{
			DawnProfiler.BeginSample("CalculateSurfaceArea");
			
			// ensure update the SurfaceArea when transform has been changed
			InMeshFilter.GetComponentInParent<Transform>().hasChanged = false;
			
			var Mesh = InMeshFilter.sharedMesh;
			var objectToWorld =  InMeshFilter.transform.localToWorldMatrix;

			var vertices = (Vector3[])Mesh.vertices.Clone ();
			var triangles = Mesh.triangles;

			// transform the vertices to world space,
			// do it in place since they are a copy
			for (int i = 0; i < vertices.Length; i++)
				vertices[i] = objectToWorld.MultiplyPoint(vertices[i]);

			// calculate the area
			float cachedSurfaceArea = 0;
			for (int i = 0; i < triangles.Length / 3; i++)
			{
				Vector3 a = vertices[triangles[3 * i]];
				Vector3 b = vertices[triangles[3 * i + 1]];
				Vector3 c = vertices[triangles[3 * i + 2]];
				cachedSurfaceArea += (Vector3.Cross(b - a, c - a)).magnitude * 0.5f;
			}
			DawnProfiler.EndSample();
			return cachedSurfaceArea;
		}

        public Vector4 GetUVBounds(MeshFilter InMeshFilter)
        {
	        return CalculateUVBounds(InMeshFilter);
        }
        Vector4 CalculateUVBounds(MeshFilter InMeshFilter)
        {
	        try
	        {
		        if (UVBounds.Equals(new Vector4(1, 1, 0, 0)))
		        {
					DawnProfiler.BeginSample("CalculateUVBounds");
			        Vector2[] uv = InMeshFilter.sharedMesh.uv2;
			        if (uv.Length == 0)
			        {
				        // in case mesh doesn't have uv2 channel
				        uv = InMeshFilter.sharedMesh.uv;
			        }
					UVBounds = MeshUtils.CalculateUVBounds(uv);
					DawnProfiler.EndSample();
		        }
	        }
	        catch (Exception e)
	        {
		        DawnDebug.Print("Calculate mesh UV bounds error! Message: {0}", e.Message);
	        }

	        return UVBounds;
        }
		public void ClearUVBounds()
        {
	        UVBounds = new Vector4(1,1,0,0);
        }
    }

	public class MeshUtils
    {
		public static Vector4 CalculateUVBounds(Vector2[] uvs)
		{
			Vector4 UVBounds = new Vector4(1, 1, 0, 0);
			foreach (var v in uvs)
			{
				// min UV
				if (v.x < UVBounds.x)
				{
					UVBounds.x = (v.x);
				}
				if (v.y < UVBounds.y)
				{
					UVBounds.y = (v.y);
				}

				// max UV
				if (v.x > UVBounds.z)
				{
					UVBounds.z = (v.x);
				}
				if (v.y > UVBounds.w)
				{
					UVBounds.w = (v.y);
				}
			}

			if(UVBounds.z - UVBounds.x > 1.0f)
            {
				UVBounds.x = 0;
				UVBounds.z = 1.0f;
			}
			if (UVBounds.w - UVBounds.y > 1.0f)
			{
				UVBounds.y = 0;
				UVBounds.w = 1.0f;
			}
			return UVBounds;
		}
	}
}

#endif