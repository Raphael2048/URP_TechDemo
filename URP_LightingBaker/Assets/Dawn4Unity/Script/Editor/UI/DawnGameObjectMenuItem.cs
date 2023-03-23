using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GPUBaking.Editor
{
    /// <summary>
    /// Set up the Unity GameObject Menu/ Hierarchy Menu for Dawn Lights
    /// </summary>
    class DawnGameObjectMenuItem
    {
        [MenuItem("GameObject/Dawn/Directional Light", false, 30)]
        static void CreateDawnDirectionalLight()
        {
            GameObject go = new GameObject("Directional Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            go.AddComponent<DawnDirectionalLight>();
            go.transform.position = Vector3.zero;
        }

        [MenuItem("GameObject/Dawn/Point Light", false, 30)]
        static void CreateDawnPointLight()
        {
            GameObject go = new GameObject("Point Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            go.AddComponent<DawnPointLight>();
            go.transform.position = Vector3.zero;
        }

        [MenuItem("GameObject/Dawn/Rectangle Light", false, 30)]
        static void CreateDawnRectLight()
        {
            GameObject go = new GameObject("Rectangle Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Rectangle;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            go.AddComponent<DawnRectLight>();
            go.transform.position = Vector3.zero;
        }

        [MenuItem("GameObject/Dawn/Spot Light", false, 30)]
        static void CreateDawnSpotLight()
        {
            GameObject go = new GameObject("Spot Light");
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            go.AddComponent<DawnSpotLight>();
            go.transform.position = Vector3.zero;
        }

        [MenuItem("GameObject/Dawn/Sky Light", false, 30)]
        static void CreateDawnSkyLight()
        {
            GameObject go = new GameObject("Sky Light");
            go.AddComponent<DawnSkyLight>();
            go.transform.position = Vector3.zero;
        }
    }
}
