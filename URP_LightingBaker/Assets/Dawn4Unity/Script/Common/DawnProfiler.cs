using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

namespace GPUBaking
{
	public class DawnProfilerSample
	{
		private string name;
		private Stopwatch watch;

		public DawnProfilerSample(string Name)
		{
			this.name = Name;
			this.watch = new Stopwatch();
			this.watch.Start ();
		}

		public void End()
		{
			this.watch.Stop ();
		}

		public string Name
		{
			get { return name;}
		}

		public float TakeTime
		{
			get { return watch.ElapsedMilliseconds / 1000.0f;}
		}
	}

	public abstract class DawnProfiler
	{
		static bool _Enabled = false;

		static float _ProfilingThreadhold = 0.01f;

		static UnsafeDawnProfiler _Profiler = new UnsafeDawnProfiler();

		static ThreadSafeProfiler _ThreadSafeProfiler = new ThreadSafeProfiler();

		public static bool Enable
		{
			set {
				_Enabled = value;
			}
			get {
				return _Enabled;
			}
		}

		public static float ProfilingThreadhold
		{
			set {
				_ProfilingThreadhold = value;
			}
			get {
				return _ProfilingThreadhold;
			}
		}

		public static void Print()
		{
			if (!_Enabled) {
				return;
			}
			_Profiler.Print ();
			_ThreadSafeProfiler.Print ();
		}

		public static void BeginSample(string eventName)
		{
			if (!_Enabled) {
				return;
			}
			_Profiler.BeginSample (eventName);
		}

		public static void EndSample()
		{
			if (!_Enabled) {
				return;
			}
			_Profiler.EndSample ();
		}

		public static void BeginSampleAnyThread(string eventName,out DawnProfilerSample Sample)
		{
			if (!_Enabled) {
				Sample = null;
				return;
			}
			Sample = _ThreadSafeProfiler.BeginSample (eventName);
		}

		public static void EndSampleAnyThread(DawnProfilerSample Sample)
		{
			if (!_Enabled)
				return;
			_ThreadSafeProfiler.EndSample (Sample);
		}
	}

	internal class UnsafeDawnProfiler
	{
		protected Dictionary<string,LinkedList<DawnProfilerSample>> Samples = new Dictionary<string,LinkedList<DawnProfilerSample>>();

		protected LinkedList<DawnProfilerSample> SampleStack = new LinkedList<DawnProfilerSample>();

		public void Reset()
		{
			SampleStack.Clear ();
			Samples.Clear ();
		}

		public void Print()
		{
			int GroupCount = 0;
			float GroupCost = 0;
			foreach (var SampleGroup in Samples) {
				GroupCost = 0;
				GroupCount = 0;
				foreach (var Sample in SampleGroup.Value) {
					GroupCost += Sample.TakeTime;
					GroupCount++;
				}
				if (GroupCost < DawnProfiler.ProfilingThreadhold)
					continue;

				if (GroupCount > 1) {
					DawnDebug.LogFormat ("DawnProfiler {0}: cost({1}),count({2}),avg({3})", SampleGroup.Key, GroupCost, GroupCount, GroupCost / GroupCount);
				} else {
					DawnDebug.LogFormat ("DawnProfiler {0}: cost({1}))",SampleGroup.Key,GroupCost);
				}
			}
			Reset ();
		}

		public void BeginSample(string eventName)
		{
			SampleStack.AddLast (new DawnProfilerSample (eventName));
		}

		public void EndSample()
		{
			DawnProfilerSample Sample = SampleStack.Last.Value;
			Sample.End ();
			SampleStack.RemoveLast ();

			AddSample (Sample);
		}

		protected void AddSample(DawnProfilerSample Sample)
		{
			LinkedList<DawnProfilerSample> SampleGroup;
			if (!Samples.TryGetValue (Sample.Name, out SampleGroup)) {
				SampleGroup = new LinkedList<DawnProfilerSample> ();
				Samples.Add (Sample.Name,SampleGroup);
			}
			SampleGroup.AddLast (Sample);
		}
	}
	
	internal sealed class ThreadSafeProfiler : UnsafeDawnProfiler
	{
		public new DawnProfilerSample BeginSample(string eventName)
		{
			return new DawnProfilerSample (eventName);
		}

		public void EndSample(DawnProfilerSample Sample)
		{
			Sample.End ();
			lock (this) {
				base.AddSample (Sample);
			}
		}
	}
}

