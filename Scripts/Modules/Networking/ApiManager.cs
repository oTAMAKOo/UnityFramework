﻿﻿
using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using UniRx;
using Extensions;

namespace Modules.Networking
{
    public abstract class ApiManager<TInstance, TWebRequest> : Singleton<TInstance>
        where TInstance : ApiManager<TInstance, TWebRequest> where TWebRequest : WebRequest, new()
    {
        //----- params -----

        protected enum RequestErrorHandle
        {
            Retry,
            Cancel,
        }

        //----- field -----
        
        private TWebRequest currentWebRequest = null;
        private Queue<TWebRequest> webRequestQueue = null;

        //----- property -----

        /// <summary> 接続先URL. </summary>
        public string ServerUrl { get; private set; }

        /// <summary> 送受信データの圧縮. </summary>
        public bool Compress { get; private set; }

        /// <summary> データ内容フォーマット. </summary>
        public DataFormat Format { get; private set; }

        /// <summary> ヘッダー情報. </summary>
        public IDictionary<string, string> Headers { get; private set; }

        /// <summary> リトライ回数. </summary>
        public int RetryCount { get; private set; }

        /// <summary> リトライするまでの時間(秒). </summary>
        public float RetryDelaySeconds { get; private set; }

        //----- method -----

        protected ApiManager()
        {
            webRequestQueue = new Queue<TWebRequest>();
            Headers = new Dictionary<string, string>();
        }

        public virtual void Initialize(string serverUrl, bool compress = true, DataFormat format = DataFormat.MessagePack, int retryCount = 3, float retryDelaySeconds = 2)
        {
            ServerUrl = serverUrl;
            Compress = compress;
            Format = format;
            RetryCount = retryCount;
            RetryDelaySeconds = retryDelaySeconds;
        }

        /// <summary> リソースの取得. </summary>
        protected IObservable<TResult> Get<TResult>(TWebRequest webRequest, IProgress<float> progress = null) where TResult : class
        {
            var requestObserver = Observable.Defer(() => webRequest.Get<TResult>(progress));

            return Observable.FromMicroCoroutine<TResult>(observer => SnedRequestInternal(observer, webRequest, requestObserver));
        }

        /// <summary> リソースの作成、追加. </summary>
        protected IObservable<TResult> Post<TResult, TContent>(TWebRequest webRequest, TContent content, IProgress<float> progress = null) where TResult : class
        {
            var requestObserver = Observable.Defer(() => webRequest.Post<TResult, TContent>(content, progress));

            return Observable.FromMicroCoroutine<TResult>(observer => SnedRequestInternal(observer, webRequest, requestObserver));
        }

        /// <summary> リソースの更新、作成. </summary>
        protected IObservable<TResult> Put<TResult, TContent>(TWebRequest webRequest, TContent content, IProgress<float> progress = null) where TResult : class
        {
            var requestObserver = Observable.Defer(() => webRequest.Put<TResult, TContent>(content, progress));

            return Observable.FromMicroCoroutine<TResult>(observer => SnedRequestInternal(observer, webRequest, requestObserver));
        }

        /// <summary> リソースの削除. </summary>
        protected IObservable<TResult> Delete<TResult, TContent>(TWebRequest webRequest, TContent content, IProgress<float> progress = null) where TResult : class
        {
            var requestObserver = Observable.Defer(() => webRequest.Delete<TResult>(progress));

            return Observable.FromMicroCoroutine<TResult>(observer => SnedRequestInternal(observer, webRequest, requestObserver));
        }

        /// <summary> リクエスト制御 </summary>
        private IEnumerator SnedRequestInternal<TResult>(IObserver<TResult> observer, TWebRequest webRequest, IObservable<TResult> requestObserver) where TResult : class
        {
            // 通信待ちキュー.
            var waitQueueingYield = Observable.FromMicroCoroutine(() => WaitQueueingRequest(webRequest)).ToYieldInstruction(false);

            while (!waitQueueingYield.IsDone)
            {
                yield return null;
            }

            // 通信中.
            currentWebRequest = webRequest;
            
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var retryCount = 0;

            TResult result = null;

            // リクエスト実行.
            while (true)
            {
                var requestYield = requestObserver.ToYieldInstruction(false);                

                while (!requestYield.IsDone)
                {
                    yield return null;
                }

                //------ 通信成功 ------

                if (requestYield.HasResult && !requestYield.HasError)
                {
                    result = requestYield.Result;
                    break;
                }
                
                //------ 通信キャンセル ------

                if (requestYield.IsCanceled) { break; }

                //------ エラー ------

                if (requestYield.HasError)
                {
                    OnError(webRequest, requestYield.Error);
                }

                //------ リトライ回数オーバー ------

                if (RetryCount <= retryCount)
                {
                    OnRetryLimit(webRequest);
                    observer.OnError(requestYield.Error);
                    break;
                }

                //------ 通信失敗 ------

                // エラーハンドリングを待つ.
                var waitErrorHandling = WaitErrorHandling(webRequest, requestYield.Error).ToYieldInstruction(false);

                while (!waitErrorHandling.IsDone)
                {
                    yield return null;
                }

                if (waitErrorHandling.HasError)
                {
                    observer.OnError(requestYield.Error);
                    break;
                }

                if (waitErrorHandling.HasResult)
                {
                    switch (waitErrorHandling.Result)
                    {
                        case RequestErrorHandle.Retry:
                            retryCount++;
                            break;
                    }

                    // キャンセル時は通信終了.
                    if (waitErrorHandling.Result == RequestErrorHandle.Cancel)
                    {
                        break;
                    }
                }

                // リトライディレイ.
                var retryDelayYield = Observable.Timer(TimeSpan.FromSeconds(RetryDelaySeconds)).ToYieldInstruction();

                while (!retryDelayYield.IsDone)
                {
                    yield return null;
                }

            }

            if (result != null)
            {
                // 正常終了.
                sw.Stop();
                OnComplete(webRequest, result, sw.Elapsed.TotalMilliseconds);

                observer.OnNext(result);
            }

            // 通信完了.
            currentWebRequest = null;

            observer.OnCompleted();
        }

        /// <summary>
        /// 通信中の全リクエストを強制中止.
        /// </summary>
        protected void ForceCancelAll()
        {
            if (currentWebRequest != null)
            {
                currentWebRequest.Cancel();
                currentWebRequest = null;
            }

            if (webRequestQueue.Any())
            {
                webRequestQueue.Clear();
            }
        }

        /// <summary>
        /// 通信処理が同時に実行されないようにキューイング.
        /// </summary>
        private IEnumerator WaitQueueingRequest(TWebRequest webRequest)
        {
            // キューに追加.
            webRequestQueue.Enqueue(webRequest);

            while (true)
            {
                // キューが空になっていた場合はキャンセル扱い.
                if (webRequestQueue.IsEmpty())
                {
                    webRequest.Cancel(true);
                    break;
                }

                // 通信中のリクエストが存在しない & キューの先頭が自身の場合待ち終了.
                if (currentWebRequest == null && webRequestQueue.Peek() == webRequest)
                {
                    webRequestQueue.Dequeue();
                    break;
                }

                yield return null;
            }
        }
        
        protected TWebRequest SetupWebRequest(string url, IDictionary<string, object> urlParams)
        {
            var webRequest = new TWebRequest();

            webRequest.Initialize(PathUtility.Combine(ServerUrl, url), Compress, Format);

            foreach (var header in Headers)
            {
                webRequest.Headers.Add(header.Key, header.Value);
            }

            if (urlParams != null)
            {
                foreach (var urlParam in urlParams)
                {
                    webRequest.UrlParams.Add(urlParam.Key, urlParam.Value);
                }
            }


            return webRequest;
        }

        /// <summary> 成功時イベント. </summary>
        protected abstract void OnComplete<TResult>(TWebRequest webRequest, TResult result, double totalMilliseconds);

        /// <summary> リトライ回数を超えた時のイベント. </summary>
        protected abstract void OnRetryLimit(TWebRequest webRequest);

        /// <summary> 通信エラー時イベント. </summary>
        protected abstract void OnError(TWebRequest webRequest, Exception ex);

        /// <summary> 通信エラーのハンドリング. </summary>
        protected abstract IObservable<RequestErrorHandle> WaitErrorHandling(TWebRequest webRequest, Exception ex);
    }
}
