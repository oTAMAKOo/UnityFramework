﻿
#if ENABLE_MOONSHARP

using UnityEngine;
using System;
using System.Linq;
using System.Text;
using Extensions;
using UniRx;
using Modules.ExternalResource;
using MoonSharp.Interpreter;

namespace Modules.AdvKit.Standard
{
    public sealed class LoadRequest : Command
    {
        //----- params -----

        //----- field -----

        private IDisposable loadDisposable = null;

        //----- property -----

        public override string CommandName { get { return "LoadRequest"; } }

        //----- method -----

        public override object GetCommandDelegate()
        {
            return (Func<DynValue>)CommandFunction;
        }

        private DynValue CommandFunction()
        {
            var advEngine = AdvEngine.Instance;

            var requests = advEngine.Resource.GetRequests();

            var resourcePaths = requests.Select(x => x.Key).ToArray();

            var builder = new StringBuilder();

            builder.Append("Request Files").AppendLine();

            foreach (var resourcePath in resourcePaths)
            {
                var assetInfo = ExternalResources.Instance.GetAssetInfo(resourcePath);

                if (assetInfo != null)
                {
                    builder.AppendFormat("{0} ({1}byte)", assetInfo.ResourcePath, assetInfo.FileSize).AppendLine();
                }
                else
                {
                    Debug.LogErrorFormat("AssetInfo not found. {0}", resourcePath);
                }
            }

            using (new DisableStackTraceScope(LogType.Log))
            {
                Debug.Log(builder.ToString());
            }

            loadDisposable = requests.Select(x => x.Value).WhenAll()
                .Subscribe(_ => advEngine.Resume())
                .AddTo(Disposable);

            return YieldWait;
        }

        public void Cancel()
        {
            if (loadDisposable != null)
            {
                loadDisposable.Dispose();
                loadDisposable = null;
            }
        }
    }
}

#endif
