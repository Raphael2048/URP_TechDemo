using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor.SceneManagement;

namespace GPUBaking
{
	[CustomEditor(typeof(DawnSkyLight))]
	[CanEditMultipleObjects]
	public class DawnSkyLightInspector  : UnityEditor.Editor{

		SerializedProperty color;
		SerializedProperty skyTexture;
		SerializedProperty indirectMultiplier;

		void OnEnable()
		{
			color = serializedObject.FindProperty("color");
			skyTexture = serializedObject.FindProperty("skyTexture");
			indirectMultiplier = serializedObject.FindProperty("indirectMultiplier");
		}
		public override void OnInspectorGUI()
		{
			serializedObject.Update ();

			EditorGUILayout.PropertyField(color, new GUIContent("Color", "Sky color. Multiplies texture color."));
			EditorGUILayout.PropertyField(indirectMultiplier, new GUIContent("Indirect intensity", "Indirect multiplier"));
			EditorGUILayout.PropertyField(skyTexture, new GUIContent("Sky texture", "Cubemap"));

			serializedObject.ApplyModifiedProperties();

			bool skyboxValid = true;
			string why = "";
			skyboxValid = CheckSkyLight (out why);

			var SkyLight = serializedObject.targetObject as DawnSkyLight;

			EditorGUILayout.Space();

			if (GUILayout.Button("Match this light to scene skybox")) {
				
				SkyLight.SendMessage ("UpdateWithUnity");
			}

			EditorGUILayout.Space();

			if (GUILayout.Button ("Capture Reflection")) {
				SkyLight.SendMessage ("CaptureReflection");
			}
		}

		bool CheckSkyLight(out string why)
		{
			var skyMat = RenderSettings.skybox;
			bool skyboxValid = true;
			why = "";
			if (skyMat != null)
			{
				if (skyMat.HasProperty("_Tex") && skyMat.HasProperty("_Exposure") && skyMat.HasProperty("_Tint"))
				{
					if (skyMat.GetTexture("_Tex") == skyTexture.objectReferenceValue)
					{
						if (color.colorValue.r != RenderSettings.ambientIntensity
							|| color.colorValue.g != RenderSettings.ambientIntensity 
							|| color.colorValue.b != RenderSettings.ambientIntensity) {
							why = "intensity doesn't match";
							skyboxValid = false;
						}
					}
					else
					{
						why = "texture doesn't match";
						skyboxValid = false;
					}
				}
				else
				{
					if (!skyMat.HasProperty("_Tex")) why += "_Tex ";
					if (!skyMat.HasProperty("_Exposure")) why += "_Exposure ";
					if (!skyMat.HasProperty("_Tint")) why += "_Tint ";
					why += "not defined";
					skyboxValid = false;
				}
			}
			else
			{
				why = "no skybox set";
				skyboxValid = false;
			}
			return skyboxValid;
		}
	}
}