using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using NSwarm;
using AgentInterface;

namespace GPUBaking.Editor
{
	public class ImportExportUtil
	{
		public const int LM_SCENE_VERSION = 1;

		public const string LM_SCENE_EXTENSION = "scenegz";
		public const string LM_JOB_EXTENSION = "jobgz";

		public const string LM_MESH_EXTENSION = "meshgz";
		public const string LM_MATERIAL_EXTENSION = "matgz";

		public const string LM_LM_EXTENSION = "lmgz";

		public const string LM_SHADOW_EXTENSION = "sdfgz";
		public const string LM_LIGHTPROBE_EXTENSION = "lpgz";

		public const string LM_DEBUG_EXTENSION = "dbgz";

		public const EChannelFlags LM_CHANNEL_WRITE_FLAGS = EChannelFlags.JOB_CHANNEL_WRITE | EChannelFlags.MISC_ENABLE_COMPRESSION;
		public const EChannelFlags LM_CHANNEL_READ_FLAGS = EChannelFlags.JOB_CHANNEL_READ | EChannelFlags.MISC_ENABLE_COMPRESSION;

		public const EChannelFlags LM_CACHED_CHANNEL_WRITE_FLAGS = EChannelFlags.CHANNEL_WRITE | EChannelFlags.MISC_ENABLE_COMPRESSION;
		public const EChannelFlags LM_CACHED_CHANNEL_READ_FLAGS = EChannelFlags.CHANNEL_READ | EChannelFlags.MISC_ENABLE_COMPRESSION;

		public static string CreateChannelName(FGuid Guid, int Version, string Extension)
		{
			return string.Format("v{0}.{1:X8}{2:X8}{3:X8}{4:X8}.{5}", Version, Guid.A, Guid.B, Guid.C, Guid.D, Extension);
		}

		public static string CreateChannelName(FGuid Guid,int LODIndex, int Version, string Extension)
		{
			return string.Format("v{0}.{1:X8}{2:X8}{3:X8}{4:X8}{5}.{6}", Version, Guid.A, Guid.B, Guid.C, Guid.D, LODIndex,Extension);
		}

		public static string CreateChannelName(FSHAHashInfo Hash,int Version, string Extension)
		{
			return string.Format("v{0}.{1}.{2}", Version, BytesToHex(Hash.Hash), Extension);
		}

		public static bool ImportScene(ref FGuid SceneGuid, byte[] SceneData,out FSceneInfo OutScene)
		{
			FMemeryImportExportContext Context = new FMemeryImportExportContext (SceneData);

			int Version = (int)EGPUBakingVersion.VERSION_INVALID;
			Context.ReadVersion (ref Version);

			OutScene = new FSceneInfo();

			OutScene.Serialize (Context);

			return true;
		}

		public static bool ImportJob(ref FGuid SceneGuid, byte[] JobData,out FBakingJobInputs OutJob)
		{
			FMemeryImportExportContext Context = new FMemeryImportExportContext (JobData);

			int Version = (int)EGPUBakingVersion.VERSION_INVALID;
			Context.ReadVersion (ref Version);

			OutJob = new FBakingJobInputs();

			OutJob.Serialize (Context);

			return true;
		}

		public static byte[] ExportScene(ref FSceneInfo SceneInfo)
		{
			FMemeryImportExportContext Context = new FMemeryImportExportContext ();

			Context.WriteVersion ((int)EGPUBakingVersion.VERSION_DEFAULT);
	
			SceneInfo.Serialize (Context);

			return Context.GetData ();
		}

		public static byte[] ExportJobs(ref FBakingJobInputs JobInfo)
		{
			FMemeryImportExportContext Context = new FMemeryImportExportContext ();

			Context.WriteVersion ((int)EGPUBakingVersion.VERSION_DEFAULT);

			JobInfo.Serialize (Context);

			return Context.GetData ();
		}

		public static bool ExportScene(FSwarmInterface SwarmInterface,ref FGuid SceneGuid, byte[] SceneData)
        {
			string ChannelName = CreateChannelName (SceneGuid, LM_SCENE_VERSION, LM_SCENE_EXTENSION);

			int ChannelID = SwarmInterface.OpenChannel(ChannelName, LM_CHANNEL_WRITE_FLAGS);

			DawnDebug.Print("OpenSceneChannel:" + ChannelID);	

			int WriteLen = SwarmInterface.WriteChannel(ChannelID, SceneData);

			DawnDebug.Print("WriteSceneChannel:" + WriteLen + "/" + SceneData.Length);

			SwarmInterface.CloseChannel(ChannelID);

			return ChannelID >= 0;
		}

		public static  bool ExportJobs(FSwarmInterface SwarmInterface, ref FGuid SceneGuid, byte[] JobData)
		{
			string ChannelName = CreateChannelName (SceneGuid, LM_SCENE_VERSION, LM_JOB_EXTENSION);

			int ChannelID = SwarmInterface.OpenChannel(ChannelName, LM_CHANNEL_WRITE_FLAGS);

			DawnDebug.Print("OpenJobChannel:" + ChannelID);

			int WriteLen = SwarmInterface.WriteChannel(ChannelID, JobData);

			DawnDebug.Print("WriteJobChannel:" + WriteLen + "/" + JobData.Length);

			SwarmInterface.CloseChannel(ChannelID);

			return ChannelID >= 0;
		}

		public static bool SerializeScene(FSwarmInterface SwarmInterface,ref FGuid SceneGuid, ref FSceneInfo SceneInfo,bool bSaved)
		{
			string ChannelName = CreateChannelName (SceneGuid, LM_SCENE_VERSION, LM_SCENE_EXTENSION);

			return SerializeObjectWithVersion<FSceneInfo> (SwarmInterface,ChannelName,ref SceneInfo,bSaved,false);
		}

		public static bool SerializeJobs(FSwarmInterface SwarmInterface,ref FGuid SceneGuid, ref FBakingJobInputs BakingJobInputs,bool bSaved)
		{
			string ChannelName = CreateChannelName (SceneGuid, LM_SCENE_VERSION, LM_JOB_EXTENSION);

			return SerializeObjectWithVersion<FBakingJobInputs> (SwarmInterface,ChannelName,ref BakingJobInputs,bSaved,false);
		}

		public static bool SerializeScene(string Path,ref FGuid SceneGuid, ref FSceneInfo SceneInfo,bool bSaved)
		{
			return SerializeObjectWithVersion<FSceneInfo> (Path,ref SceneInfo,bSaved,false);
		}

		public static bool SerializeJobs(string Path,ref FGuid SceneGuid, ref FBakingJobInputs BakingJobInputs,bool bSaved)
		{
			return SerializeObjectWithVersion<FBakingJobInputs> (Path,ref BakingJobInputs,bSaved,false);
		}

		public static bool SerializeObjectWithVersion<T>(string Path,ref T Obj,bool bSaved,bool bCached) where T : ISerializableObject
		{
			FFileImportExportContext Context = new FFileImportExportContext (Path,bSaved);

			if (bSaved) {
				Context.WriteVersion ((int)EGPUBakingVersion.VERSION_DEFAULT);
			} else {
				int Version = (int)EGPUBakingVersion.VERSION_INVALID;
				Context.ReadVersion (ref Version);
			}

			Obj.Serialize (Context);

			Context.Close();

			return true;
        }

		public static bool SerializeObjectWithVersion<T>(FSwarmInterface SwarmInterface, string ChannelName,ref T Obj,bool bSaved,bool bCached) where T : ISerializableObject
		{
			EChannelFlags ChannelFlags = bSaved ? LM_CHANNEL_WRITE_FLAGS : LM_CHANNEL_READ_FLAGS;

			EChannelFlags CachedChannelFlags = bSaved ? LM_CACHED_CHANNEL_WRITE_FLAGS : LM_CACHED_CHANNEL_READ_FLAGS;
			
			int ChannelID = SwarmInterface.OpenChannel(ChannelName, bCached ? CachedChannelFlags : ChannelFlags);

			DawnDebug.Print("OpenSceneChannel:" + ChannelID);	

			if (ChannelID < 0) {
				return false;
			}

			FSwarmImportExportContext Context = new FSwarmImportExportContext (SwarmInterface,ChannelID,bSaved);

			if (bSaved) {
				Context.WriteVersion ((int)EGPUBakingVersion.VERSION_DEFAULT);
			} else {
				int Version = (int)EGPUBakingVersion.VERSION_INVALID;
				Context.ReadVersion (ref Version);
			}

			Obj.Serialize (Context);

			SwarmInterface.CloseChannel(ChannelID);

			return true;
        }

		public static bool SerializeObject<T>(FSwarmInterface SwarmInterface, string ChannelName,ref T Obj,bool bSaved,bool bCached) where T : ISerializableObject
		{
			EChannelFlags ChannelFlags = bSaved ? LM_CHANNEL_WRITE_FLAGS : LM_CHANNEL_READ_FLAGS;

			EChannelFlags CachedChannelFlags = bSaved ? LM_CACHED_CHANNEL_WRITE_FLAGS : LM_CACHED_CHANNEL_READ_FLAGS;

			int ChannelID = SwarmInterface.OpenChannel(ChannelName, bCached ? CachedChannelFlags : ChannelFlags);

			DawnDebug.Print("OpenSceneChannel {1} For {0}",ChannelName, ChannelID);	

			if (ChannelID < 0) {
				return false;
			}

			FSwarmImportExportContext Context = new FSwarmImportExportContext (SwarmInterface,ChannelID,bSaved);

			Obj.Serialize (Context);

			SwarmInterface.CloseChannel(ChannelID);

			return true;
		}

		public static bool SerializeArray<T>(FSwarmInterface SwarmInterface, string ChannelName, TSerializedArray<T> Obj, bool bSaved, bool bCached) where T : struct
		{
			EChannelFlags ChannelFlags = bSaved ? LM_CHANNEL_WRITE_FLAGS : LM_CHANNEL_READ_FLAGS;

			EChannelFlags CachedChannelFlags = bSaved ? LM_CACHED_CHANNEL_WRITE_FLAGS : LM_CACHED_CHANNEL_READ_FLAGS;

			int ChannelID = SwarmInterface.OpenChannel(ChannelName, bCached ? CachedChannelFlags : ChannelFlags);

			DawnDebug.Print("OpenSceneChannel {1} For {0}", ChannelName, ChannelID);

			if (ChannelID < 0)
			{
				return false;
			}

			FSwarmImportExportContext Context = new FSwarmImportExportContext(SwarmInterface, ChannelID, bSaved);

			Obj.Serialize(Context);

			SwarmInterface.CloseChannel(ChannelID);

			return true;
		}

		public static bool SerializeMesh(FSwarmInterface SwarmInterface,ref FGuid Guid,ref FMeshInfo MeshInfo, int LODIndex,bool bSaved)
		{
			return SerializeObjectWithVersion (SwarmInterface,CreateChannelName(Guid,LODIndex,LM_SCENE_VERSION,LM_MESH_EXTENSION),ref MeshInfo,bSaved,true);
		}

		public static bool SerializeMaterial(FSwarmInterface SwarmInterface,ref FSHAHashInfo Hash,ref FMaterialInfo MatInfo,bool bSaved)
		{
			return SerializeObjectWithVersion (SwarmInterface,CreateChannelName(Hash,LM_SCENE_VERSION,LM_MATERIAL_EXTENSION),ref MatInfo,bSaved,true);
		}

		public static bool IsMaterialCached(FSwarmInterface SwarmInterface,ref FSHAHashInfo Hash)
		{
			var ChannelName = CreateChannelName (Hash, LM_SCENE_VERSION, LM_MATERIAL_EXTENSION);
			return IsObjectCached (SwarmInterface,ChannelName);
		}

		public static bool IsMeshCached(FSwarmInterface SwarmInterface,ref FGuid Guid,int LODIndex)
		{
			return IsObjectCached (SwarmInterface,ref Guid,LODIndex,LM_SCENE_VERSION,LM_MESH_EXTENSION);
		}

		public static bool IsObjectCached(FSwarmInterface SwarmInterface,string ChannelName)
		{
			int ErrorCode = SwarmInterface.TestChannel(ChannelName);
			return ErrorCode >= 0;
		}

		public static bool IsObjectCached(FSwarmInterface SwarmInterface,ref FGuid Guid,int FileVersion,string Extension)
		{
			return IsObjectCached (SwarmInterface,CreateChannelName(Guid,FileVersion,Extension));
		}

		public static bool IsObjectCached(FSwarmInterface SwarmInterface,ref FGuid Guid,int LODIndex,int FileVersion,string Extension)
		{
			return IsObjectCached (SwarmInterface,CreateChannelName(Guid,LODIndex,FileVersion,Extension));
		}

		public static string BytesToHex(byte[] Bytes)
		{
			System.Text.StringBuilder Result = new System.Text.StringBuilder(Bytes.Length * 2);
			string HexAlphabet = "0123456789ABCDEF";

			foreach (byte B in Bytes)
			{
				Result.Append(HexAlphabet[(int)(B >> 4)]);
				Result.Append(HexAlphabet[(int)(B & 0xF)]);
			}

			return Result.ToString();
		}

		public class FSwarmImportExportContext : FImportExportContext
		{
			FSwarmInterface SwarmInterface;

			int ChannelID;
	
			public FSwarmImportExportContext(FSwarmInterface SwarmInterface,int ChannelID,bool bSaved) : base(bSaved)
			{
				this.SwarmInterface = SwarmInterface;
				this.ChannelID = ChannelID;
			}
			protected override int Read (byte[] Data, int Offset, int Size)
			{
				return SwarmInterface.ReadChannel (ChannelID, Data);
			}

			protected override int Write (byte[] Data, int Offset, int Size)
			{
				return SwarmInterface.WriteChannel (ChannelID, Data);
			}
		}

		public class FMemeryImportExportContext : FImportExportContext
		{
			MemoryStream Stream;

			public FMemeryImportExportContext() : base(true)
			{
				this.Stream = new System.IO.MemoryStream();
			}

			public FMemeryImportExportContext(byte[] Data) : base(false)
			{
				this.Stream = new System.IO.MemoryStream(Data);
			}
			protected override int Read (byte[] Data, int Offset, int Size)
			{
				return Stream.Read (Data, 0,Size);
			}

			protected override int Write (byte[] Data, int Offset, int Size)
			{
				Stream.Write (Data, 0,Size);
				return Size;
			}

			public byte [] GetData()
			{
				return this.Stream.ToArray ();
			}
		}

		public class FFileImportExportContext : FImportExportContext
		{
			FileStream Stream;

			public FFileImportExportContext(string Path,bool bSaved) : base(bSaved)
			{
				this.Stream = new FileStream(Path,bSaved ? FileMode.OpenOrCreate : FileMode.Open, bSaved ? FileAccess.Write : FileAccess.Read);
			}
			protected override int Read (byte[] Data, int Offset, int Size)
			{
				return Stream.Read (Data, 0,Size);
			}

			protected override int Write (byte[] Data, int Offset, int Size)
			{
				Stream.Write (Data, 0,Size);
				return Size;
			}

			public void Close()
			{
				Stream.Close();
			}
		}
	}
}