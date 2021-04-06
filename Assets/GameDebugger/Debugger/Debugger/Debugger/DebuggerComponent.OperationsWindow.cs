//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2019 Jiang Yin. All rights reserved.
// Homepage: http://gameframework.cn/
// Feedback: mailto:jiangyin@gameframework.cn
//------------------------------------------------------------

using System;
using UnityEngine;

namespace GameFramework.Debugger
{
    internal sealed class OperationsWindow : ScrollableDebuggerWindowBase
    {
        protected override void OnDrawScrollableWindow()
        {
            GUILayout.Label("<b>Operations</b>");
            GUILayout.BeginVertical("box");
            {

                if (GUILayout.Button("Unload Unused Assets", GUILayout.Height(30f)))
                {
                    Resources.UnloadUnusedAssets();
                }

                if (GUILayout.Button("Unload Unused Assets and Garbage Collect", GUILayout.Height(30f)))
                {
                    Resources.UnloadUnusedAssets();
                    GC.Collect();
                }
            }
            GUILayout.EndVertical();
        }
    }
}
