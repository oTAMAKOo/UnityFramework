﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using UniRx;
using Extensions;
using Modules.ApplicationEvent;

namespace Modules.Notifications
{
    public abstract partial class LocalPushNotification<Tinstance> : Singleton<Tinstance> where Tinstance : LocalPushNotification<Tinstance>
    {
        //----- params -----

        public sealed class NotificationInfo
        {
            public int Identifier { get; private set; }

            // 必須項目.
            public long UnixTime { get; private set; }
            public string Title { get; private set; }
            public string Message { get; private set; }

            // オプション.
            public int BadgeCount { get; set; }
            public string LargeIconResource { get; set; }
            public string SmallIconResource { get; set; }
            public Color32? Color { get; set; }

            public NotificationInfo(long unixTime, string title, string message)
            {
                Identifier = Instance.initializedTime + Instance.incrementCount;

                // 通知IDを重複させない為カウンタを加算.
                Instance.incrementCount++;

                UnixTime = unixTime;
                Title = title;
                Message = message;

                LargeIconResource = null;
                SmallIconResource = "notify_icon_small";
                Color = null;
                BadgeCount = 1;
            }
        }

        //----- field -----

        private bool enable = false;

        private int initializedTime = 0;

        private int incrementCount = 0;

        private Dictionary<long, NotificationInfo> notifications = null;

        private Subject<Unit> onNotificationRegister = null;

        private bool initialized = false;

        //----- property -----

        public bool Enable
        {
            get { return enable; }

            set
            {
                // 有効に切り替わった過去に登録した通知を削除.
                if (!enable && value)
                {
                    Clear();
                }

                enable = value;
            }
        }

        public abstract long CurrentTime { get; }

        //----- method -----

        protected LocalPushNotification() { }

        public void Initialize()
        {
            if (initialized){ return; }

            enable = false;

            initializedTime = (int)DateTime.Now.ToUnixTime();

            incrementCount = 0;

            notifications = new Dictionary<long, NotificationInfo>();

            // イベント登録.

            ApplicationEventHandler.OnQuitAsObservable()
                .Subscribe(_ => OnSuspend())
                .AddTo(Disposable);

            ApplicationEventHandler.OnSuspendAsObservable()
                .Subscribe(_ => OnSuspend())
                .AddTo(Disposable);

            ApplicationEventHandler.OnResumeAsObservable()
                .Subscribe(_ => OnResume())
                .AddTo(Disposable);

            initialized = true;
        }

        /// <summary>
        /// 通知を登録.
        /// </summary>
        /// <param name="info"> 通知のパラメータ</param>
        /// <returns> 登録成功時は正のID、失敗時は-1</returns>
        public long Set(NotificationInfo info)
        {
            if(!enable) { return -1; }

            notifications.Add(info.Identifier, info);

            return info.Identifier;
        }

        public void Remove(long id)
        {
            if (!enable){ return; }

            if (id == -1){ return; }

            if(notifications.ContainsKey(id))
            {
                notifications.Remove(id);
            }
        }

        private void Register()
        {
            if (!initialized || !enable) { return; }

            // 二重登録されないように一旦クリア.
            Clear();

            // 通知登録イベント.
            if(onNotificationRegister != null)
            {
                onNotificationRegister.OnNext(Unit.Default);
            }

            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR

            SetNotify();

            #endif

            notifications.Clear();
        }
        /// <summary> 通知をすべてクリア </summary>
        private void Clear()
        {
            if (!initialized || !enable) { return; }

            #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR

            ClearNotifications();

            #endif
        }

        private void OnSuspend()
        {
            Register();
        }

        private void OnResume()
        {
            Clear();
        }

        public IObservable<Unit> OnNotifyRegisterAsObservable()
        {
            return onNotificationRegister ?? (onNotificationRegister = new Subject<Unit>());
        }
    }
}
