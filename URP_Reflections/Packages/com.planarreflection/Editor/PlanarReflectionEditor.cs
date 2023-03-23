using UnityEditor;
using UnityEngine;

namespace H3D.URP
{
    public class RenderingLayerMaskFieldAttribute : PropertyAttribute
    {
        [RenderingLayerMaskField]
        public int renderingLayerMask = -1;
    }
    [CustomPropertyDrawer(typeof(RenderingLayerMaskFieldAttribute))]
    public class RenderingLayerMaskDrawer : PropertyDrawer
    {
       static string[] renderingLayerNames = new string[32];
       
        public static void Draw(
            Rect position, SerializedProperty property, GUIContent label
        )
        {
            for (int i = 0; i < renderingLayerNames.Length; i++)
            {
                if (i==0)
                {
                    renderingLayerNames[i] = "Layer " + (i )+": Light Layer default";  
                }
                else  renderingLayerNames[i] = "Layer " + (i );
            }

            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int mask = property.intValue;
            if (mask == int.MaxValue)
            {
                mask = -1;
            }
            mask = EditorGUI.MaskField(
                position, label, mask, renderingLayerNames
            );
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = mask == -1 ? int.MaxValue : mask;
            }
            EditorGUI.showMixedValue = false;
        }
        public static void Draw(SerializedProperty property, GUIContent label)
        {
            Draw(EditorGUILayout.GetControlRect(), property, label);
        }
    }
    
    [CustomEditor(typeof(ReflectionPlane))]
    public class ReflectionRendererDataEditor : Editor
    {
        private static class Styles
        {
            //General
            public static readonly GUIContent ReflectAxis = new GUIContent("Reflect Axis", "Controls which axis is up for the plane.");
            public static readonly GUIContent ClipPlaneOffset = new GUIContent("Clip Plane Offset", "Controls the near clip plane value.");
            public static readonly GUIContent AdaptiveBlur = new GUIContent("Adaptive Blur", "Adaptive Blur.");
            public static readonly GUIContent GradientDistance = new GUIContent("Gradient Distance", "Controls the max gradient diatance.");
            public static readonly GUIContent GradientIntensity = new GUIContent("Gradient Intensity", "Enable access gradient.");
            public static readonly GUIContent BlurPower = new GUIContent("Blur Power", "Controls the blue effect power.");            
            public static readonly GUIContent ReflectionMode = new GUIContent("ReflectionMode", "");
            public static readonly GUIContent TextureSize = new GUIContent("Texture Size", "set the max texture size of reflection.");
            //Usual Planar Reflection
            public static readonly GUIContent RenderLayerMask = new GUIContent("Render Layer Mask", "Controls which Render layers this renderer draws.");
            public static readonly GUIContent ReflectSky = new GUIContent("Reflect Sky", "Controls whether to render skybox.");
        }
        
        //General
        SerializedProperty m_ReflectionMode;
        SerializedProperty m_ReflectAxis;
        SerializedProperty m_GradientDistance;
        SerializedProperty m_GradientIntenity;
        SerializedProperty m_BlurPower;
        SerializedProperty m_AdaptiveBlur;
        SerializedProperty m_ClipPlaneOffset;
        SerializedProperty m_TextureSize;
    
        //Usual Planar Reflection
        SerializedProperty m_RenderLayerMask;
        SerializedProperty m_ReflectSky;

        private void OnEnable()
        {
            //General
            m_ReflectAxis = serializedObject.FindProperty("m_ReflectAxis");
            m_ReflectionMode = serializedObject.FindProperty("m_Technique");
            m_ClipPlaneOffset = serializedObject.FindProperty("m_ClipPlaneOffset");
            m_GradientDistance = serializedObject.FindProperty("m_GradientDistance");
            m_GradientIntenity = serializedObject.FindProperty("m_GradientIntensity");
            m_BlurPower = serializedObject.FindProperty("m_BlurPower");
            m_AdaptiveBlur = serializedObject.FindProperty("m_AdaptiveBlur");
            m_TextureSize = serializedObject.FindProperty("m_TextureSize");
            
            //Usual Planar Reflection
            m_ReflectSky = serializedObject.FindProperty("m_ReflectSky");
            m_RenderLayerMask = serializedObject.FindProperty("m_RenderLayerMask");
        }
    
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_ReflectionMode, Styles.ReflectionMode);
            EditorGUILayout.PropertyField(m_ReflectAxis, Styles.ReflectAxis);
            EditorGUILayout.PropertyField(m_TextureSize, Styles.TextureSize);
            EditorGUILayout.PropertyField(m_ClipPlaneOffset, Styles.ClipPlaneOffset);
            EditorGUILayout.PropertyField(m_GradientIntenity, Styles.GradientIntensity);
            EditorGUILayout.PropertyField(m_GradientDistance, Styles.GradientDistance);
            
            EditorGUILayout.PropertyField(m_BlurPower, Styles.BlurPower);            
            EditorGUILayout.PropertyField(m_AdaptiveBlur, Styles.AdaptiveBlur);
            if (m_AdaptiveBlur.boolValue)
            {
                m_GradientIntenity.boolValue = true;
            }

            if (m_ReflectionMode.intValue == 0)
            {
                RenderingLayerMaskDrawer.Draw(m_RenderLayerMask, Styles.RenderLayerMask);
                EditorGUILayout.PropertyField(m_ReflectSky, Styles.ReflectSky);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
