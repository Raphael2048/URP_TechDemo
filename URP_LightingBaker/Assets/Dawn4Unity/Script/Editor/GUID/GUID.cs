using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Reflection;

namespace GPUBaking.Editor
{
    [System.Serializable]
    public class GUID
    {
        public UInt64 High;
        public UInt64 Low;

        public GUID(UInt64 InHigh,UInt64 InLow)
        {
            this.High = InHigh;
            this.Low = InLow;
        }

        public static GUID CreateGUID(UnityEngine.Object InObject)
        {
#if UNITY_2018_1_OR_NEWER
            var LocalInfo = PrefabUtility.GetPrefabInstanceHandle(InObject);
            var SharedInfo = PrefabUtility.GetCorrespondingObjectFromSource(InObject);
#else
            var LocalInfo = PrefabUtility.GetPrefabObject(InObject);
            var SharedInfo = PrefabUtility.GetPrefabParent(InObject);
#endif
            return LocalInfo != null && SharedInfo != null ? new GUID(GetCustomLocalID(SharedInfo), GetCustomLocalID(LocalInfo)) : new GUID(GetCustomLocalID(InObject), 0);
        }

        public static UInt64 GetCustomLocalID(UnityEngine.Object InObject)
        {
            PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
            SerializedObject serializedObject = new SerializedObject(InObject);
            inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);
            SerializedProperty localIdProp = serializedObject.FindProperty("m_LocalIdentfierInFile");
            long LocalId = localIdProp.longValue;
            serializedObject.Dispose();
            return (UInt64)LocalId;
        }

        protected bool Equals(GUID other)
        {
            return Low == other.Low && High == other.High;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GUID)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Low.GetHashCode() * 397) ^ High.GetHashCode();
            }
        }
    }
}
