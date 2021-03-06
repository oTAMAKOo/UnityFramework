﻿
using UnityEditor;
using UnityEditor.SceneManagement;
using UniRx;
using Extensions;
using Modules.Devkit.EventHook;

namespace Modules.Devkit.CleanComponent
{
    public sealed class SceneParticleComponentCleaner : ParticleComponentCleaner
    {
        //----- params -----

        //----- field -----

        //----- property -----

        //----- method -----

        [InitializeOnLoadMethod]
        private static void InitializeOnLoadMethod()
        {
            CurrentSceneSaveHook.OnSaveSceneAsObservable().Subscribe(x => Clean(x));
        }

        public static void Clean()
        {
            var activeScene = EditorSceneManager.GetActiveScene();

            Clean(activeScene.path);
        }

        private static void Clean(string sceneAssetPath)
        {
            if (!Prefs.autoClean) { return; }

            var activeScene = EditorSceneManager.GetActiveScene();

            if (activeScene.path != sceneAssetPath) { return; }

            var rootGameObjects = activeScene.GetRootGameObjects();

            if (!CheckExecute(rootGameObjects)) { return; }

            foreach (var rootGameObject in rootGameObjects)
            {
                ModifyParticleSystemComponent(rootGameObject);
            }
        }
    }
}
