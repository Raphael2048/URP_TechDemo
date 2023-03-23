using System;
using UnityEngine;
using System.Reflection;
using UnityEditor;



namespace GPUBaking.Editor
{
    public class AutoUIScript
    {
		public static void ReflectiveStructure(SerializedObject Target,DawnLocalizationAsset LocalizationAsset)
        {
			var Property = Target.GetIterator ();
			var BaseDepth = EditorGUI.indentLevel;

			int VisiableMask = 1 << 0;
			if (Property.NextVisible (true)) {
				while (Property.NextVisible(true)) {
					int PropertyMask = 1 << (Property.depth);
					if ((PropertyMask & VisiableMask) == PropertyMask) {
						EditorGUI.indentLevel = BaseDepth + Property.depth;
						var displayName = Property.displayName;
						var tooltip = Property.tooltip;

						#if UNITY_2018_1_OR_NEWER
						if (LocalizationAsset != null)
						{
							var displayItem = LocalizationAsset.GetItem(Property.name);
							if (displayItem != null)
							{
								if(!string.IsNullOrEmpty(displayItem.displayName))
                                {
									displayName = displayItem.displayName;
								}
								tooltip = displayItem.tooltip;
							}
						}
						#endif
						if (displayName.StartsWith("B "))
                        {
							displayName = displayName.Substring(2);
						}

						if (EditorGUILayout.PropertyField (Property,new GUIContent(displayName, string.IsNullOrEmpty(tooltip) ? displayName : tooltip),false)) {
							VisiableMask |= 1 << (Property.depth + 1);
						} else {
							VisiableMask &= ~(1 << (Property.depth + 1));
						}
					}
				}
			}

			EditorGUI.indentLevel = BaseDepth;
        }
	}
}
