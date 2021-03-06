﻿
using UnityEngine;
using System;
using System.Collections;
using System.Linq;
using UniRx;
using Extensions;
using Modules.StateMachine;

namespace Modules.Animation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class AnimationPlayer : MonoBehaviour, IStateMachineEventHandler
    {
        //----- params -----

        //----- field -----

        [SerializeField]
        private bool stopOnAwake = true;
        [SerializeField]
        private EndActionType endActionType = EndActionType.None;
        [SerializeField]
        private bool ignoreTimeScale = false;

        private int layer = -1;

        // 現在の状態.
        private State currentState = State.Stop;

        // ステート遷移待ちフラグ.
        private bool waitStateTransition = false;

        // 再生開始通知 (ステート遷移待ち完了後に呼び出される).
        private Subject<AnimationPlayer> onEnterAnimation = null;

        // 終了通知.
        private Subject<AnimationPlayer> onEndAnimation = null;

        // StateMachineイベント.
        private Subject<StateMachineEvent> onStateMachineEvent = null;

        // アニメーションイベント.
        private Subject<string> onAnimationEvent = null;

        // ポーズ中か.
        private bool isPause = false;

        // 再生速度(倍率).
        public float speedRate = 1f;

        // Pause前の再生速度.
        private float? pausedSpeed = null;

        [NonSerialized]
        private bool isInitialized = false;

        //----- property -----

        public string CurrentAnimationName { get; private set; }

        public Animator Animator { get; private set; }

        public AnimationClip[] Clips { get; private set; }

        public float DefaultSpeed { get; private set; }

        public bool IsPlaying
        {
            get { return State != State.Stop; }
        }

        public State State
        {
            get
            {
                return isPause ? State.Pause : currentState;
            }
        }

        public EndActionType EndActionType
        {
            get { return endActionType; }

            set { endActionType = value; }
        }

        public bool Pause
        {
            get { return isPause; }

            set
            {
                if (!isInitialized) { return; }

                SetPauseState(value);
            }
        }

        public float SpeedRate
        {
            get { return speedRate; }

            set
            {
                speedRate = value;
                ApplySpeedRate();
            }
        }

        public bool IsInitialized { get { return isInitialized; } }

        //----- method -----

        private void Initialize()
        {
            var animator = UnityUtility.GetOrAddComponent<Animator>(gameObject);

            if (animator == null) { return; }

            if (isInitialized) { return; }

            var animatorController = animator.runtimeAnimatorController;

            Animator = animator;
            Clips = animatorController != null ? animator.runtimeAnimatorController.animationClips : new AnimationClip[0];
            DefaultSpeed = Animator.speed;

            if (stopOnAwake)
            {
                Stop();
            }

            Refresh();

            isInitialized = true;
        }

        void OnEnable()
        {
            Initialize();
        }

        void OnDisable()
        {
            Stop();
        }

        public IObservable<Unit> Play(string animationName, int layer = -1, float normalizedTime = float.NegativeInfinity, bool immediate = true)
        {
            if (string.IsNullOrEmpty(animationName)) { return Observable.ReturnUnit(); }

            UnityUtility.SetActive(gameObject, true);

            // ヒエラルキー上で非アクティブでないか.
            if (!UnityUtility.IsActiveInHierarchy(gameObject))
            {
                Debug.LogErrorFormat("Animation can't play not active in hierarchy.\n{0}", gameObject.transform.name);

                return Observable.ReturnUnit();
            }

            this.layer = layer;

            CurrentAnimationName = animationName;

            var hash = Animator.StringToHash(CurrentAnimationName);

            if (Animator.HasState(GetCurrentLayerIndex(), hash))
            {
                PlayAnimator(hash, layer, normalizedTime);

                // 指定アニメーションへ遷移待ち.
                // FromMicroCoroutineは次のフレームから始まるので即時遷移はこのタイミングで実行する.
                if (immediate)
                {
                    if (!IsCurrentState(CurrentAnimationName, GetCurrentLayerIndex()))
                    {
                        WaitTransitionStateImmediate();
                    }
                }

                return Observable.FromMicroCoroutine(() => PlayInternal(hash, layer, normalizedTime, immediate));
            }

            CurrentAnimationName = null;

            Debug.LogErrorFormat("Animation State Not found. {0}", animationName);

            return Observable.ReturnUnit();
        }

        private void PlayAnimator(int hash, int layer, float normalizedTime)
        {
            // リセット.
            Refresh();

            // 再生速度設定.
            ApplySpeedRate();

            // 再生.
            currentState = State.Play;
            Animator.enabled = true;
            Animator.Play(hash, layer, normalizedTime);
        }

        private bool IsCurrentState(string animationName, int layer)
        {
            var stateInfo = Animator.GetCurrentAnimatorStateInfo(layer);

            return stateInfo.IsName(animationName);
        }

        private IEnumerator PlayInternal(int hash, int layer, float normalizedTime, bool immediate)
        {
            while (true)
            {
                if (!UnityUtility.IsActiveInHierarchy(gameObject)) { break; }

                // 指定アニメーションへ遷移待ち.
                if (!IsCurrentState(CurrentAnimationName, GetCurrentLayerIndex()))
                {
                    var waitTransitionStateYield = Observable.FromMicroCoroutine(() => WaitTransitionState()).ToYieldInstruction();

                    while (!waitTransitionStateYield.IsDone)
                    {
                        yield return null;
                    }
                }

                // アニメーション開始通知.
                if (onEnterAnimation != null)
                {
                    onEnterAnimation.OnNext(this);
                }

                // アニメーションの終了待ち.
                var waitForEndYield = Observable.FromMicroCoroutine(() => WaitForEndOfAnimation()).ToYieldInstruction();

                while (!waitForEndYield.IsDone)
                {
                    yield return null;
                }

                if (endActionType != EndActionType.Loop) { break; }

                if (State == State.Stop) { yield break; }

                // ループ再生の場合は再度再生を行う.
                PlayAnimator(hash, layer, normalizedTime);
            }

            EndAction();
        }

        // 指定アニメーションへ遷移待ち.
        private IEnumerator WaitTransitionState()
        {
            // ステートの遷移待ち.
            while (true)
            {
                if (UnityUtility.IsNull(this)) { yield break; }

                if (UnityUtility.IsNull(gameObject)) { yield break; }

                if (!UnityUtility.IsActiveInHierarchy(gameObject)) { break; }

                if (IsCurrentState(CurrentAnimationName, GetCurrentLayerIndex())) { break; }

                yield return null;
            }
        }

        // 指定アニメーションへ遷移待ち.
        private void WaitTransitionStateImmediate()
        {
            while (true)
            {
                if (UnityUtility.IsNull(this)) { return; }

                if (UnityUtility.IsNull(gameObject)) { return; }

                if (!UnityUtility.IsActiveInHierarchy(gameObject)) { break; }
                
                if (IsCurrentState(CurrentAnimationName, GetCurrentLayerIndex())) { break; }

                Animator.Update(0);
            }
        }

        // アニメーションの終了待ち.
        private IEnumerator WaitForEndOfAnimation()
        {
            if (!isInitialized) { yield break; }

            waitStateTransition = true;

            while (true)
            {
                if (State == State.Stop) { yield break; }

                if (State == State.Play)
                {
                    // 終了監視.
                    if (!IsAlive()) { break; }
                }

                yield return null;
            }
        }

        public void Stop()
        {
            if (UnityUtility.IsNull(Animator)) { return; }

            if (isInitialized && State != State.Stop)
            {
                // Animator停止.
                foreach (var clip in Clips)
                {
                    clip.SampleAnimation(Animator.gameObject, clip.length);
                }

                Refresh();
            }

            Animator.enabled = false;
        }

        private void ApplySpeedRate()
        {
            Animator.speed *= speedRate;
        }

        private void Refresh()
        {
            if (!isInitialized) { return; }

            currentState = State.Stop;

            isPause = false;
            pausedSpeed = null;

            // TimeScaleの影響を受けるか.
            Animator.updateMode = ignoreTimeScale ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;

            if (UnityUtility.IsActiveInHierarchy(gameObject))
            {
                // 実行中にSampleAnimationしても表示された物が更新されない為、一旦再生して表示物をリセットする.
                var currentStateInfo = Animator.GetCurrentAnimatorStateInfo(0);

                Animator.Play(currentStateInfo.fullPathHash, -1, 0.0f);
            }

            Animator.speed = DefaultSpeed;            
        }

        private void EndAction()
        {
            switch (endActionType)
            {
                case EndActionType.None:
                    currentState = State.Stop;
                    break;

                case EndActionType.Deactivate:
                    if (Application.isPlaying)
                    {
                        UnityUtility.SetActive(gameObject, false);
                    }

                    currentState = State.Stop;
                    break;

                case EndActionType.Destroy:
                    if (Application.isPlaying)
                    {
                        UnityUtility.SafeDelete(gameObject);
                    }

                    currentState = State.Stop;
                    break;

                case EndActionType.Loop:
                    break;
            }

            if (onEndAnimation != null)
            {
                onEndAnimation.OnNext(this);
            }
        }

        /// <summary>
        /// 配下のAnimatorに対してパラメータをセット.
        /// </summary>
        /// <param name="parameters"></param>
        public void SetParameters(params StateMachineParameter[] parameters)
        {
            DoLazyFunc(animator =>
                {
                    foreach (var parameter in parameters)
                    {
                        parameter.SetParameter(animator);
                    }
                    return Unit.Default;
                })
            .Subscribe()
            .AddTo(this);
        }

        /// <summary>
        /// 指定した型の<see cref="StateMachineBehaviour"/>をすべて取得.
        /// <see cref="Animator"/>の初期化が完了するまで取得出来ないため、非同期で取得.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IObservable<T[]> GetStateMachineBehavioursAsync<T>() where T : StateMachineBehaviour
        {
            return DoLazyFunc(x => x.GetBehaviours<T>())
                .SelectMany(x => x)
                .ToArray();
        }

        private void SetPauseState(bool pause)
        {
            if (isPause == pause) { return; }

            // 中断.
            if (pause)
            {
                pausedSpeed = Animator.speed;
                Animator.speed = 0f;
            }
            // 再開.
            else
            {
                Animator.speed = pausedSpeed.HasValue ? pausedSpeed.Value : 1f;
                pausedSpeed = null;
            }

            isPause = pause;
        }

        private void ResetAnimatorSpeed()
        {
            Animator.speed = DefaultSpeed;
        }

        private bool IsAlive()
        {
            if (!UnityUtility.IsActiveInHierarchy(gameObject)) { return false; }

            var layerIndex = GetCurrentLayerIndex();

            var stateInfo = Animator.GetCurrentAnimatorStateInfo(layerIndex);

            // 指定ステートへ遷移待ち.
            if (waitStateTransition)
            {
                if (!stateInfo.IsName(CurrentAnimationName) || Animator.IsInTransition(layerIndex))
                {
                    return true;
                }

                waitStateTransition = false;
            }

            if (!stateInfo.IsName(CurrentAnimationName)) { return false; }

            if (1 < stateInfo.normalizedTime && !Animator.IsInTransition(layerIndex)) { return false; }

            // 再生中のアニメーションクリップ.
            var clipInfo = Animator.GetCurrentAnimatorClipInfo(layerIndex);

            if (clipInfo.Any())
            {
                var currentClipInfo = clipInfo[0];

                if (currentClipInfo.clip.length == 0) { return false; }
            }

            return true;
        }

        private int GetCurrentLayerIndex()
        {
            return layer == -1 ? 0 : layer;
        }

        private IObservable<TResult> DoLazyFunc<TResult>(Func<Animator, TResult> func)
        {
            // 初期化済の場合は即座に実行.
            if (Animator.isInitialized)
            {
                return Observable.Return(func(Animator));
            }

            // 初期化済でない場合は初期化完了を待つ.
            // これは非アクティブでも回って欲しいのであえて Observable.EveryUpdateを使う.
            return Observable.EveryUpdate()
                .SkipWhile(_ => !Animator.isInitialized)
                .Take(1)
                .Select(_ => func(Animator));
        }

        public IObservable<AnimationPlayer> OnEnterAnimationAsObservable()
        {
            return onEnterAnimation ?? (onEnterAnimation = new Subject<AnimationPlayer>());
        }

        public IObservable<AnimationPlayer> OnEndAnimationAsObservable()
        {
            return onEndAnimation ?? (onEndAnimation = new Subject<AnimationPlayer>());
        }

        #region StateMachine Event

        public void StateMachineEvent(StateMachineEvent stateMachineEvent)
        {
            if (onStateMachineEvent != null)
            {
                onStateMachineEvent.OnNext(stateMachineEvent);
            }
        }

        public IObservable<StateMachineEvent> OnStateMachineEventAsObservable()
        {
            return onStateMachineEvent ?? (onStateMachineEvent = new Subject<StateMachineEvent>());
        }

        #endregion

        #region Animation Event

        public void Event(string value)
        {
            if (onAnimationEvent != null)
            {
                onAnimationEvent.OnNext(value);
            }
        }

        public IObservable<string> OnAnimationEventAsObservable()
        {
            return onAnimationEvent ?? (onAnimationEvent = new Subject<string>());
        }

        #endregion        
    }
}
