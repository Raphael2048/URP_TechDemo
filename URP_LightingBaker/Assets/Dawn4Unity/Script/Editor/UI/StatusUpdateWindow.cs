using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Scripting;
using System;
using System.IO;
using GPUBaking;

namespace GPUBaking.Editor
{
    class StatusUpdateWindow : EditorWindow
    {
        EBakingState BakingState = EBakingState.IDLE;
        EBakingError BuildError = EBakingError.NONE;
        bool bIsBuildSuccessed = false;
        public static void openStatusUpdateWindow()
        {
            int winWidth = 600;
            int winHight = 100;
            StatusUpdateWindow window = EditorWindow.GetWindow<StatusUpdateWindow>("Baking");
            window.position = new Rect(Screen.currentResolution.width - winWidth, Screen.currentResolution.height - winHight-100, winWidth, winHight);
            window.Show();
        }
        void OnGUI()
        {
            if (BuildError != EBakingError.NONE)
            {
                GUILayout.Label(BuildError.ToString(), EditorStyles.boldLabel);
            }
            else if (bIsBuildSuccessed)
            {
                GUILayout.Label("Build Successed!", EditorStyles.boldLabel);
            }

			var LightingSystem =  Dawn4Unity.GetLightingSystem();

            if (BakingState == EBakingState.IDLE)
            {
                if (GUILayout.Button("Start Bake"))
                {
                    Dawn4Unity.BuildLighting();
                   
                }
            }else if (LightingSystem != null) {

                GUILayout.Label("State:" + BakingState.ToString(), EditorStyles.boldLabel);

                if (BakingState == EBakingState.BUILDING)
                {
                    EditorGUI.ProgressBar(new Rect(3, position.height -25, position.width - 6, 20)
                        , (float)LightingSystem.NumOfTaskCompleted / (float)LightingSystem.NumOfTask
                        , string.Format("Task Progress:{0}/{1}", LightingSystem.NumOfTaskCompleted,LightingSystem.NumOfTask));

                }
                else if (BakingState == EBakingState.IMPORTING)
                {
                    EditorGUI.ProgressBar(new Rect(3, position.height - 25, position.width - 6, 20)
                         , (float)LightingSystem.NumOfTaskImported / (float)LightingSystem.NumOfTask
                         , string.Format("Import Progress:{0}/{1}", LightingSystem.NumOfTaskImported, LightingSystem.NumOfTask));
                }

                if (GUILayout.Button("Cancel Baking"))
                {
                   LightingSystem.Cancel();
                }

            }
            
        }

        void OnInspectorUpdate()
        {
			if (BakingState != EBakingState.NONE && Dawn4Unity.GetLightingSystem() != null)
            {
				var LightingSystem =  Dawn4Unity.GetLightingSystem();
                BakingState = LightingSystem.BuildingState;
                BuildError = LightingSystem.BuildError;
                bIsBuildSuccessed = LightingSystem.IsBuildSuccessed;
                string TextMessage = LightingSystem.TextMessage;
        
            }
            Repaint();
        }

        bool isLostFocus = false;
        private void OnLostFocus()
        {
            isLostFocus = true;
        }

        private void OnFocus()
        {
            isLostFocus = false;
        }



       private void Update()
        {

            if (isLostFocus/* execute only when focus is lost */)
            {
                Repaint();
                OnInspectorUpdate();
            }

        }
    }
}
