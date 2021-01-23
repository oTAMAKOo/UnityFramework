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
        public static void Save<T>(this T data) where T : ILocalData, new()
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

        private AesCryptoKey aesCryptoKey = null;

        private string fileDirectory = null;

        private Dictionary<Type, string> filePathDictionary = null;

        //----- property -----

        //----- method -----

        protected override void OnCreate()
        {
            filePathDictionary = new Dictionary<Type, string>();

            fileDirectory = Application.persistentDataPath + "/LocalData/";
        }

        public static void SetCryptoKey(AesCryptoKey cryptoKey)
        {
            Instance.aesCryptoKey = cryptoKey;
        }

        public void SetFileDirectory(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            fileDirectory = directory;
        }

        public static T Get<T>() where T : ILocalData, new()
        {
            var result = default(T);

            MessagePackValidater.ValidateAttribute(typeof(T));

            var filePath = Instance.GetLocalDataFilePath<T>();

            if (File.Exists(filePath))
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var bytes = new byte[fileStream.Length];

                    fileStream.Read(bytes, 0, bytes.Length);

                    var cryptoKey = Instance.GetCryptoKey();

                    bytes = bytes.Decrypt(cryptoKey);

                    var options = StandardResolverAllowPrivate.Options
                        .WithCompression(MessagePackCompression.Lz4BlockArray)
                        .WithResolver(UnityContractResolver.Instance);

                    result = MessagePackSerializer.Deserialize<T>(bytes, options);
                }
            }

            if (result == null)
            {
                result = new T();
            }

            return result;
        }

        public static void Save<T>(T data) where T : ILocalData, new()
        {
            MessagePackValidater.ValidateAttribute(typeof(T));

            var filePath = Instance.GetLocalDataFilePath<T>();

            using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                var options = StandardResolverAllowPrivate.Options
                    .WithCompression(MessagePackCompression.Lz4BlockArray)
                    .WithResolver(UnityContractResolver.Instance);

                var bytes = MessagePackSerializer.Serialize(data, options);

                var cryptoKey = Instance.GetCryptoKey();

                bytes = bytes.Encrypt(cryptoKey);

                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        private string GetLocalDataFilePath<T>() where T : ILocalData, new()
        {
            var filePath = filePathDictionary.GetValueOrDefault(typeof(T));

            if (!string.IsNullOrEmpty(filePath)){ return filePath; }
            
            if (!Directory.Exists(fileDirectory))
            {
                Directory.CreateDirectory(fileDirectory);
            }

            var fileName = string.Empty;

            var cryptoKey = GetCryptoKey();
            
            var type = typeof(T);

            var fileNameAttribute = type.GetCustomAttributes(typeof(FileNameAttribute), false)
                .Cast<FileNameAttribute>()
                .FirstOrDefault();

            if (fileNameAttribute != null)
            {
                fileName = fileNameAttribute.FileName.Encrypt(cryptoKey).GetHash();
            }
            else
            {
                throw new Exception(string.Format("FileNameAttribute is not set for this class.\n{0}", type.FullName));
            }

            filePath = fileDirectory + fileName;

            filePathDictionary.Add(typeof(T), filePath);

            return filePath;
        }

        private AesCryptoKey GetCryptoKey()
        {
            if (aesCryptoKey == null)
            {
                aesCryptoKey = new AesCryptoKey(DefaultKey, DefaultIv);
            }

            return aesCryptoKey;
        }

        public static void Delete<T>() where T : ILocalData, new()
        {
            var filePath = Instance.GetLocalDataFilePath<T>();

            if (!File.Exists(filePath)){ return; }

            File.Delete(filePath);
        }

        public static void DeleteAll()
        {
            var directory = Instance.fileDirectory;

            DirectoryUtility.Clean(directory);
        }
    }
}
