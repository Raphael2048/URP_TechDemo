using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Runtime.InteropServices;
using GPUBaking;
using GPUBaking.Editor;
using System.Reflection;

namespace GPUBaking.Editor
{
	public partial class DawnLightAsset
	{
		static void ConvertLightProbetAsset(DawnBakeResultAsset bakeResultAsset, UnityEngine.Object lightProbeAsset)
        {
			EnsureSphericalHarmonicsL2Names();

			PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
			SerializedObject lightProbeAssetObject = new SerializedObject(lightProbeAsset);
			inspectorModeInfo.SetValue(lightProbeAssetObject, InspectorMode.Debug, null);

			var Tetrahedra = lightProbeAssetObject.FindProperty("m_Data.m_Tetrahedralization.m_Tetrahedra");
			var HullRays = lightProbeAssetObject.FindProperty("m_Data.m_Tetrahedralization.m_HullRays");
			var ProbeSets = lightProbeAssetObject.FindProperty("m_Data.m_ProbeSets");
			var Positions = lightProbeAssetObject.FindProperty("m_Data.m_Positions");
			var BakedCoefficients = lightProbeAssetObject.FindProperty("m_BakedCoefficients");
			var BakedLightOcclusion = lightProbeAssetObject.FindProperty("m_BakedLightOcclusion");

			DawnLightProbeUtils.DawnLightProbe ProbeData = new DawnLightProbeUtils.DawnLightProbe();
			ProbeData.m_Positions = bakeResultAsset.BakedLightProbePositions;
			DawnLightProbeUtils.CalculateTet(ProbeData);

			Positions.arraySize = bakeResultAsset.BakedLightProbePositions.Count;
			BakedCoefficients.arraySize = bakeResultAsset.BakedLightProbeCeffs.Count;
			BakedLightOcclusion.arraySize = bakeResultAsset.BakedLightProbePositions.Count;

			for (int ProbeIndex = 0; ProbeIndex < Positions.arraySize; ++ProbeIndex)
			{
				var Position = Positions.GetArrayElementAtIndex(ProbeIndex);
				var BakedCoefficientValue = BakedCoefficients.GetArrayElementAtIndex(ProbeIndex);
				var BakedLightOcclusionValue = BakedLightOcclusion.GetArrayElementAtIndex(ProbeIndex);

				Position.vector3Value = bakeResultAsset.BakedLightProbePositions[ProbeIndex];

				var SHInfo = bakeResultAsset.BakedLightProbeCeffs[ProbeIndex];
				for (int SHIndex = 0;SHIndex < SHInfo.SHValue.Length; ++SHIndex)
                {
					var SHValue = BakedCoefficientValue.FindPropertyRelative(SphericalHarmonicsL2Names[SHIndex]);
					SHValue.floatValue = SHInfo.SHValue[SHIndex];
				}

				var OcclusionArray = BakedLightOcclusionValue.FindPropertyRelative("m_Occlusion");
				var ProbeOcclusionLightIndexArray = BakedLightOcclusionValue.FindPropertyRelative("m_ProbeOcclusionLightIndex");
				var OcclusionMaskChannelArray = BakedLightOcclusionValue.FindPropertyRelative("m_OcclusionMaskChannel");

				var ProbeOcclusions = bakeResultAsset.BakedLightProbeOcclusions[ProbeIndex];
				OcclusionArray.arraySize = ProbeOcclusionLightIndexArray.arraySize = OcclusionMaskChannelArray.arraySize = 4;

				for (int Index = 0;Index < OcclusionArray.arraySize;++Index)
                {
					var OcclusionValue = OcclusionArray.GetArrayElementAtIndex(Index);
					OcclusionValue.floatValue = ProbeOcclusions.Occlusion[Index];
				}
				for (int Index = 0; Index < ProbeOcclusionLightIndexArray.arraySize; ++Index)
				{
					var ProbeOcclusionLightIndex = ProbeOcclusionLightIndexArray.GetArrayElementAtIndex(Index);
					ProbeOcclusionLightIndex.intValue = ProbeOcclusions.ProbeOcclusionLightIndex[Index];
				}
				for (int Index = 0; Index < OcclusionMaskChannelArray.arraySize; ++Index)
				{
					var OcclusionMaskChannel = OcclusionMaskChannelArray.GetArrayElementAtIndex(Index);
					OcclusionMaskChannel.intValue = ProbeOcclusions.OcclusionMaskChannel[Index];
				}
			}

			var TetrahedraData = ProbeData.m_TetrahedronInfo.TetrahedraDatas;
			var HullRaysData = ProbeData.m_TetrahedronInfo.Rays;

			Tetrahedra.arraySize = TetrahedraData.Count;
			for (int TetrahedraIndex = 0; TetrahedraIndex < TetrahedraData.Count;++TetrahedraIndex)
            {
				var TetrahedraDataObj = TetrahedraData[TetrahedraIndex];
				var TetrahedraValue = Tetrahedra.GetArrayElementAtIndex(TetrahedraIndex);
				for(int Index = 0;Index < 4;++Index)
                {
					var indices = TetrahedraValue.FindPropertyRelative("indices["+Index+"]");
					var neighbors = TetrahedraValue.FindPropertyRelative("neighbors[" + Index + "]");

					indices.intValue = TetrahedraDataObj.Indices[Index];
					neighbors.intValue = TetrahedraDataObj.Neighbors[Index];
				}
				SerializedProperty[] matrix = new SerializedProperty[12];
				{
					matrix[0] = TetrahedraValue.FindPropertyRelative("matrix.e00");
					matrix[1] = TetrahedraValue.FindPropertyRelative("matrix.e10");
					matrix[2] = TetrahedraValue.FindPropertyRelative("matrix.e20");
					matrix[3] = TetrahedraValue.FindPropertyRelative("matrix.e01");
					matrix[4] = TetrahedraValue.FindPropertyRelative("matrix.e11");
					matrix[5] = TetrahedraValue.FindPropertyRelative("matrix.e21");
					matrix[6] = TetrahedraValue.FindPropertyRelative("matrix.e02");
					matrix[7] = TetrahedraValue.FindPropertyRelative("matrix.e12");
					matrix[8] = TetrahedraValue.FindPropertyRelative("matrix.e22");
					matrix[9] = TetrahedraValue.FindPropertyRelative("matrix.e03");
					matrix[10] = TetrahedraValue.FindPropertyRelative("matrix.e13");
					matrix[11] = TetrahedraValue.FindPropertyRelative("matrix.e23");
				}
				for (int Index = 0; Index < 12; ++Index)
                {
					matrix[Index].floatValue = TetrahedraDataObj.Matrix[Index];
				}

				var TetrahedraValid = TetrahedraValue.FindPropertyRelative("isValid");
				if(TetrahedraValid!=null)
                {
					TetrahedraValid.boolValue = true;
				}				
			}
			HullRays.arraySize = HullRaysData.Count;
			for (int HullRayIndex = 0; HullRayIndex < HullRaysData.Count; ++HullRayIndex)
			{
				var HullRayValue = HullRays.GetArrayElementAtIndex(HullRayIndex);
				HullRayValue.vector3Value = HullRaysData[HullRayIndex];
			}

			ProbeSets.arraySize = 1;
			for (int ProbeSetIndex = 0; ProbeSetIndex < ProbeSets.arraySize; ++ProbeSetIndex)
			{
				var ProbeSetIndexValue = ProbeSets.GetArrayElementAtIndex(ProbeSetIndex);
				var Hash = ProbeSetIndexValue.FindPropertyRelative("m_Hash");
				var Offset = ProbeSetIndexValue.FindPropertyRelative("m_Offset");
				var Size = ProbeSetIndexValue.FindPropertyRelative("m_Size");

				Offset.intValue = 0;
				Size.intValue = bakeResultAsset.BakedLightProbePositions.Count;

				byte[] PositionData = new byte[sizeof(float) * 3 * bakeResultAsset.BakedLightProbePositions.Count];
				int PositionIndex = 0;
				foreach (var ProbePosition in bakeResultAsset.BakedLightProbePositions)
                {
					Array.Copy(BitConverter.GetBytes(ProbePosition.x), 0, PositionData, PositionIndex * 3 + 0, 4);
					Array.Copy(BitConverter.GetBytes(ProbePosition.y), 0, PositionData, PositionIndex * 3 + 1, 4);
					Array.Copy(BitConverter.GetBytes(ProbePosition.z), 0, PositionData, PositionIndex * 3 + 2, 4);
					++PositionIndex;
				}
				var HashValue= CityHash.CityHashCrc128(PositionData);
				byte[] HighBytes = BitConverter.GetBytes(HashValue.High);
				byte[] LowBytes = BitConverter.GetBytes(HashValue.Low);
				for (int Index = 0;Index < 16;++Index)
                {
					var HashByte = Hash.FindPropertyRelative("bytes[" + Index+"]");
					HashByte.intValue = Index < 8 ? HighBytes[Index] : LowBytes[Index - 8];
				}
			}

			DawnDebug.LogFormat("LightProbes({0}):Tetrahedra={1},HullRays={2},ProbeSets={3},Positions={4},BakedCoefficients={5}",
				lightProbeAsset.name, Tetrahedra.arraySize, HullRays.arraySize, ProbeSets.arraySize, Positions.arraySize, BakedCoefficients.arraySize);

			lightProbeAssetObject.ApplyModifiedProperties();
		}
	}
}