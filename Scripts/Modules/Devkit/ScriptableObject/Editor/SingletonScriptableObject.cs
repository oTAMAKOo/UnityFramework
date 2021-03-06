﻿
using UnityEngine;
using UnityEditor;
using System.Linq;
using Extensions.Devkit;

namespace Modules.Devkit.ScriptableObjects
{
    public abstract class SingletonScriptableObject<T> : UnityEngine.ScriptableObject where T : SingletonScriptableObject<T>
    {
        //----- params -----

        //----- field -----

        protected static T instance = null;

        //----- property -----

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = LoadInstance();

                    if (instance != null)
                    {
                        instance.OnLoadInstance();
                    }
                }

                return instance;
            }
        }

        //----- method -----

        void Awake()
        {
            if (instance != null && instance != this)
            {
                EditorApplication.delayCall += () =>
                {
                    Debug.LogWarningFormat("Can not create multiple instance.\nSingleton class : {0}", typeof(T).FullName);

                    var path = AssetDatabase.GetAssetPath(this);
                    AssetDatabase.DeleteAsset(path);
                };
            }
        }

        public static void Reload()
        {
            instance = LoadInstance();
        }

        protected static T LoadInstance()
        {
            var targetAsset = UnityEditorUtility.FindAssetsByType<T>(string.Format("t:{0}", typeof(T).FullName)).FirstOrDefault();

            if (targetAsset == null)
            {
                Debug.LogErrorFormat("Not found a matching instance.\nclass : {0}", typeof(T).FullName);
            }

            return targetAsset;
        }

        protected virtual void OnLoadInstance() { }
    }
}
