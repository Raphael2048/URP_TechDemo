using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GPUBaking
{
    /// <summary>
    /// use to storage Dawn parameters which related to the scene, will be hid in Hierarchy.
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]   
    public class DawnSceneStorage : MonoBehaviour
    {
        /// <summary>
        /// record this scene's DawnSetting
        /// </summary>
        public DawnSettings DawnSetting = null;
    }
}
