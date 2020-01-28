﻿
using System;
using DG.Tweening;
using Extensions;
using MoonSharp.Interpreter;

namespace Modules.AdvKit.Standard
{
    public sealed class Scale : Command
    {
        //----- params -----

        //----- field -----

        //----- property -----

        public override string CommandName { get { return "Scale"; } }

        //----- method -----

        public override object GetCommandDelegate()
        {
            return (Func<string, float, float, string, bool, DynValue>)CommandFunction;
        }

        private DynValue CommandFunction(string identifier, float scale, float duration = 0, string easingType = null, bool wait = true)
        {
            var advEngine = AdvEngine.Instance;

            var advObject = advEngine.ObjectManager.Get<AdvObject>(identifier);

            if (advObject != null)
            {
                TweenCallback onComplete = () =>
                {
                    if (wait)
                    {
                        advEngine.Resume();
                    }
                };

                var ease = EnumExtensions.FindByName(easingType, Ease.Linear);

                var tweener = advObject.transform.DOScale(scale, duration)
                    .SetEase(ease)
                    .OnComplete(onComplete);

                advEngine.SetTweenTimeScale(tweener);
            }

            return wait ? YieldWait : DynValue.Nil;
        }
    }
}