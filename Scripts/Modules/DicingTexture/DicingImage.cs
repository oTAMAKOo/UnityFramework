﻿
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using UniRx;

namespace Modules.Dicing
{
    [ExecuteInEditMode, RequireComponent(typeof(RectTransform))]
    public sealed class DicingImage : MaskableGraphic, ICanvasRaycastFilter
    {
        //----- params -----

        private sealed class BlockInfo
        {
            public Rect Rect { get; set; }
            public DicingBlockData BlockData { get; set; }
        }

        //----- field -----

        [SerializeField]
        private DicingTexture dicingTexture = null;
        [SerializeField]
        private bool crossFade = false;
        [SerializeField]
        private float crossFadeTime = 0.2f;

        // 現在のテクスチャ.
        private DicingSourceData sourceTexture = null;
        // 当たり判定用のブロック情報.
        private List<BlockInfo> blockInfos = new List<BlockInfo>();
        // 当たり判定用座標.
        private Vector2 rayCastPosition = Vector2.zero;
        // 当たり判定カメラ.
        private Camera eventCamera = null;
        // 現在のテクスチャ名.
        private string currentTextureName = null;

        // クロスフェード.
        private string crossFadeTextureName = null;
        private Color crossFadeColor = Color.clear;
        private IDisposable fadeDisposable = null;

        //----- property -----

        public override Texture mainTexture
        {
            get
            {
                if (dicingTexture == null)
                {
                    if (material != null && material.mainTexture != null)
                    {
                        return material.mainTexture;
                    }
                    return s_WhiteTexture;
                }

                return dicingTexture.Texture;
            }
        }

        public DicingTexture DicingTexture
        {
            get { return dicingTexture; }
            set
            {
                if (dicingTexture != value)
                {
                    currentTextureName = null;
                }

                dicingTexture = value;
            }
        }

        public DicingSourceData Current
        {
            get { return sourceTexture; }
        }

        public bool RaycastTarget
        {
            get { return raycastTarget; }
            set { raycastTarget = value; }
        }

        public Color Color
        {
            get { return color; }
            set
            {
                color = value;
                SetAllDirty();
            }
        }

        public Material Material
        {
            get { return material; }
            set
            {
                material = value;
                SetAllDirty();
            }
        }

        public bool CrossFade
        {
            get { return crossFade; }
            set { crossFade = value; }
        }

        public float CrossFadeTime
        {
            get { return crossFadeTime; }
            set { crossFadeTime = value; }
        }

        /// <summary>
        /// パターンテクスチャを設定.
        /// </summary>
        public string PatternName
        {
            get { return sourceTexture != null ? currentTextureName : null; }

            set { SetPatternName(value); }
        }

        //----- method -----

        public override void SetNativeSize()
        {
            if (dicingTexture == null) { return; }

            if (sourceTexture == null) { return; }

            rectTransform.anchorMax = rectTransform.anchorMin;

            rectTransform.sizeDelta = new Vector2(sourceTexture.width, sourceTexture.height);

            SetAllDirty();
        }

        public string[] GetAllPatternName()
        {
            if (dicingTexture == null) { return new string[0]; }

            var sourceTextures = dicingTexture.GetAllDicingSource();
            var patternNames = sourceTextures.Select(x => x.textureName).ToArray();

            return patternNames;
        }

        public DicingSourceData GetPatternData(string patternName)
        {
            if (string.IsNullOrEmpty(patternName)) { return null; }

            if (dicingTexture == null) { return null; }

            return dicingTexture.GetDicingSource(patternName);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            blockInfos.Clear();

            vh.Clear();

            if (dicingTexture == null) { return; }

            if (sourceTexture == null) { return; }

            if (string.IsNullOrEmpty(currentTextureName)) { return; }

            var rect = GetPixelAdjustedRect();

            if (rect.width <= 0 || rect.height <= 0) { return; }

            var pos = new Vector3(rect.x, rect.y);

            var textureWidth = (float)dicingTexture.Texture.width;
            var textureHeight = (float)dicingTexture.Texture.height;

            for (var by = 0; by < sourceTexture.yblock; by++)
            {
                var vertexSizeY = 0f;

                for (var bx = 0; bx < sourceTexture.xblock; bx++)
                {
                    var block = dicingTexture.GetBlockData(currentTextureName, bx, by);

                    if (block == null) { continue; }

                    var vertexSizeX = rect.width * ((float)block.w / sourceTexture.width);

                    vertexSizeY = rect.height * ((float)block.h / sourceTexture.height);

                    // 左上
                    var lt = UIVertex.simpleVert;
                    lt.position = new Vector3(pos.x, pos.y, 0);
                    lt.uv0 = new Vector2(block.x / textureWidth, block.y / textureHeight);
                    lt.color = color;

                    // 右上
                    var rt = UIVertex.simpleVert;
                    rt.position = new Vector3(pos.x + vertexSizeX, pos.y, 0);
                    rt.uv0 = new Vector2((block.x + block.w) / textureWidth, block.y / textureHeight);
                    rt.color = color;

                    // 右下
                    var rb = UIVertex.simpleVert;
                    rb.position = new Vector3(pos.x + vertexSizeX, pos.y + vertexSizeY, 0);
                    rb.uv0 = new Vector2((block.x + block.w) / textureWidth, (block.y + block.h) / textureHeight);
                    rb.color = color;

                    // 左下
                    var lb = UIVertex.simpleVert;
                    lb.position = new Vector3(pos.x, pos.y + vertexSizeY, 0);
                    lb.uv0 = new Vector2(block.x / textureWidth, (block.y + block.h) / textureHeight);
                    lb.color = color;

                    vh.AddUIVertexQuad(new UIVertex[] { lb, rb, rt, lt });

                    // クロスフェード.
                    if (CrossFade && !string.IsNullOrEmpty(crossFadeTextureName))
                    {
                        DrawCrossFade(vh, bx, by, pos, textureWidth, textureHeight, vertexSizeX, vertexSizeY);
                    }

                    pos.x += vertexSizeX;

                    // ブロック情報更新.
                    var blockInfo = new BlockInfo()
                    {
                        Rect = new Rect(lt.position, new Vector2(vertexSizeX, vertexSizeY)),
                        BlockData = block,
                    };

                    blockInfos.Add(blockInfo);
                }

                pos.x = rect.x;
                pos.y += vertexSizeY;
            }
        }

        // ヒットテスト.
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            this.eventCamera = eventCamera;
            this.rayCastPosition = sp;

            if (!raycastTarget) { return false; }

            var hitLocation = GetHitPosition();

            return hitLocation != null;
        }

        public Vector2? GetHitPosition()
        {
            if (eventCamera == null) { return null; }

            var localPosition = Vector2.zero;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, rayCastPosition, eventCamera, out localPosition);

            if (!GetPixelAdjustedRect().Contains(localPosition)) { return null; }

            if (blockInfos.IsEmpty()) { return null; }

            foreach (var blockInfo in blockInfos)
            {
                if (blockInfo.Rect.Contains(localPosition))
                {
                    var px = localPosition.x - blockInfo.Rect.x;
                    var py = localPosition.y - blockInfo.Rect.y;

                    if (blockInfo.BlockData.HasAlpha((int)px, (int)py))
                    {
                        return localPosition;
                    }
                }
            }

            return null;
        }

        private bool CheckCrossFade(string textureName)
        {
            if (!Application.isPlaying) { return false; }

            if (!crossFade) { return false; }

            if (sourceTexture == null) { return false; }

            var dicingSource = dicingTexture.GetDicingSource(textureName);

            if (dicingSource == null) { return false; }

            if (sourceTexture.width != dicingSource.width || sourceTexture.height != dicingSource.height)
            {
                return false;
            }

            return true;
        }

        private void StartCrossFade()
        {
            StopCrossFade();

            crossFadeTextureName = currentTextureName;

            fadeDisposable = Observable.FromCoroutine(() => Fade(crossFadeTime))
                .Subscribe(_ => StopCrossFade())
                .AddTo(this);
        }

        public void StopCrossFade()
        {
            if (fadeDisposable != null)
            {
                fadeDisposable.Dispose();
                fadeDisposable = null;
            }

            crossFadeTextureName = null;

            SetAllDirty();
        }

        private void DrawCrossFade(VertexHelper vh, int bx, int by, Vector3 pos, float textureWidth, float textureHeight, float vertexSizeX, float vertexSizeY)
        {
            var block = dicingTexture.GetBlockData(currentTextureName, bx, by);

            var crossFadeBlock = dicingTexture.GetBlockData(crossFadeTextureName, bx, by);

            if (block == null || crossFadeBlock == null) { return; }

            // 同じピクセル情報なら描画しない.
            if (block.blockId == crossFadeBlock.blockId) { return; }

            // 左上
            var lt = UIVertex.simpleVert;
            lt.position = new Vector3(pos.x, pos.y, 0f);
            lt.uv0 = new Vector2(crossFadeBlock.x / textureWidth, crossFadeBlock.y / textureHeight);
            lt.color = crossFadeColor;

            // 右上
            var rt = UIVertex.simpleVert;
            rt.position = new Vector3(pos.x + vertexSizeX, pos.y, 0f);
            rt.uv0 = new Vector2((crossFadeBlock.x + crossFadeBlock.w) / textureWidth, crossFadeBlock.y / textureHeight);
            rt.color = crossFadeColor;

            // 右下
            var rb = UIVertex.simpleVert;
            rb.position = new Vector3(pos.x + vertexSizeX, pos.y + vertexSizeY, 0f);
            rb.uv0 = new Vector2((crossFadeBlock.x + crossFadeBlock.w) / textureWidth, (crossFadeBlock.y + crossFadeBlock.h) / textureHeight);
            rb.color = crossFadeColor;

            // 左下
            var lb = UIVertex.simpleVert;
            lb.position = new Vector3(pos.x, pos.y + vertexSizeY, 0f);
            lb.uv0 = new Vector2(crossFadeBlock.x / textureWidth, (crossFadeBlock.y + crossFadeBlock.h) / textureHeight);
            lb.color = crossFadeColor;

            vh.AddUIVertexQuad(new UIVertex[] { lb, rb, rt, lt });
        }

        private IEnumerator Fade(float time)
        {
            var current = 0f;

            crossFadeColor = color;

            while (current < time)
            {
                current += Time.deltaTime;

                var c = this.color;

                c.a *= 1f - Mathf.Clamp01(current / time);

                crossFadeColor = c;

                SetAllDirty();

                yield return null;
            }

            crossFadeTextureName = null;

            SetAllDirty();
        }

        private void SetPatternName(string patternName)
        {
            if (dicingTexture == null) { return; }

            var textureName = Path.GetFileNameWithoutExtension(patternName);

            if (patternName != null)
            {
                if (sourceTexture == null || currentTextureName != textureName)
                {
                    if (CheckCrossFade(textureName))
                    {
                        StartCrossFade();
                    }

                    sourceTexture = dicingTexture.GetDicingSource(textureName);
                    currentTextureName = textureName;

                    if (sourceTexture == null)
                    {
                        Debug.LogErrorFormat("This texture is not found. {0}", textureName);
                    }

                    SetAllDirty();
                }
            }
            else
            {
                sourceTexture = null;
                currentTextureName = null;
            }
        }
    }
}
