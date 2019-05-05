﻿
using UnityEngine;

namespace Extensions.Devkit
{
    public class ContentColorScope : GUI.Scope
    {
        //----- params -----

        //----- field -----

        private readonly Color originColor;

        //----- property -----

        //----- method -----

        public ContentColorScope(Color color)
        {
            originColor = GUI.backgroundColor;

            GUI.contentColor = color;
        }

        protected override void CloseScope()
        {
            GUI.contentColor = originColor;
        }
    }
}
