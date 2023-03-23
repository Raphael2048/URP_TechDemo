using System;
using UnityEditor;


namespace GPUBaking.Editor
{
    public enum EBakingState
    {
        NONE = 0,
        IDLE,
		PENDING_STARTUP,
		GATHERING,
		PENDING_SWARM,
		STARTING_UP,
		SWARM_STARTED,
        EXPORTING,       
        PENDING_JOB,
        BUILDING,
        IMPORTING,
        ENCODING,
        APPLYING,
		SAVING,
		CONVERT_LIGHTINGDATA,
        COMPLETED
    }

    public enum EBakingError
    {
        NONE = 0,
        SUCESSED = 0,
        JOB_CANCELED = 1,
        SWARM_STARTUP_FAILURE = -1,
        SWARM_CONNECTION_LOST = -2,
        JOB_START_FAILURE = -3,
        JOB_KILLED = -5,
        JOB_FAILURE = -6,      
		EXPORT_SCENE_FAILURE = -7,
		EXPORT_JOB_FAILURE = -8,
        TASK_ADD_FAILURE = -10,
        TASK_FAILURE = -11,
        TASK_REJECTED = -12,
		RESULT_IMPORT_FAILURE = -20,
		RESULT_ENCODING_FAILURE = -21,
		RESULT_APPLY_FAILURE = -22,
		RESULT_SAVE_FAILURE = -23,
		RESULT_CONVERT_FAILURE = -24,
		EXCEPTION = -100,
		UNKOWN = -1000
    }

	public partial class DawnLightingSystem
	{
        EBakingState State = EBakingState.IDLE;
        EBakingError Error = EBakingError.NONE;
		bool bBuildSuccessed = false;
		bool bBuildCompleted = false;

        internal NSwarm.FGuid SceneGuid;
		internal bool bDebugLightingSystem;
		internal bool bDebugLightmapTexel;

		internal DawnBakingContext BakingContext;

        DawnExporter Exporter;
        DawnImporter Importer;

		internal static NSwarm.FGuid CreateGuid()
		{
			var NewGuid = System.Guid.NewGuid ();
			var GuidData = NewGuid.ToByteArray ();

			NSwarm.FGuid Guid = new NSwarm.FGuid ();
			Guid.A = BitConverter.ToUInt32 (GuidData, 0);
			Guid.B = BitConverter.ToUInt32 (GuidData, 4);
			Guid.C = BitConverter.ToUInt32 (GuidData, 8);
			Guid.D = BitConverter.ToUInt32 (GuidData, 12);
			return Guid;
		}

		public DawnLightingSystem(NSwarm.FGuid SceneGuid)
		{
			this.SceneGuid = SceneGuid;
			this.BakingContext = new DawnBakingContext ();
		}

		public void Start(DawnSettings BakingSettings, bool bDebugLightingSystem,bool bDebugLightmapTexel,bool bUsePrecomputedProbes,DawnBakingMode BakingMode = DawnBakingMode.Default)
        {
			this.bDebugLightingSystem = bDebugLightingSystem;
			this.bDebugLightmapTexel = bDebugLightmapTexel;

			State = EBakingState.IDLE;
			Error = EBakingError.NONE;
			bBuildSuccessed = false;
			bBuildCompleted = false;


			DawnLightingStatistics.Reset();

			PendingTasks.Clear ();
			CompletedTasks.Clear ();
			ImportedTasks.Clear ();

			BakingContext.Reset ();
			BakingContext.Settings = BakingSettings;
            BakingContext.IsEnableExportCache = true;
			BakingContext.BakingMode = BakingMode;
			BakingContext.bUsePrecomputedProbes = bUsePrecomputedProbes;

			Exporter = new DawnExporter(this);
			Importer = new DawnImporter(this,UnityEngine.SystemInfo.processorCount + 1);

			State = EBakingState.PENDING_STARTUP;
        }
		public void Cancel(EBakingError Reason = EBakingError.JOB_CANCELED)
        {
            State = EBakingState.COMPLETED;
			if (Error == EBakingError.NONE) {
				Error = Reason;
			}
        }
		public bool Update()
        {
            if(State == EBakingState.NONE)
            {
				return false;
            }
            switch(State)
            {
				case EBakingState.PENDING_STARTUP:
					{
						DawnLightingStatistics.BeginEvent(EBakingState.GATHERING);
						State = EBakingState.GATHERING;
					}
					break;
				case EBakingState.GATHERING:
					if(Gather()){
						DawnLightingStatistics.EndEvent(EBakingState.GATHERING);
                        State = EBakingState.STARTING_UP;						
                    }
					break;
				case EBakingState.STARTING_UP:
					DawnLightingStatistics.BeginEvent(EBakingState.SWARM_STARTED);
					if (StartingUp ()) {
                        State = EBakingState.SWARM_STARTED;
					}
					break;
				case EBakingState.SWARM_STARTED:
					{
						DawnLightingStatistics.EndEvent(EBakingState.SWARM_STARTED);
                        State = EBakingState.EXPORTING;
						DawnLightingStatistics.BeginEvent(EBakingState.EXPORTING);
                        Exporter.StartExport(BakingContext,ref SceneGuid);
					}
					break;
                case EBakingState.EXPORTING:
					if (Export ()) {
						State = EBakingState.PENDING_JOB;
						DawnLightingStatistics.EndEvent(EBakingState.EXPORTING);						
                    }
                    break;
                case EBakingState.PENDING_JOB:
                    {
                        if(StartJob())
                        {
                            State = EBakingState.BUILDING;
							DawnLightingStatistics.BeginEvent(EBakingState.BUILDING);
							Importer.StartImport ();
                        }
                        else
                        {
                            State = EBakingState.COMPLETED;
                        }                   
                    }
                    break;
                case EBakingState.BUILDING:
                    {
						if (Error == EBakingError.NONE) {
							bool bTaskCompleted = NumOfTaskCompleted >= PendingTasks.Count;
							if (bTaskCompleted) {
								DawnLightingStatistics.EndEvent(EBakingState.BUILDING);
                                State = EBakingState.IMPORTING;
								DawnLightingStatistics.BeginEvent(EBakingState.IMPORTING);
                            }
							else if(Importer.HasError)
                            {
								Error = EBakingError.RESULT_IMPORT_FAILURE;
							}
						}
                    }
                    break;
                case EBakingState.IMPORTING:
					if (ImportResults())
                    {
						DawnLightingStatistics.EndEvent(EBakingState.IMPORTING);
                        State = EBakingState.ENCODING;						
                    }

                    break;
                case EBakingState.ENCODING:
                    if (EncodeTextures())
                    {
                        State = EBakingState.APPLYING;
                    }
                    break;
                case EBakingState.APPLYING:
                    if (ApplyResults())
                    { 
                        State = EBakingState.SAVING;
                    }
                    break;
				case EBakingState.SAVING:
					if (SaveResults())
					{ 
						State = EBakingState.CONVERT_LIGHTINGDATA;
					}
					break;
				case EBakingState.CONVERT_LIGHTINGDATA:
					if (ConvertResults())
					{
						State = EBakingState.COMPLETED;
					}
					break;
				case EBakingState.COMPLETED:
					{
						if(bDebugLightmapTexel && Error == EBakingError.SUCESSED)
                        {
							ShowDebugInfo();
                        }
						Finish();
						State = EBakingState.IDLE;
						bBuildSuccessed = Error == EBakingError.SUCESSED;
						bBuildCompleted = true;
					}                    
                    break;
            }

			if(Error != EBakingError.NONE && State != EBakingState.COMPLETED && State != EBakingState.IDLE)
            {
                State = EBakingState.COMPLETED;
            }

            return State != EBakingState.COMPLETED;
        }

        void Finish()
        {
			DawnDebug.LogFormat ("Lighting Finished");
            DawnLightingStatistics.Finish();
            if (Importer != null)
            {
                Importer.StopImport();
            }
			ClearTasks ();

			try
			{
 				CloseJob();
            	ShutdownSwarmAgent();
			}
			catch(Exception e)
			{
				DawnDebug.LogException(e);
			}
           
            Exporter = null;
            Importer = null;

			System.GC.Collect();
        }

		bool Gather()
		{
			// Initialize light probes first because may need to switch to new temp scene to generate new lighting data asset,
			// otherwise old scene gather object will be invalid when export
			//DawnLightingDataAssetManager.ValidateLightingDataAssetForLightProbes();
			DawnBakePathSetting.GetInstance().BakeResultFolderPath(true);
			
			Exporter.GatherScene(BakingContext);
			Exporter.GatherJobs(BakingContext);
			
			BakingContext.Print ();

			return true;
		}

		bool StartingUp()
		{
			State = EBakingState.PENDING_SWARM;
			System.Threading.ThreadPool.QueueUserWorkItem (StartingUp,State);
			return false;
		}

		void StartingUp(object state)
		{
			bool bSuccessed = StartSwarmAgent();
			bSuccessed = bSuccessed && OpenJob();
			if (!bSuccessed) {
				Error = EBakingError.JOB_START_FAILURE;
			} else {
				State = EBakingState.SWARM_STARTED;
			}
		}

        bool Export()
        {
			EBakingError ExportError = Exporter.ExportError;
            bool IsDone = Exporter.ExportingState == EExportingState.EXPORT_DONE;
			if(IsDone && ExportError== EBakingError.NONE)
			{
				Exporter.ClearSceneData(BakingContext);
				return true;
			}
			Error = ExportError;
            return false;
        }
        bool ImportResults()
        {
			bool ImportError = Importer.HasError;
			if (Importer.IsDone && !ImportError) {
				return true;
			}
			if (ImportError) {
				Error = EBakingError.RESULT_IMPORT_FAILURE;
			}
			return false;
        }

        bool EncodeTextures()
        {
			DawnLightingStatistics.BeginEvent(EBakingState.ENCODING);

			bool bSuccessed = Importer.EncodeTextures (BakingContext);

			if (!bSuccessed) {
				Error = EBakingError.RESULT_ENCODING_FAILURE;
			}

			DawnLightingStatistics.EndEvent(EBakingState.ENCODING);
			return bSuccessed;
        }

        bool ApplyResults()
        {
			if (Importer.ApplyResults (BakingContext)) {
				return true;
			}
			Error = EBakingError.RESULT_APPLY_FAILURE;
            return false;
        }

		bool SaveResults()
		{
			bool bSuccessed = false;
            try
            {
				bSuccessed = Importer.SaveResults(BakingContext);
			}
			catch(Exception e)
            {
				BakingContext.LogErrorFormat("SaveResults Failure With {0}", e);
			}
			if(!bSuccessed)
            {
				Error = EBakingError.RESULT_SAVE_FAILURE;
			}
			return bSuccessed;
		}

		bool ConvertResults()
		{
			if(!BakingContext.IsEnableResultConverting)
            {
				return true;
            }
			bool bSuccessed = false;
			try
			{
				bSuccessed = Importer.ConvertResults(BakingContext);
			}
			catch (Exception e)
			{
				BakingContext.LogErrorFormat("SaveResults Failure With {0}", e);
			}
			if (!bSuccessed)
			{
				Error = EBakingError.RESULT_CONVERT_FAILURE;
			}
			return bSuccessed;
		}
		public void ClearError()
		{
			Error = EBakingError.NONE;
		}

		public EExportingState ExportingState
		{
			get{ return Exporter.ExportingState;}
		}

		public float ExportingProgress
		{
			get{ return Exporter.ExportingProgress;}
		}

		public EBakingState BuildingState
		{
			get{ return State;}
		}

		public EBakingError BuildError
		{
			get{ return Error;}
		}

		public bool IsBuildSuccessed
		{
			get { return bBuildSuccessed;}
		}

		public bool IsBuildCompleted
		{
			get { return bBuildCompleted; }
			set { bBuildCompleted = value; }
		}
	}
}

