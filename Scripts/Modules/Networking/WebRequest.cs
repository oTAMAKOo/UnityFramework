﻿﻿
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UniRx;
using Newtonsoft.Json;
using Extensions;
using MessagePack;
using MessagePack.Resolvers;
using Modules.MessagePack;

namespace Modules.Networking
{
    public enum DataFormat
    {
        Json,
        MessagePack,
    }

    public abstract class WebRequest
    {
        //----- params -----

        //----- field -----

        protected UnityWebRequest request = null;

        //----- property -----

        /// <summary> リクエストタイプ. </summary>
        public string Method { get { return request.method; } }

        /// <summary> URL. </summary>
        public string HostUrl { get; private set; }

        /// <summary> リクエストURL. </summary>
        public string Url { get; private set; }

        /// <summary> ヘッダー情報. </summary>
        public IDictionary<string, string> Headers { get; private set; }
        
        /// <summary> URLパラメータ. </summary>
        public IDictionary<string, object> UrlParams { get; private set; }

        /// <summary> 送受信データの圧縮. </summary>
        public bool Compress { get; private set; }

        /// <summary> 通信データフォーマット. </summary>
        public DataFormat Format { get; private set; }

        /// <summary> サーバーへ送信するデータを扱うオブジェクト. </summary>
        public UploadHandler UploadHandler { get; private set; }

        /// <summary> サーバーから受信するデータを扱うオブジェクト. </summary>
        public DownloadHandler DownloadHandler { get; private set; }

        /// <summary> タイムアウト時間(秒). </summary>
        public virtual int TimeOutSeconds { get { return 3; } }

        /// <summary> 通信中か. </summary>
        public bool Connecting { get; private set; }

        /// <summary> ステータスコード. </summary>
        public string StatusCode
        {
            get { return request.GetResponseHeaders().GetValueOrDefault("STATUS"); }
        }

        //----- method -----

        public virtual void Initialize(string hostUrl, bool compress, DataFormat format = DataFormat.MessagePack)
        {
            HostUrl = hostUrl;
            Compress = compress;
            Format = format;

            Headers = new Dictionary<string, string>();
            UrlParams = new Dictionary<string, object>();

            DownloadHandler = CreateDownloadHandler();

            Connecting = false;
        }

        protected virtual void CreateWebRequest(string method)
        {
            var uri = BuildUri();

            request = new UnityWebRequest(uri);

            request.method = method;
            request.timeout = TimeOutSeconds;

            SetRequestHeaders();

            Url = request.url;
        }

        public IObservable<TResult> Get<TResult>(IProgress<float> progress = null) where TResult : class
        {
            CreateWebRequest(UnityWebRequest.kHttpVerbGET);

            request.downloadHandler = CreateDownloadHandler();

            return SendRequest<TResult>(progress);
        }

        public IObservable<TResult> Post<TResult, TContent>(TContent content, IProgress<float> progress = null) where TResult : class
        {
            CreateWebRequest(UnityWebRequest.kHttpVerbPOST);

            request.uploadHandler = CreateUploadHandler(content);
            request.downloadHandler = CreateDownloadHandler();

            return SendRequest<TResult>(progress);
        }

        public IObservable<TResult> Put<TResult, TContent>(TContent content, IProgress<float> progress = null) where TResult : class
        {
            CreateWebRequest(UnityWebRequest.kHttpVerbPUT);

            request.uploadHandler = CreateUploadHandler(content);
            request.downloadHandler = CreateDownloadHandler();

            return SendRequest<TResult>(progress);
        }

        public IObservable<TResult> Patch<TResult, TContent>(TContent content, IProgress<float> progress = null) where TResult : class
        {
            const string kHttpVerbPatch = "PATCH";

            CreateWebRequest(kHttpVerbPatch);

            request.uploadHandler = CreateUploadHandler(content);
            request.downloadHandler = CreateDownloadHandler();

            return SendRequest<TResult>(progress);
        }

        public IObservable<TResult> Delete<TResult>(IProgress<float> progress = null) where TResult : class
        {
            CreateWebRequest(UnityWebRequest.kHttpVerbDELETE);

            request.downloadHandler = CreateDownloadHandler();

            return SendRequest<TResult>(progress);
        }

        public void Cancel(bool throwException = false)
        {
            request.Abort();

            if (throwException)
            {
                throw new UnityWebRequestErrorException(request);
            }
        }

        private IObservable<TResult> SendRequest<TResult>(IProgress<float> progress) where TResult : class
        {
            Connecting = true;

            return request.Send(progress).Select(x => ReceiveResponse<TResult>(x)).Do(x => Connecting = false);
        }

        private TResult ReceiveResponse<TResult>(byte[] value) where TResult : class
        {
            if (value == null || value.IsEmpty()) { return null; }

            TResult result = null;

            value = DecryptResponseData(value);

            switch (Format)
            {
                case DataFormat.Json:
                    {
                        if (Compress)
                        {
                            value = value.Decompress();
                        }

                        var json = Encoding.UTF8.GetString(value);

                        if (!string.IsNullOrEmpty(json))
                        {
                            result = JsonConvert.DeserializeObject<TResult>(json);
                        }
                    }
                    break;

                case DataFormat.MessagePack:
                    {
                        if (value != null && value.Any())
                        {
                            var options = StandardResolverAllowPrivate.Options.WithResolver(UnityContractResolver.Instance);

                            if (Compress)
                            {
                                options = options.WithCompression(MessagePackCompression.Lz4Block);
                            }

                            result = MessagePackSerializer.Deserialize<TResult>(value, options);
                        }
                    }
                    break;
            }

            return result;
        }
        
        private Uri BuildUri()
        {
            RegisterDefaultUrlParams();

            var queryBuilder = new StringBuilder();

            for (var i = 0; i < UrlParams.Count; i++)
            {
                var item = UrlParams.ElementAt(i);

                queryBuilder.Append(i == 0 ? "?" : "&");

                var query = string.Format("{0}={1}", item.Key, item.Value);

                var encryptQuery = EncryptUriQuery(query);

                var escapeStr = Uri.EscapeDataString(encryptQuery);

                queryBuilder.Append(escapeStr);
            }

            var uriBuilder = new UriBuilder(HostUrl)
            {
                Query = queryBuilder.ToString(),
            };

            return uriBuilder.Uri;
        }
        
        private void SetRequestHeaders()
        {
            RegisterDefaultHeader();

            foreach (var header in Headers)
            {
                var key = header.Key.TrimEnd('\0');
                var value = header.Value.TrimEnd('\0');

                request.SetRequestHeader(key, value);
            }
        }

        protected virtual DownloadHandler CreateDownloadHandler()
        {
            return new DownloadHandlerBuffer();
        }

        protected virtual UploadHandler CreateUploadHandler<TContent>(TContent content)
        {
            if (content == null) { return null; }

            byte[] bodyData = null;

            switch (Format)
            {
                case DataFormat.Json:
                    {
                        var json = JsonConvert.SerializeObject(content);

                        bodyData = Encoding.UTF8.GetBytes(json);
                        
                        if (Compress)
                        {
                            bodyData = bodyData.Compress();
                        }
                    }
                    break;

                case DataFormat.MessagePack:
                    {
                        var options = StandardResolverAllowPrivate.Options.WithResolver(UnityContractResolver.Instance);

                        if (Compress)
                        {
                            options = options.WithCompression(MessagePackCompression.Lz4Block);
                        }

                        bodyData = MessagePackSerializer.Serialize(content, options);
                    }
                    break;
            }

            bodyData = EncryptBodyData(bodyData);

            return new UploadHandlerRaw(bodyData);
        }

        /// <summary> 常時付与されるヘッダー情報を登録 </summary>
        protected virtual void RegisterDefaultHeader() { }

        /// <summary> 常時付与されるURLパラメータを登録 </summary>
        protected virtual void RegisterDefaultUrlParams() { }

        /// <summary> URLパラメータ暗号化. </summary>
        protected abstract string EncryptUriQuery(string query);

        /// <summary> リクエストボディデータ暗号化. </summary>
        protected abstract byte[] EncryptBodyData(byte[] bytes);

        /// <summary> レスポンスデータ復号化. </summary>
        protected abstract byte[] DecryptResponseData(byte[] bytes);
    }
}
