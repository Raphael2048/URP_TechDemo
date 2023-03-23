using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace GPUBaking.Editor
{
	public sealed class DawnLightingStatistics
	{
		static Dictionary<EBakingState, float2> BakingStateStatistics = new Dictionary<EBakingState, float2>();

        public static void Reset()
        {
			BakingStateStatistics.Clear();
        }

		public static void BeginEvent(EBakingState eventName)
        {
			float2 StatisticsEvent =  new float2 (Time.realtimeSinceStartup, Time.realtimeSinceStartup);
			BakingStateStatistics [eventName] = StatisticsEvent;
        }

		public static void EndEvent(EBakingState eventName)
        {
			float2 StatisticsEvent;
			if (BakingStateStatistics.TryGetValue (eventName,out StatisticsEvent)) {
				StatisticsEvent.y = Time.realtimeSinceStartup;
				BakingStateStatistics [eventName] = StatisticsEvent;
			}
        }

		public static float EventElapsedTime(EBakingState eventName)
		{
			float2 StatisticsEvent;
			if (BakingStateStatistics.TryGetValue (eventName, out StatisticsEvent)) {
				return StatisticsEvent.y - StatisticsEvent.x;  
			}
			return 0;
		}

		public static float EventElapsedTime(EBakingState eventNameA,EBakingState eventNameB)
		{
			float2 StatisticsEventA;
			float2 StatisticsEventB;
			if (BakingStateStatistics.TryGetValue (eventNameA, out StatisticsEventA) && BakingStateStatistics.TryGetValue (eventNameB, out StatisticsEventB)) {
				return StatisticsEventB.y - StatisticsEventA.x;
			}
			return 0;
		}

		public static float EventElapsedTime(EBakingState eventNameA, EBakingState eventNameB, EBakingState eventNameC)
		{
			float2 StatisticsEventA;
			if (!BakingStateStatistics.TryGetValue(eventNameA, out StatisticsEventA))
			{
				return 0.0f;
			}
			float2 StatisticsEventB;
			if (BakingStateStatistics.TryGetValue(eventNameB, out StatisticsEventB))
			{
				return StatisticsEventB.y - StatisticsEventA.x;
			}
			if (BakingStateStatistics.TryGetValue(eventNameC, out StatisticsEventB))
			{
				return StatisticsEventB.y - StatisticsEventA.x;
			}
			return 0;
		}

		public static void Finish()
        {
            DawnDebug.LogFormat("Lighting Total Cost:{0} Seconds"
                      + "\r\nGathering Cost:{1} Seconds"
                      + "\r\nSwarming Cost:{2} Seconds"
                      + "\r\nExporting Cost:{3} Seconds"
                      + "\r\nBuilding Cost:{4} Seconds"
                      + "\r\nImporting Cost:{5} Seconds"
                      + "\r\nEncoding Cost:{6} Seconds"
                      + "\r\nApplying Cost:{7} Seconds"
					  + "\r\nSaving Cost:{8} Seconds"
					  + "\r\nConverting Cost:{9} Seconds",
					EventElapsedTime(EBakingState.GATHERING, EBakingState.CONVERT_LIGHTINGDATA, EBakingState.SAVING),
					EventElapsedTime(EBakingState.GATHERING),
					EventElapsedTime(EBakingState.SWARM_STARTED),
					EventElapsedTime(EBakingState.EXPORTING),
					EventElapsedTime(EBakingState.BUILDING),
					EventElapsedTime(EBakingState.IMPORTING),
					EventElapsedTime(EBakingState.ENCODING),
					EventElapsedTime(EBakingState.APPLYING),
					EventElapsedTime(EBakingState.SAVING),
					EventElapsedTime(EBakingState.CONVERT_LIGHTINGDATA));

			DawnProfiler.Print ();
        }
    }
}

