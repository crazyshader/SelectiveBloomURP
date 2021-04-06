//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2019 Jiang Yin. All rights reserved.
// Homepage: http://gameframework.cn/
// Feedback: mailto:jiangyin@gameframework.cn
//------------------------------------------------------------

using UnityEngine;
using GameFramework.Setting;

namespace GameFramework.Debugger
{
    internal sealed class SettingsWindow : ScrollableDebuggerWindowBase
    {
        private DebuggerManager m_DebuggerManager = null;
        private SettingManager m_SettingManager = null;
        private float m_LastIconX = 0f;
        private float m_LastIconY = 0f;
        private float m_LastWindowX = 0f;
        private float m_LastWindowY = 0f;
        private float m_LastWindowWidth = 0f;
        private float m_LastWindowHeight = 0f;
        private float m_LastWindowScale = 0f;

        public override void Initialize(params object[] args)
        {
            m_DebuggerManager = DebuggerManager.Instance;
            if (m_DebuggerManager == null)
            {
                Log.Fatal("Debugger component is invalid.");
                return;
            }

            m_SettingManager = SettingManager.Instance;
            if (m_SettingManager == null)
            {
                Log.Fatal("Setting component is invalid.");
                return;
            }

            m_LastIconX = m_SettingManager.GetFloat("Debugger.Icon.X", DebuggerManager.DefaultIconRect.x);
            m_LastIconY = m_SettingManager.GetFloat("Debugger.Icon.Y", DebuggerManager.DefaultIconRect.y);
            m_LastWindowX = m_SettingManager.GetFloat("Debugger.Window.X", DebuggerManager.DefaultWindowRect.x);
            m_LastWindowY = m_SettingManager.GetFloat("Debugger.Window.Y", DebuggerManager.DefaultWindowRect.y);
            m_LastWindowWidth = m_SettingManager.GetFloat("Debugger.Window.Width", DebuggerManager.DefaultWindowRect.width);
            m_LastWindowHeight = m_SettingManager.GetFloat("Debugger.Window.Height", DebuggerManager.DefaultWindowRect.height);
            m_DebuggerManager.WindowScale = m_LastWindowScale = m_SettingManager.GetFloat("Debugger.Window.Scale", DebuggerManager.DefaultWindowScale);
            m_DebuggerManager.IconRect = new Rect(m_LastIconX, m_LastIconY, DebuggerManager.DefaultIconRect.width, DebuggerManager.DefaultIconRect.height);
            m_DebuggerManager.WindowRect = new Rect(m_LastWindowX, m_LastWindowY, m_LastWindowWidth, m_LastWindowHeight);
        }

        public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            if (m_LastIconX != m_DebuggerManager.IconRect.x)
            {
                m_LastIconX = m_DebuggerManager.IconRect.x;
                m_SettingManager.SetFloat("Debugger.Icon.X", m_DebuggerManager.IconRect.x);
            }

            if (m_LastIconY != m_DebuggerManager.IconRect.y)
            {
                m_LastIconY = m_DebuggerManager.IconRect.y;
                m_SettingManager.SetFloat("Debugger.Icon.Y", m_DebuggerManager.IconRect.y);
            }

            if (m_LastWindowX != m_DebuggerManager.WindowRect.x)
            {
                m_LastWindowX = m_DebuggerManager.WindowRect.x;
                m_SettingManager.SetFloat("Debugger.Window.X", m_DebuggerManager.WindowRect.x);
            }

            if (m_LastWindowY != m_DebuggerManager.WindowRect.y)
            {
                m_LastWindowY = m_DebuggerManager.WindowRect.y;
                m_SettingManager.SetFloat("Debugger.Window.Y", m_DebuggerManager.WindowRect.y);
            }

            if (m_LastWindowWidth != m_DebuggerManager.WindowRect.width)
            {
                m_LastWindowWidth = m_DebuggerManager.WindowRect.width;
                m_SettingManager.SetFloat("Debugger.Window.Width", m_DebuggerManager.WindowRect.width);
            }

            if (m_LastWindowHeight != m_DebuggerManager.WindowRect.height)
            {
                m_LastWindowHeight = m_DebuggerManager.WindowRect.height;
                m_SettingManager.SetFloat("Debugger.Window.Height", m_DebuggerManager.WindowRect.height);
            }

            if (m_LastWindowScale != m_DebuggerManager.WindowScale)
            {
                m_LastWindowScale = m_DebuggerManager.WindowScale;
                m_SettingManager.SetFloat("Debugger.Window.Scale", m_DebuggerManager.WindowScale);
            }
        }

        protected override void OnDrawScrollableWindow()
        {
            GUILayout.Label("<b>Window Settings</b>");
            GUILayout.BeginVertical("box");
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Position:", GUILayout.Width(60f));
                    GUILayout.Label("Drag window caption to move position.");
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    float width = m_DebuggerManager.WindowRect.width;
                    GUILayout.Label("Width:", GUILayout.Width(60f));
                    if (GUILayout.RepeatButton("-", GUILayout.Width(30f)))
                    {
                        width--;
                    }
                    width = GUILayout.HorizontalSlider(width, 100f, Screen.width - 20f);
                    if (GUILayout.RepeatButton("+", GUILayout.Width(30f)))
                    {
                        width++;
                    }
                    width = Mathf.Clamp(width, 100f, Screen.width - 20f);
                    if (width != m_DebuggerManager.WindowRect.width)
                    {
                        m_DebuggerManager.WindowRect = new Rect(m_DebuggerManager.WindowRect.x, m_DebuggerManager.WindowRect.y, width, m_DebuggerManager.WindowRect.height);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    float height = m_DebuggerManager.WindowRect.height;
                    GUILayout.Label("Height:", GUILayout.Width(60f));
                    if (GUILayout.RepeatButton("-", GUILayout.Width(30f)))
                    {
                        height--;
                    }
                    height = GUILayout.HorizontalSlider(height, 100f, Screen.height - 20f);
                    if (GUILayout.RepeatButton("+", GUILayout.Width(30f)))
                    {
                        height++;
                    }
                    height = Mathf.Clamp(height, 100f, Screen.height - 20f);
                    if (height != m_DebuggerManager.WindowRect.height)
                    {
                        m_DebuggerManager.WindowRect = new Rect(m_DebuggerManager.WindowRect.x, m_DebuggerManager.WindowRect.y, m_DebuggerManager.WindowRect.width, height);
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    float scale = m_DebuggerManager.WindowScale;
                    GUILayout.Label("Scale:", GUILayout.Width(60f));
                    if (GUILayout.RepeatButton("-", GUILayout.Width(30f)))
                    {
                        scale -= 0.01f;
                    }
                    scale = GUILayout.HorizontalSlider(scale, 0.5f, 4f);
                    if (GUILayout.RepeatButton("+", GUILayout.Width(30f)))
                    {
                        scale += 0.01f;
                    }
                    scale = Mathf.Clamp(scale, 0.5f, 4f);
                    if (scale != m_DebuggerManager.WindowScale)
                    {
                        m_DebuggerManager.WindowScale = scale;
                    }
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("0.5x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 0.5f;
                    }
                    if (GUILayout.Button("1.0x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 1f;
                    }
                    if (GUILayout.Button("1.5x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 1.5f;
                    }
                    if (GUILayout.Button("2.0x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 2f;
                    }
                    if (GUILayout.Button("2.5x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 2.5f;
                    }
                    if (GUILayout.Button("3.0x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 3f;
                    }
                    if (GUILayout.Button("3.5x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 3.5f;
                    }
                    if (GUILayout.Button("4.0x", GUILayout.Height(60f)))
                    {
                        m_DebuggerManager.WindowScale = 4f;
                    }
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Reset Window Settings", GUILayout.Height(30f)))
                {
                    m_DebuggerManager.IconRect = DebuggerManager.DefaultIconRect;
                    m_DebuggerManager.WindowRect = DebuggerManager.DefaultWindowRect;
                    m_DebuggerManager.WindowScale = DebuggerManager.DefaultWindowScale;
                }
            }
            GUILayout.EndVertical();
        }
    }
}
