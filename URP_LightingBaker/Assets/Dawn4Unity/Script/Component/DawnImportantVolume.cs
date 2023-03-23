using System;
using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
    [ExecuteInEditMode]
    public class DawnImportantVolume : MonoBehaviour
    {
        public Color debugColor = Color.blue;
        public Vector3 size = Vector3.one;

        private void Update()
        {
            transform.rotation = Quaternion.identity;
        }

        void OnDrawGizmosSelected()
        {
            var bounds = VolumeBounds;
            Gizmos.color = debugColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        public Bounds VolumeBounds
        {
            get {
                return new Bounds(transform.position,transform.TransformVector(size));
            }
        }
    }
}

#endif