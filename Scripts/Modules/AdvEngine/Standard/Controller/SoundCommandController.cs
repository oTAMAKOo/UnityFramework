﻿
#if ENABLE_MOONSHARP

using System;

namespace Modules.AdvKit.Standard
{
    public class SoundCommandController : CommandController
    {
        //----- params -----

        protected static readonly Type[] CommandList = new Type[]
        {
            // Bgm.
            typeof(SetupBgm), typeof(PlayBgm), typeof(StopBgm),

            // Se.
            typeof(SetupSe), typeof(PlaySe), typeof(StopSe),

            // Voice.
            typeof(SetupVoice), typeof(PlayVoice), typeof(StopVoice),

            // Ambience.
            typeof(SetupAmbience), typeof(PlayAmbience), typeof(StopAmbience),
        };

        //----- field -----

        //----- property -----

        public override string LuaName { get { return "sound"; } }

        protected override Type[] CommandTypes { get { return CommandList; } }

        //----- method -----        
    }    
}

#endif
