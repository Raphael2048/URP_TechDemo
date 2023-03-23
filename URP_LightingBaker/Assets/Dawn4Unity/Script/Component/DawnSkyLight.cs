using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR

namespace GPUBaking
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public class DawnSkyLight : MonoBehaviour {
	#if UNITY_EDITOR
		public Cubemap skyTexture;
		public Color color = Color.white;
		public float intensity = 1.0f;
		public float indirectMultiplier = 1.0f;

		protected void UpdateWithUnity()
		{
			if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Skybox) {
				skyTexture = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;
				color = Color.white;
				intensity = RenderSettings.ambientIntensity;
			} else {
				color = RenderSettings.ambientSkyColor;
				intensity = 1.0f;
				skyTexture = null;
			}
			indirectMultiplier = RenderSettings.reflectionIntensity;
		}

		protected void CaptureReflection()
		{
			Cubemap cubemap = new Cubemap (128, TextureFormat.RGBA32, false);
			cubemap.name = "CaptureReflection";
			// Create a temporary camera to render a skybox cubemap
			var lastActiveCamera = SceneView.lastActiveSceneView.camera;
			GameObject go=new GameObject("DawnCubemapCamera");
			go.AddComponent<Camera>();
			go.transform.position = lastActiveCamera.gameObject.transform.position;
			var renderCamera = go.GetComponent<Camera> ();
			// ignore ohter objects
			renderCamera.cullingMask = 0;
			renderCamera.RenderToCubemap(cubemap);
			MonoBehaviour.DestroyImmediate (go);

			skyTexture = cubemap;
		}
		#endif
	}
}

#endif