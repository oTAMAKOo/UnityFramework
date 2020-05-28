﻿
#if UNITY_EDITOR

using System;
using UnityEngine;
using System.Security.Cryptography;
using Extensions;

namespace Modules.GameText.Components
{
    public partial class GameTextSetter
    {
        //----- params -----

        private const string AESKey = "5k7DpFGc9A9iRaLkv2nCdMxCmjHFzxOX";
        private const string AESIv = "FiEA3x1fs8JhK9Tp";

        public const char DevelopmentMark = '#';

        //----- field -----

        #pragma warning disable 0414

        [SerializeField, HideInInspector]
        private string developmentText = null;

        #pragma warning restore 0414

        private static AesManaged aesManaged = null;

        //----- property -----

        //----- method -----

        private string GetDevelopmentText()
        {
            if (aesManaged == null)
            {
                aesManaged = AESExtension.CreateAesManaged(AESKey, AESIv);
            }

            if (string.IsNullOrEmpty(developmentText)) { return string.Empty; }

            return string.Format("{0}{1}", DevelopmentMark, developmentText.Decrypt(aesManaged));
        }

        private void SetDevelopmentText(string text)
        {
            if (aesManaged == null)
            {
                aesManaged = AESExtension.CreateAesManaged(AESKey, AESIv);
            }

            developmentText = string.IsNullOrEmpty(text) ? string.Empty : text.Encrypt(aesManaged);

            ImportText();
        }

        private void ApplyDevelopmentText()
        {
            if (Application.isPlaying) { return; }

            if (string.IsNullOrEmpty(developmentText)) { return; }

            if (!string.IsNullOrEmpty(textGuid)) { return; }

            var text = GetDevelopmentText();

            ApplyText(text);
        }

        private bool CleanDevelopmentText()
        {
            if (Application.isPlaying) { return false; }

            if (string.IsNullOrEmpty(developmentText)) { return false; }

            var text = GetDevelopmentText();

            var targetText = GetTargetText();

            if (targetText == text)
            {
                ApplyText(string.Empty);

                return true;
            }

            return false;
        }
    }
}

#endif
