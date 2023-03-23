using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor.SceneManagement;

namespace GPUBaking
{
	[CustomEditor(typeof(DawnLightmapGroupSelector))]
	[CanEditMultipleObjects]
	public class DawnLightmapGroupInspector  : UnityEditor.Editor{

		int SelectedAtlasSize = -1;

		DawnLightmapPackMode SelectedPackMode = DawnLightmapPackMode.Default;

		SerializedProperty groupAsset;

		private string NewGroupName = ""; 
		void OnEnable()
		{
			groupAsset = serializedObject.FindProperty("groupAsset");
		}
		public override void OnInspectorGUI()
		{
			serializedObject.Update ();

			bool bChanged = EditorGUILayout.PropertyField(groupAsset, new GUIContent("Group Asset", "Select Lightmap Group Asset"));

			serializedObject.ApplyModifiedProperties();

			var GroupSelector = serializedObject.targetObject as DawnLightmapGroupSelector;

			if (GroupSelector.groupAsset == null) {
				EditorGUILayout.Space ();

				if (NewGroupName == "")
				{
					NewGroupName = GroupSelector.name;
				}
				NewGroupName = EditorGUILayout.TextField ("New Group Name", NewGroupName);
				
				DawnSettings DawnSetting = Dawn4Unity.GetLightingSetting ();

				if (SelectedAtlasSize <= 0) {
					SelectedAtlasSize = DawnSetting.AtlasSettings.MaxLightmapSize;
				}

				SelectedAtlasSize = EditorGUILayout.IntField ("Atlas Size", SelectedAtlasSize);

				SelectedPackMode = (DawnLightmapPackMode)EditorGUILayout.EnumPopup("Pack Mode", SelectedPackMode);

				if (GUILayout.Button ("Create New Group")) {
					GroupSelector.groupAsset = CreateNewLightmapGroup ("LM_Group_" + NewGroupName, SelectedAtlasSize, SelectedPackMode);
					bChanged = true;
				}
			} else {
				EditorGUILayout.LabelField ("Group Name",GroupSelector.groupAsset.GroupName);
				EditorGUILayout.LabelField ("Atlas Size",GroupSelector.groupAsset.AtlasSize.ToString());
				EditorGUILayout.LabelField("Pack Mode", GroupSelector.groupAsset.PackMode.ToString());
			}

			if(bChanged)
            {
				EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
			}			
		}

		DawnLightmapGroupAsset CreateNewLightmapGroup(string GroupName,int AtlasSize,DawnLightmapPackMode PackMode)
		{
			string GroupAssetDir = "Assets/DawnGroupAssets/";
			if(!Directory.Exists(GroupAssetDir))
			{
				AssetDatabase.CreateFolder("Assets", "DawnGroupAssets");
			}

			string AssetPath = GroupAssetDir + GroupName+".asset";
			var GroupAsset = ScriptableObject.CreateInstance<DawnLightmapGroupAsset>();
			GroupAsset.GroupName = GroupName;
			GroupAsset.AtlasSize = AtlasSize;
			GroupAsset.PackMode = PackMode;

			AssetDatabase.CreateAsset(GroupAsset, AssetPath);

			AssetDatabase.Refresh ();
			return GroupAsset;
		}
	}
}