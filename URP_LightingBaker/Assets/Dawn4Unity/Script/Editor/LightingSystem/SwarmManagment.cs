using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

using System;
using System.Runtime.InteropServices;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
    public partial class DawnLightingSystem
    {
        internal FSwarmInterface SwarmInterface;

		List<string> TextMessageList = new List<string>();

        string OptionsFolder = Application.dataPath.Replace("/Assets", "/Dawn");

        bool StartSwarmAgent()
        {
            NSwarm.DebugLog.CustomLogFunc = SwarmLogOutput;

            SwarmInterface = FSwarmInterface.GetInstance();            

            DawnDebug.Print("OptionsFolder:" + OptionsFolder);

			int ConnectionHandle = SwarmInterface.OpenConnection(SwarmCallback, IntPtr.Zero, AgentInterface.ELogFlags.LOG_NONE, OptionsFolder);

			DawnDebug.Log(string.Format("OpenConnection:{0:X8}" , ConnectionHandle));

			if (ConnectionHandle < 0)
            {
                Error = EBakingError.SWARM_STARTUP_FAILURE;
            }

			return ConnectionHandle >= 0;
        }

        void ShutdownSwarmAgent()
        {
            if (SwarmInterface != null)
            {
                if (Error != EBakingError.SWARM_CONNECTION_LOST)
                {
                    SwarmInterface.CloseConnection();
                    SwarmInterface = null;                    
                }
                NSwarm.DebugLog.CustomLogFunc = null;
            }            
        }

        static void SwarmLogOutput(string msg)
        {
            DawnDebug.Log("Swarm:" + msg);
        }

        void SwarmCallback(IntPtr NativeMessagePtr, IntPtr CallbackData)
        {
            FMessage Message = (FMessage)Marshal.PtrToStructure(NativeMessagePtr, typeof(FMessage));

            switch (Message.Type)
            {
                case EMessageType.JOB_STATE:
                    {
						FJobState JobStateMessage = (FJobState)Marshal.PtrToStructure(NativeMessagePtr, typeof(FJobState));
						ProcessJobState (ref JobStateMessage);
                    }
                    break;
                case EMessageType.TASK_STATE:
                    {
                        FTaskState TaskStateMessage = (FTaskState)Marshal.PtrToStructure(NativeMessagePtr, typeof(FTaskState));
						ProcessTaskState (ref TaskStateMessage);
                    }
                    break;
                case EMessageType.INFO:
                    {
                        FInfoMessage InfoMessage = (FInfoMessage)Marshal.PtrToStructure(NativeMessagePtr, typeof(FInfoMessage));
                        string TextMessage = FStringMarshaler.MarshalNativeToManaged(InfoMessage.TextMessage);
						DawnDebug.Log("Info:" + TextMessage);
						TextMessageList.Add (TextMessage);
                    }
                    break;
                case EMessageType.QUIT:
                    DawnDebug.LogWarning("Dawn Quited!!!");
                    break;
				default:
					DawnDebug.LogWarning("SwarmCallback:" + Message.Type);
					break;
            }
        }

		public string TextMessage
		{
			get { 
				if (TextMessageList.Count > 0) {
					string Info = TextMessageList [0];
					TextMessageList.RemoveAt (0);
					return Info;
				}
				return null;
			}
		}
    }
}