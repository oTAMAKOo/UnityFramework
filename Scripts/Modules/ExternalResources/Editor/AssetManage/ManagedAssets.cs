﻿
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Extensions;
using Extensions.Devkit;
using Modules.Devkit.ScriptableObjects;

namespace Modules.ExternalResource.Editor
{
    public sealed class ManagedAssets : ReloadableScriptableObject<ManagedAssets>
    {
        //----- params -----

        //----- field -----
        
        [SerializeField, HideInInspector]
        private ManageInfo[] manageInfos = null;

        private Dictionary<string, ManageInfo> manageInfoDictionary = null;

        //----- property -----

        //----- method -----

        protected override void OnLoadInstance()
        {
            DeleteInvalidInfo();
        }

        public void SetManageInfos(ManageInfo[] manageInfos)
        {
            this.manageInfos = manageInfos
                .Where(x => x != null)
                .Where(x => !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(x.guid)))
                .ToArray();

            manageInfoDictionary = manageInfos.ToDictionary(x => x.guid);

            UnityEditorUtility.SaveAsset(this);
        }

        public ManageInfo[] GetAllInfos()
        {
            return manageInfos;
        }

        public ManageInfo[] GetCategoryInfos(string category)
        {
            if (string.IsNullOrEmpty(category)){ return GetAllInfos(); }

            return manageInfos.Where(x => x.category == category).ToArray();
        }

        public ManageInfo GetManageInfo(string assetGuid)
        {
            if (manageInfoDictionary == null)
            {
                manageInfoDictionary = manageInfos.ToDictionary(x => x.guid);
            }

            return manageInfoDictionary.GetValueOrDefault(assetGuid);
        }

        public void DeleteInvalidInfo()
        {
            var list = new List<ManageInfo>();

            for (var i = 0; i < manageInfos.Length; i++)
            {
                var manageInfo = manageInfos[i];

                if (manageInfo == null){ continue; }

                if (string.IsNullOrEmpty(manageInfo.guid)) { continue; }

                var assetPath = AssetDatabase.GUIDToAssetPath(manageInfo.guid);

                var path = UnityPathUtility.ConvertAssetPathToFullPath(assetPath);

                var isFolder = AssetDatabase.IsValidFolder(assetPath);

                if (isFolder)
                {
                    if (!Directory.Exists(path)) { continue; }
                }
                else
                {
                    if (!File.Exists(path)) { continue; }
                }

                list.Add(manageInfo);
            }

            if (list.Count != manageInfos.Length)
            {
                manageInfos = list.ToArray();

                UnityEditorUtility.SaveAsset(this);
            }
        }
    }
}
