using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR

namespace GPUBaking
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class DawnProbeGenerationSelector : MonoBehaviour
    {
        public bool bGeneration = true;
    }
}

#endif