using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System;
using System.Runtime.InteropServices;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
    public partial class DawnLightingSystem
    {
        bool OpenJob()
        {
            int ErrorCode = SwarmInterface.OpenJob(SceneGuid);
			DawnDebug.LogFormat("OpenJob:{0:X8}{1:X8}{2:X8}{3:X8} ErrorCode:{4}" ,SceneGuid.A, SceneGuid.B, SceneGuid.C, SceneGuid.D, ErrorCode);

            if(ErrorCode!=0)
            {
                Error = EBakingError.JOB_START_FAILURE;
            }

            return ErrorCode == 0;
        }

        void CloseJob()
        {
			if (SwarmInterface != null) 
			{
				SwarmInterface.CloseJob (); 
			} 
        }

        bool StartJob()
        {
			string[] DescriptionKeys = new string[2]{ "MapName","GameName" };
			string[] DescriptionValues = new string[2]{ "Demo","Unity4Dawn" };

            string DawnName = bDebugLightmapTexel && !bDebugLightingSystem ? "Dawn-Debug":"Dawn";

            string DawnExecutable64 = OptionsFolder + "/Win64/Binaries/"+ DawnName+".exe";
            string[] RequiredDependencyPaths64 = new string[]
            {
            OptionsFolder + "/Win64/DotNET/DawnSwarmInterface.dll",
            OptionsFolder + "/Win64/DotNET/DawnAgentInterface.dll",
            OptionsFolder + "/Win64/Binaries/optix.6.5.0.dll",
            OptionsFolder + "/Win64/Binaries/optix_prime.6.5.0.dll",
            OptionsFolder + "/Win64/Binaries/optixu.6.5.0.dll",
            OptionsFolder + "/Win64/Binaries/OpenImageDenoise.dll",
            OptionsFolder + "/Win64/Binaries/tbb.dll",
            OptionsFolder + "/Win64/Binaries/"+DawnName+".data"
            };

			string[] OptionalDependencyPaths64 = new string[]
			{
				OptionsFolder + "/Win64/Binaries/Dawn.pdb"
			};

			string CommandLineParameters = string.Format("{0:X8}{1:X8}{2:X8}{3:X8}", SceneGuid.A, SceneGuid.B, SceneGuid.C, SceneGuid.D);

			EJobTaskFlags JobFlags = EJobTaskFlags.FLAG_USE_DEFAULTS;
			if (bDebugLightingSystem)
			{
				JobFlags |= EJobTaskFlags.FLAG_MANUAL_START;
			} 
			else if(!bDebugLightmapTexel)
			{
				JobFlags |= EJobTaskFlags.FLAG_ALLOW_REMOTE;
			}

			JobFlags |= EJobTaskFlags.FLAG_MINIMIZED;

			FJobSpecification JobSpecification32 = new FJobSpecification();
			FJobSpecification JobSpecification64 = new FJobSpecification(DawnExecutable64, CommandLineParameters, JobFlags);
			JobSpecification64.AddDependencies(RequiredDependencyPaths64, (uint)RequiredDependencyPaths64.Length, OptionalDependencyPaths64, (uint)OptionalDependencyPaths64.Length);
			JobSpecification64.AddDescription (DescriptionKeys, DescriptionValues, (uint)DescriptionKeys.Length);

            int ErrorCode = SwarmInterface.BeginJobSpecification(JobSpecification32, JobSpecification64);

			DawnDebug.Log("BeginJobSpecification:" + ErrorCode);

            if(ErrorCode != 0)
            {
                Error = EBakingError.JOB_START_FAILURE;
                return false;
            }

            ErrorCode = AddTasks();

            if (ErrorCode != 0)
            {
                Error = EBakingError.TASK_ADD_FAILURE;
            }

            ErrorCode = SwarmInterface.EndJobSpecification();

			DawnDebug.Log("EndJobSpecification:" + ErrorCode);

            return ErrorCode == 0;
        }

		void ProcessJobState(ref FJobState JobStateMessage)
		{
			DawnDebug.Print("JobState:" + JobStateMessage.JobState);
			switch (JobStateMessage.JobState)
			{
			case EJobTaskState.STATE_KILLED:
				Error = EBakingError.JOB_KILLED;
				break;
			case EJobTaskState.STATE_COMPLETE_FAILURE:
				Error = EBakingError.JOB_FAILURE;
				break;
			}
		}
    }
}