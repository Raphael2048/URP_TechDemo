
using System;
using UnityEngine;

namespace IrradianceVolume
{
    public class IrradianceVolumeImportantArea : MonoBehaviour
    {
        public Vector3 size;

        public void OnDrawGizmos()
        {
            Gizmos.DrawWireCube(transform.position, size);
        }
    }
}
