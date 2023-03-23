using System;
using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
	[RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class DawnLightMesh : DawnMeshComponent{
#if UNITY_EDITOR
		public bool bUseInverseSquaredFalloff = true;
        public float LightSourceRadius = 1.0f;
        public Color EmissiveColor = Color.white;
        public float EmissiveIntensity = 1.0f;
        public float IndirectIntensity = 1.0f;
        public Material EmissiveMaterial = null;
        public float LightFalloffExponent = 1.0f;
#endif
    }
}

#endif