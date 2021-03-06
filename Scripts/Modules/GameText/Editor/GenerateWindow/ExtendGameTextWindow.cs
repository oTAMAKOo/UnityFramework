﻿
using Extensions;
using UnityEngine;
using UnityEditor;
using Extensions.Devkit;

namespace Modules.GameText.Editor
{
    public sealed class ExtendGameTextWindow : GenerateWindowBase<ExtendGameTextWindow>
    {
        //----- params -----

        public const string WindowTitle = "GameText-Extend";

        //----- field -----

        //----- property -----

        //----- method -----

        public static void Open()
        {
            Instance.Initialize();
        }

        private void Initialize()
        {
            titleContent = new GUIContent(WindowTitle);

            minSize = new Vector2(250, 200f);

            Show(true);
        }

        void OnGUI()
        {
            var generateInfos = GameTextLanguage.Infos;

            if (generateInfos == null) { return; }

            Reload();

            var extendGameTextSetting = config.ExtendGameText;
            
            // 言語情報.
            var info = GetCurrentLanguageInfo();

            GUILayout.Space(2f);

            EditorLayoutTools.ContentTitle("Asset");

            using (new ContentsScope())
            {
                GUILayout.Space(4f);

                // 生成制御.
                using (new DisableScope(info == null))
                {
                    if (GUILayout.Button("Generate"))
                    {
                        GameTextGenerator.Generate(GameText.AssetType.Extend, info);

                        Repaint();
                    }
                }

                GUILayout.Space(2f);
            }

            // エクセル制御.
            ControlExcelGUIContents(extendGameTextSetting);

            // 言語選択.
            SelectLanguageGUIContents();
        }
    }
}
