using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUBaking
{
	public enum EDawnLogLevel
	{
		DEBUG = 0,
		INFO = 1,
		WARNING = 2,
		ERROR = 3,
		FAIL = 4
	}

	public sealed class DawnDebug
	{
		public static EDawnLogLevel LogLevel = EDawnLogLevel.INFO;

		public static void Print(string format, params object[] args)
		{
			if (LogLevel <= EDawnLogLevel.DEBUG) {
				Debug.LogFormat(format,args);
			}
		}

		public static void Log(string format)
		{
			if (LogLevel <= EDawnLogLevel.INFO) {
				Debug.Log(format);
			}
		}

		public static void LogFormat (string format, params object[] args)
		{
			if (LogLevel <= EDawnLogLevel.INFO) {
				Debug.LogFormat (format, args);
			}
		}

		public static void LogWarning (string format)
		{
			if (LogLevel <= EDawnLogLevel.WARNING) {
				Debug.LogWarning (format);
			}
		}

		public static void LogWarningFormat (string format, params object[] args)
		{
			if (LogLevel <= EDawnLogLevel.WARNING) {
				Debug.LogWarningFormat (format, args);
			}
		}

		public static void LogError (string format)
		{
			Debug.LogError (format);
		}

		public static void LogErrorFormat (string format, params object[] args)
		{
			Debug.LogErrorFormat (format, args);
		}

		public static void LogException (System.Exception e)
		{
			Debug.LogException (e);
		}

		public static void AssertFormat (bool condition,string format, params object[] args)
		{
			if (LogLevel <= EDawnLogLevel.DEBUG) {
				Debug.AssertFormat (condition, format, args);
			}
		}
	}
}
