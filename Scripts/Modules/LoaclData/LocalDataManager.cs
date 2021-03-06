﻿
using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Extensions;
using MessagePack;
using MessagePack.Resolvers;
using Modules.MessagePack;

namespace Modules.LocalData
{
    public interface ILocalData { }

    public static class LocalDataExtension
    {
        public static void Save<T>(this T data) where T : class, ILocalData, new()
        {
            LocalDataManager.Save(data);
        }
    }

    public sealed class LocalDataManager : Singleton<LocalDataManager>
    {
        //----- params -----

        private const string DefaultKey = "5k7DpsG19A91R7Lkv261AMxCmjHFFFxX";
        private const string DefaultIv = "YiEs3x1as8JhK9qp";

        //----- field -----
        
        private AesCryptKey aesCryptKey = null;

        private Dictionary<Type, string> filePathCache = null;

        private Dictionary<Type, ILocalData> dataCache = null;

        //----- property -----

        public string FileDirectory { get; private set; }

        //----- method -----

        protected override void OnCreate()
        {
            filePathCache = new Dictionary<Type, string>();
            dataCache = new Dictionary<Type, ILocalData>();

            FileDirectory = Application.persistentDataPath + "/LocalData/";
        }

        public void SetCryptKey(AesCryptKey cryptKey)
        {
            Instance.aesCryptKey = cryptKey;
        }

        public void SetFileDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory)) { return; }

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            FileDirectory = directory;
        }

        public void CacheClear()
        {
            if (filePathCache != null)
            {
                filePathCache.Clear();
            }

            if (dataCache != null)
            {
                dataCache.Clear();
            }
        }

        public static void Load<T>() where T : class, ILocalData, new()
        {
            var type = typeof(T);

            var filePath = Instance.GetLocalDataFilePath<T>();

            var data = default(T);

            if (File.Exists(filePath))
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var bytes = new byte[fileStream.Length];

                    fileStream.Read(bytes, 0, bytes.Length);

                    var cryptKey = Instance.GetCryptKey();

                    bytes = bytes.Decrypt(cryptKey);

                    var options = StandardResolverAllowPrivate.Options
                        .WithCompression(MessagePackCompression.Lz4BlockArray)
                        .WithResolver(UnityContractResolver.Instance);

                    data = MessagePackSerializer.Deserialize<T>(bytes, options);
                }
            }
            else
            {
                data = new T();
            }

            Instance.dataCache[type] = data;
        }

        public static T Get<T>() where T : class, ILocalData, new()
        {
            var type = typeof(T);

            var dataCache = Instance.dataCache;

            if (!dataCache.ContainsKey(type))
            {
                Load<T>();
            }

            return dataCache.GetValueOrDefault(typeof(T)) as T;
        }

        public static void Save<T>(T data) where T : class, ILocalData, new()
        {
            var filePath = Instance.GetLocalDataFilePath<T>();

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                var options = StandardResolverAllowPrivate.Options
                    .WithCompression(MessagePackCompression.Lz4BlockArray)
                    .WithResolver(UnityContractResolver.Instance);

                var bytes = MessagePackSerializer.Serialize(data, options);

                var cryptKey = Instance.GetCryptKey();

                bytes = bytes.Encrypt(cryptKey);

                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        private string GetLocalDataFilePath<T>() where T : class, ILocalData, new()
        {
            var filePath = filePathCache.GetValueOrDefault(typeof(T));

            if (!string.IsNullOrEmpty(filePath)){ return filePath; }
            
            if (!Directory.Exists(FileDirectory))
            {
                Directory.CreateDirectory(FileDirectory);
            }

            var fileName = string.Empty;

            var cryptKey = GetCryptKey();
            
            var type = typeof(T);

            var fileNameAttribute = type.GetCustomAttributes(typeof(FileNameAttribute), false)
                .Cast<FileNameAttribute>()
                .FirstOrDefault();

            if (fileNameAttribute != null)
            {
                fileName = fileNameAttribute.FileName.Encrypt(cryptKey).GetHash();
            }
            else
            {
                throw new Exception(string.Format("FileNameAttribute is not set for this class.\n{0}", type.FullName));
            }

            filePath = PathUtility.Combine(FileDirectory, fileName);

            filePathCache.Add(typeof(T), filePath);

            return filePath;
        }

        private AesCryptKey GetCryptKey()
        {
            if (aesCryptKey == null)
            {
                aesCryptKey = new AesCryptKey(DefaultKey, DefaultIv);
            }

            return aesCryptKey;
        }

        public static void Delete<T>() where T : class, ILocalData, new()
        {
            var type = typeof(T);

            var filePath = Instance.GetLocalDataFilePath<T>();
            var dataCache = Instance.dataCache;

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (dataCache != null && dataCache.ContainsKey(type))
            {
                dataCache.Remove(type);
            }
        }

        public static void DeleteAll()
        {
            var directory = Instance.FileDirectory;
            var dataCache = Instance.dataCache;

            DirectoryUtility.Clean(directory);

            if (dataCache != null)
            {
                dataCache.Clear();
            }
        }
    }
}
