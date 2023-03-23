using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting;
using System;
using System.IO;
using GPUBaking;
using GPUBaking.Editor;

[InitializeOnLoad]
public partial class Dawn4UnityWindow : EditorWindow
{
    
    static DawnSettingEditor SettingEditor;

	static EBakingState BakingState = EBakingState.IDLE;


    Vector2 scrollPosition;

    static Dawn4UnityWindow()
	{
		Dawn4Unity.OnUpdateEvent = UpdateUI;
		Dawn4Unity.OnCompleteEvent = ShowCompleteUI;
	}

    void OnGUI()
    {
		if (SettingEditor == null) {
			SettingEditor = new DawnSettingEditor ();
		}
		SettingEditor.SetSetting (Dawn4Unity.GetLightingSetting (), Dawn4Unity.GetDebugSetting());

        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition, GUILayout.Width(position.width), GUILayout.Height(position.height-50));
        SettingEditor.OnGUI ();
        GUILayout.EndScrollView();

        if (BakingState == EBakingState.IDLE) {
			if (GUILayout.Button (DawnLocalizationAsset.GetDisplayName("Start Bake"))) {     
                Dawn4Unity.BuildLighting();
			}
		} else {
			if (GUILayout.Button (DawnLocalizationAsset.GetDisplayName("Cancel Baking"))) {
				Dawn4Unity.CancelBuildLighting ();
			}
		}
    }


	static void UpdateUI(DawnLightingSystem LightingSystem)
	{
		BakingState = LightingSystem.BuildingState;
		EBakingError BuildError = LightingSystem.BuildError;
		string BuildErrorString = LightingSystem.BakingContext !=null ? LightingSystem.BakingContext.LastErrorInfo : string.Empty;
		bool bIsBuildSuccessed = LightingSystem.IsBuildSuccessed;

		if (BakingState == EBakingState.PENDING_STARTUP) {
			EditorUtility.DisplayProgressBar ("Dawn Starting", "Starting...", 0);
		}
		else if (BakingState == EBakingState.GATHERING) {
			EditorUtility.DisplayProgressBar ("Dawn Starting", "Gather...", 0.0f);
		}
		else if (BakingState == EBakingState.STARTING_UP || BakingState == EBakingState.PENDING_SWARM) {
			EditorUtility.DisplayProgressBar ("Dawn Starting", "Swarm Starting...", 0.25f);
		}
		else if (BakingState == EBakingState.EXPORTING || BakingState == EBakingState.SWARM_STARTED) {
			EditorUtility.DisplayProgressBar ("Dawn Starting", "Exporting...", 0.5f);
		}
		else if (BakingState == EBakingState.BUILDING)
		{
			if(EditorUtility.DisplayCancelableProgressBar ("Dawn Building"
				, string.Format ("Build Progress:{0}/{1}", LightingSystem.NumOfTaskCompleted, LightingSystem.NumOfTask)
				, (float)LightingSystem.NumOfTaskCompleted / (float)LightingSystem.NumOfTask))
			{
				Dawn4Unity.CancelBuildLighting ();
				EditorUtility.ClearProgressBar ();
			}
		}
		else if (BakingState == EBakingState.IMPORTING)
		{
			if(EditorUtility.DisplayCancelableProgressBar ("Dawn Importing"
				, string.Format ("Import Progress:{0}/{1}", LightingSystem.NumOfTaskImported, LightingSystem.NumOfTask)
				, (float)LightingSystem.NumOfTaskImported / (float)LightingSystem.NumOfTask))
			{
				Dawn4Unity.CancelBuildLighting();
				EditorUtility.ClearProgressBar ();
			}
		}
		else if (BakingState == EBakingState.ENCODING) {
			EditorUtility.DisplayProgressBar ("Dawn Completed", "Encoding...", 0.0f);
		}
		else if (BakingState == EBakingState.APPLYING) {
			EditorUtility.DisplayProgressBar ("Dawn Completed", "Applying...", 0.25f);
		}
		else if (BakingState == EBakingState.SAVING) {
			EditorUtility.DisplayProgressBar ("Dawn Completed", "Saving...", 0.5f);
		}
		else if (BakingState == EBakingState.CONVERT_LIGHTINGDATA)
		{
			EditorUtility.DisplayProgressBar("Dawn Completed", "Converting...", 0.75f);
		}
		else if (BakingState == EBakingState.COMPLETED)
		{
			EditorUtility.ClearProgressBar ();
		}

		string TextMessage = LightingSystem.TextMessage;
		if (!string.IsNullOrEmpty (TextMessage)) {
			//EditorUtility.DisplayDialog("Info", TextMessage, "OK");
		}

		if(BuildError!= EBakingError.NONE && LightingSystem.IsBuildCompleted)
		{
			EditorUtility.DisplayDialog("Baking Failure", BuildError + "\r\n\r\n" + BuildErrorString, "OK");
			EditorUtility.ClearProgressBar ();
		}
	}

	internal static void ShowCompleteUI(DawnLightingSystem LightingSystem,bool bSuccessed)
    {
		if(bSuccessed)
        {
			EditorUtility.DisplayDialog("Info", "Baking Successed!", "OK");
		}
	}
}
