using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUBaking.Editor
{
    [System.Serializable]
    public class DawnLocalizationItem
    {
        public string key;
        public string displayName;
        public string tooltip;
    }

    [CreateAssetMenu(fileName = "DawnLocalizationAsset", menuName = "Dawn/Localization Settings", order = 2)]
    [System.Serializable]
    public class DawnLocalizationAsset : ScriptableObject
    {
        [SerializeField] List<DawnLocalizationItem> Items;

        Dictionary<string, DawnLocalizationItem> ItemCache = new Dictionary<string, DawnLocalizationItem>();

        public DawnLocalizationItem GetItem(string Key)
        {
            if(Items == null || Items.Count == 0)
            {
                return null;
            }
            DawnLocalizationItem FoundItem = null;
            if (ItemCache.TryGetValue(Key,out FoundItem))
            {
                return FoundItem;
            }

            foreach(var Item in Items)
            {
                if(Item.key == Key)
                {
                    FoundItem = Item;
                    ItemCache.Add(Key, Item);
                    break;
                }
            }
            return FoundItem;
        }

        public static string GetDisplayName(string Key)
        {
            var LocalizationAsset = Instance;
            if(LocalizationAsset == null)
            {
                return Key;
            }
            var Item = LocalizationAsset.GetItem(Key);
            if(Item == null)
            {
                return Key;
            }
            if(string.IsNullOrEmpty(Item.displayName))
            {
                return Key;
            }
            return Item.displayName;
        }

        private static string DefaultLocalationPath = "Assets/Dawn4Unity/Configs/Dawn_ZH_CN.asset";

        private static DawnLocalizationAsset _Instance = null;

        public static DawnLocalizationAsset Instance
        {
            get
            {
                if (_Instance == null)
                {
                    _Instance = UnityEditor.AssetDatabase.LoadAssetAtPath<DawnLocalizationAsset>(DefaultLocalationPath);
                }
                return _Instance;
            }
            set
            {
                _Instance = value;
            }
        }
    }
}