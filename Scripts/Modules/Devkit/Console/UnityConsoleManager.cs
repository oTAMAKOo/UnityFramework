﻿
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Extensions;
using Modules.Devkit.Prefs;

namespace Modules.Devkit.Console
{
    [Serializable]
    public sealed class ConsoleInfo
    {
        public string eventName = null;
        public bool enable = false;
    }

    public sealed class UnityConsoleManager : Singleton<UnityConsoleManager>
    {
        //----- params -----

        private static class Prefs
        {
            public static bool isEnable
            {
                get { return ProjectPrefs.GetBool("UnityConsolePrefs-isEnable", true); }
                set { ProjectPrefs.SetBool("UnityConsolePrefs-isEnable", value); }
            }

            public static string config
            {
                get { return ProjectPrefs.GetString("UnityConsolePrefs-config", string.Empty); }
                set { ProjectPrefs.SetString("UnityConsolePrefs-config", value); }
            }
        }

        //----- field -----

        public IReadOnlyList<ConsoleInfo> ConsoleInfos { get; private set; }

        //----- property -----

        //----- method -----

        protected override void OnCreate()
        {
            Load();
        }

        public bool IsEnable()
        {
            return Prefs.isEnable;
        }

        public void SetEnable(bool enable)
        {
            Prefs.isEnable = enable;
        }

        public bool IsEventEnable(string eventName)
        {
            var info = ConsoleInfos.FirstOrDefault(x => x.eventName == eventName);

            if (info == null){ return true; }

            return info.enable;
        }

        public ConsoleInfo[] Load()
        {
            var consoleInfos = new List<ConsoleInfo>();

            var configJson = Prefs.config;

            if (!string.IsNullOrEmpty(configJson))
            {
                var infos = JsonConvert.DeserializeObject<ConsoleInfo[]>(configJson);

                consoleInfos = infos.ToList();
            }

            if (consoleInfos.IsEmpty())
            {
                var info = new ConsoleInfo()
                {
                    eventName = UnityConsole.InfoEvent.ConsoleEventName,
                    enable = true,
                };

                consoleInfos.Add(info);
            }

            ConsoleInfos = consoleInfos;

            return ConsoleInfos.ToArray();
        }

        public void Save(IEnumerable<ConsoleInfo> consoleInfos)
        {
            ConsoleInfos = consoleInfos.Where(x => !string.IsNullOrEmpty(x.eventName)).ToArray();

            var json = JsonConvert.SerializeObject(ConsoleInfos);

            Prefs.config = json;
        }
    }
}
