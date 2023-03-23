using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;
using NSwarm;
using AgentInterface;


namespace GPUBaking.Editor
{
    public class GUIDUtility
    {
        public static FGuid GetObjectGuid(UnityEngine.Object unityObject)
        {
            var ObjectGuid = GUID.CreateGUID(unityObject);
            UInt128 HashCode = CityHash.CityHashCrc128(ObjectGuid.High + "-"+ ObjectGuid.Low);
            return ConvertUint128ToFGuid(HashCode);
        }

        public static void GetMaterialHash(Material material,bool bUseMetaPass, out byte[] Hash)
        {
            Hash = new byte[20];

            string guid = string.Empty;
            long localId = 0;
#if UNITY_2018_1_OR_NEWER
            if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material, out guid, out localId))
#endif
            {
                string path = AssetDatabase.GetAssetPath(material);
                guid = AssetDatabase.AssetPathToGUID(path);
                var Identifier = GUID.CreateGUID(material);
                localId = (long)Identifier.High;
            }

            string properties = "";

            if(material.HasProperty("_Color"))
            {
                properties += material.GetColor("_Color").ToString();
            }
            if (material.HasProperty("_EmissionColor"))
            {
                properties += material.GetColor("_EmissionColor").ToString();
            }

            UInt128 HashCode = CityHash.CityHashCrc128(guid + "-" + localId + "-"+ material.shader.name + "-"+properties);

            var HighBytes = BitConverter.GetBytes(HashCode.High);
            var LowBytes = BitConverter.GetBytes(HashCode.Low);
            int Offset = 0;
            for (int Index = 0; Index < HighBytes.Length;++Index)
            {
                Hash[Offset+ Index] = HighBytes[Index];
            }
            Offset += HighBytes.Length;
            for (int Index = 0; Index < LowBytes.Length; ++Index)
            {
                Hash[Offset + Index] = LowBytes[Index];
            }
            Offset += LowBytes.Length;

            Hash[Offset] = (byte)(bUseMetaPass ? 1 : 0);
        }
        public static FGuid GetMeshGuid(Mesh mesh,int LODIndex)
        {
            byte[] meshBytes = MeshSerializer.WriteMesh(mesh,LODIndex, true);
            UInt128 meshHashCode =CityHash.CityHashCrc128(meshBytes);
            return ConvertUint128ToFGuid(meshHashCode);
        }

        static FGuid ConvertUint128ToFGuid(UInt128 uint128)
        {
            uint High128_96 = (uint)(uint128.High >> 32);
            uint High96_64 = (uint)(uint128.High);
            uint Low64_32 = (uint)(uint128.Low >> 32);
            uint Low32_1 = (uint)(uint128.Low);
            //DawnDebug.Print("FGuid->>{0} : {1} : {2} : {3}",  High128_96, High96_64, Low64_32, Low32_1);
            return new FGuid(High128_96, High96_64, Low64_32, Low32_1);
        }

        public static string ToString(FGuid Guid)
		{
			return string.Format("{0:X8}-{1:X8}-{1:X8}-{3:X8}", Guid.A, Guid.B, Guid.C, Guid.D);
		}
    }
}
