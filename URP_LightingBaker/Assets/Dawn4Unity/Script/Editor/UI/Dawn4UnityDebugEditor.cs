using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting;
using System;
using System.IO;
using GPUBaking;
using GPUBaking.Editor;

[ExecuteInEditMode]
[InitializeOnLoad]
public class Dawn4UnityDebugEditor
{
	static Dawn4UnityDebugEditor()
	{
	#if UNITY_5 || UNITY_2017 || UNITY_2018
		SceneView.onSceneGUIDelegate += OnSceneGUI;
	#else
		SceneView.duringSceneGui += OnSceneGUI;
	#endif
	}

	static Transform DebugPosition;
	public struct DebugSelectionLightmap
	{
		public FGuidInfo MeshID;
		public int2 	 DebugUV;
		public int2 	 DebugSize;

		public Renderer Renderer;
		public Vector3 HitPoint;
		public Vector3 HitNormal;
		public Vector2 LightmapCoords;
	}

	public static  DebugSelectionLightmap SelectionHitInfo;

	static void OnSceneGUI(SceneView sceneView)
    {
		var DebugSetting = Dawn4Unity.GetDebugSetting();

		if (DebugSetting == null || !DebugSetting.bDebugLightmapTexel)
        {
			return;
        }

		var e = Event.current;
		
		if (e.button == 1 && e.type == EventType.MouseDown) {

			var Ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

			RaycastHit HitInfo;

			if (Physics.Raycast (Ray, out HitInfo)) {
				SelectionHitInfo.MeshID = new FGuidInfo(0,0,0,0);
				SelectionHitInfo.Renderer = HitInfo.collider.GetComponent<Renderer>();
				SelectionHitInfo.HitPoint = HitInfo.point;
				SelectionHitInfo.HitNormal = HitInfo.normal;
				SelectionHitInfo.LightmapCoords = HitInfo.textureCoord2;

				Debug.LogFormat ("object({0}) hit:{1},normal:{2},triangle:{3},uv:({4:N4},{5:N4})",
					HitInfo.collider.name,
					SelectionHitInfo.HitPoint,
					SelectionHitInfo.HitNormal,
					HitInfo.triangleIndex,
					SelectionHitInfo.LightmapCoords.x,SelectionHitInfo.LightmapCoords.y);
				/*
				if (SelectionHitInfo.Renderer.lightmapIndex >= 0) {
					var Lightmaps = LightmapSettings.lightmaps;
					var Lightmap = Lightmaps[SelectionHitInfo.Renderer.lightmapIndex];
					var lightmapColor = TextureUtils.CopyTexture (Lightmap.lightmapColor,Lightmap.lightmapColor.width,Lightmap.lightmapColor.height);
					int X = Mathf.RoundToInt(SelectionHitInfo.LightmapCoords.x * Lightmap.lightmapColor.width);
					int Y = Mathf.RoundToInt(SelectionHitInfo.LightmapCoords.y * Lightmap.lightmapColor.height);
					lightmapColor.SetPixel (X, Y, Color.red);
					//lightmapColor.Apply ();
					Lightmap.lightmapColor = lightmapColor;
					LightmapSettings.lightmaps = Lightmaps;
				}
				*/				
			}		
		}

		if(DebugSetting!=null && DebugSetting.bDebugLightmapTexel && DebugSetting.RayTracingSettings != null && DebugSetting.RayTracingSettings.bShowDebugRay)
        {
			DrawDebugRays(DebugSetting.RayTracingSettings);
		}		
	}

	static Color[] BouncesColor = new Color[]{
		Color.red,
		Color.green,
		Color.blue,
		Color.yellow,
		new Color(243/255.0f, 156/255.0f, 18/255.0f),
		Color.cyan,
		Color.magenta
	};
	static void DrawDebugRays(RayTracingDebugSettings DebugSettings)
	{
		var GDebugPathVerticeList = DawnLightingSystem.GDebugPathVerticeList;		

		var restoreColor = Handles.color;

		if (GDebugPathVerticeList != null && GDebugPathVerticeList.NumElements > 1)
		{
			var FirstVertex = GDebugPathVerticeList[0];

			bool FristValidPathFlag = false;
			int ValidSpp = 0;

			//Debug.LogFormat("draw debug rays {0}", ToVector3(ref FirstVertex.Position));

			for (int Index = 0; Index < (GDebugPathVerticeList.NumElements - 1) / 2; ++Index)
			{
				var PathVertex = GDebugPathVerticeList[Index * 2 + 1];
				var LightPathVertex = GDebugPathVerticeList[Index * 2 + 2];
				Color ThroughOutColor = ToColor(ref PathVertex.Color,1.0f);

				if (PathVertex.bValid != 0)
				{
					if (PathVertex.Bounces == 0)
					{
						if (FristValidPathFlag)
						{
							ValidSpp++;
							if (ValidSpp >= DebugSettings.DebugSamplesPerPixel)
							{
								break;
							}
						}
						else
						{
							FristValidPathFlag = true;
						}
						if (DebugSettings.bDrawPoints)
						{
							DrawPoint(ref PathVertex, DebugSettings.bDrawAlbedoColor ? ThroughOutColor : Color.red, DebugSettings.DebugMaxBounces);
						}
						else
						{
							DrawArrowLine(ref FirstVertex, ref PathVertex, DebugSettings.bDrawAlbedoColor ? ThroughOutColor : Color.red, DebugSettings.DebugMaxBounces);
						}
					}
					else if (PathVertex.Bounces < DebugSettings.DebugMaxBounces)
					{
						if (PathVertex.Bounces < BouncesColor.Length && !DebugSettings.bDrawAlbedoColor)
						{
							ThroughOutColor = BouncesColor[PathVertex.Bounces];

						}
						var PreVertex = GDebugPathVerticeList[(Index - 1) * 2 + 1];
						if (DebugSettings.bDrawPoints)
						{
							DrawPoint(ref PathVertex, ThroughOutColor, DebugSettings.DebugMaxBounces);
						}
						else
						{
							DrawArrowLine(ref PreVertex, ref PathVertex, ThroughOutColor, DebugSettings.DebugMaxBounces);
						}
					}

					if (PathVertex.Bounces < DebugSettings.DebugMaxBounces)
					{
						if (LightPathVertex.bValid == 2 || LightPathVertex.bValid == 4 && DebugSettings.bDrawShadowLight)
						{
							if (DebugSettings.bDrawLightColor)
							{
								DrawArrowLine(ref PathVertex, ref LightPathVertex, ToColor(ref LightPathVertex.Color), DebugSettings.DebugMaxBounces);
							}
							else
							{
								DrawArrowLine(ref PathVertex, ref LightPathVertex, Color.white, DebugSettings.DebugMaxBounces);
							}
						}
					}
				}
				else if(DebugSettings.bDrawSkyLightRay && LightPathVertex.bValid !=0 && LightPathVertex.Bounces == 0 && ValidSpp <= DebugSettings.DebugSamplesPerPixel)
                {
					ValidSpp++;
					if (DebugSettings.bDrawLightColor)
					{
						DrawArrowLine(ref FirstVertex, ref LightPathVertex, ToColor(ref LightPathVertex.Color), DebugSettings.DebugMaxBounces);
					}
					else
					{
						DrawArrowLine(ref FirstVertex, ref LightPathVertex, Color.white, DebugSettings.DebugMaxBounces);
					}
				}
			}
		}
		Handles.color = restoreColor;
	}

	static void DrawArrowLine(ref FPathVertex Start, ref FPathVertex End,Color InColor, int MaxBounces) {
		float Bounces = Start.Bounces;
		float Thickness = (MaxBounces - Mathf.Min(MaxBounces - 1, Bounces)) / MaxBounces;

		Handles.color = InColor;
		#if !UNITY_2020
		Handles.DrawLine(ToVector3(ref Start.Position), ToVector3(ref End.Position));
		#else
		Handles.DrawLine(ToVector3(ref Start.Position), ToVector3(ref End.Position), Thickness);
		#endif
	}

	static void DrawPoint(ref FPathVertex Point, Color InColor, int MaxBounces) {
		Handles.color = InColor;
		Handles.DrawSolidDisc(ToVector3(ref Point.Position),Vector3.up,0.1f);
	}


	static Color ToColor(ref float3 Input, float Alpha = 1.0f)
	{
		return new Color(Input.x, Input.y, Input.z, Alpha);
	}

	static Vector3 ToVector3(ref float3 Input)
	{
		return new Vector4(Input.x, Input.y, Input.z);
	}
}
