﻿
using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using System.Text;
using UniRx;
using Extensions;

namespace Modules.MessagePack
{
    public static class MessagePackCodeGenerator
    {
        //----- params -----

        private const string CodeGenerateCommand = "mpc";

        //----- field -----

        //----- property -----

        //----- method -----

        public static bool Generate()
        {
            var isSuccess = false;

            var generateInfo = new MessagePackCodeGenerateInfo();

            var lastUpdateTime = DateTime.MinValue;

            if (File.Exists(generateInfo.CsFilePath))
            {
                var fileInfo = new FileInfo(generateInfo.CsFilePath);

                lastUpdateTime = fileInfo.LastWriteTime;
            }

            #if UNITY_EDITOR_OSX

            SetMsBuildPath();

            #endif

            var codeGenerateResult = ProcessUtility.Start(CodeGenerateCommand, generateInfo.CommandLineArguments);

            if (codeGenerateResult.Item1 == 0)
            {
                isSuccess = CsFileUpdate(generateInfo, lastUpdateTime);
            }

            OutputGenerateLog(isSuccess, generateInfo);

            if (!isSuccess)
            {
                var error = codeGenerateResult.Item2;
                
                Debug.LogError(error);

                throw new Exception(error);
            }

            return true;
        }

        public static IObservable<bool> GenerateAsync()
        {
            return Observable.FromMicroCoroutine<bool>(observer => GenerateInternalAsync(observer));
        }

        private static IEnumerator GenerateInternalAsync(IObserver<bool> observer)
        {
            var isSuccess = false;
            
            var generateInfo = new MessagePackCodeGenerateInfo();

            var lastUpdateTime = DateTime.MinValue;
            
            if (File.Exists(generateInfo.CsFilePath))
            {
                var fileInfo = new FileInfo(generateInfo.CsFilePath);

                lastUpdateTime = fileInfo.LastWriteTime;
            }

            #if UNITY_EDITOR_OSX

            SetMsBuildPath();

            #endif

            var codeGenerateTask = ProcessUtility.StartAsync(CodeGenerateCommand, generateInfo.CommandLineArguments);

            while (!codeGenerateTask.IsCompleted)
            {
                yield return null;
            }

            if (codeGenerateTask.Result.Item1 == 0)
            {
                isSuccess = CsFileUpdate(generateInfo, lastUpdateTime);
            }

            OutputGenerateLog(isSuccess, generateInfo);

            if (!isSuccess)
            {
                var error = codeGenerateTask.Result.Item2;

                using (new DisableStackTraceScope())
                {
                    Debug.LogError(error);
                }
                
                observer.OnError(new Exception(error));

                yield break;
            }

            observer.OnNext(true);
            observer.OnCompleted();
        }

        private static void SetMsBuildPath()
        {
            var msbuildPath = MessagePackConfig.Prefs.msbuildPath;

            var environmentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);

            var path = string.Format("{0}:{1}", environmentPath, msbuildPath);

            Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.Process);
        }

        private static void ImportGeneratedCsFile(MessagePackCodeGenerateInfo generateInfo)
        {
            var assetPath = UnityPathUtility.ConvertFullPathToAssetPath(generateInfo.CsFilePath);

            if (File.Exists(generateInfo.CsFilePath))
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        private static bool CsFileUpdate(MessagePackCodeGenerateInfo generateInfo, DateTime lastUpdateTime)
        {
            var isCsFileUpdate = false;

            if (File.Exists(generateInfo.CsFilePath))
            {
                var fileInfo = new FileInfo(generateInfo.CsFilePath);

                isCsFileUpdate = lastUpdateTime < fileInfo.LastWriteTime;
            }

            ImportGeneratedCsFile(generateInfo);

            return isCsFileUpdate;
        }

        private static void OutputGenerateLog(bool result, MessagePackCodeGenerateInfo generateInfo)
        {
            using (new DisableStackTraceScope())
            {
                var logBuilder = new StringBuilder();

                logBuilder.AppendLine();
                logBuilder.AppendLine();
                logBuilder.AppendFormat("MessagePack file : {0}", generateInfo.CsFilePath).AppendLine();
                logBuilder.AppendLine();
                logBuilder.AppendFormat("Command:").AppendLine();
                logBuilder.AppendLine(CodeGenerateCommand + generateInfo.CommandLineArguments);

                if (result)
                {
                    logBuilder.Insert(0, "MessagePack code generate success!");

                    Debug.Log(logBuilder.ToString());
                }
                else
                {
                    logBuilder.Insert(0, "MessagePack code generate failed.");

                    Debug.LogError(logBuilder.ToString());
                }
            }
        }
    }
}
