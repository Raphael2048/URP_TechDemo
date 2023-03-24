using System;
using UnityEngine;

namespace UnityEngine.Rendering.Universal
{
    public class RainAndSnowMaskCamera : MonoBehaviour
    {
        static RainAndSnowMaskCamera Instance;

        public static Camera GetInstanceCamera()
        {
            if (Instance == null)
            {
                var obj = new GameObject("TopDownCamera");
                Instance = obj.AddComponent<RainAndSnowMaskCamera>();
                obj.hideFlags = HideFlags.HideInHierarchy;
                obj.AddComponent<Camera>().enabled = false;
            }
            return Instance.GetComponent<Camera>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}