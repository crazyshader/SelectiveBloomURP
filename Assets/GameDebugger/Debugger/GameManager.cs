using UnityEngine;
using GameFramework;
using GameFramework.Singleton;
using GameFramework.Setting;
using GameFramework.Debugger;

namespace GameFramework
{
    /// <summary>
    /// 游戏管理器
    /// </summary>
    public sealed class GameManager : MonoSingleton<GameManager>
    {
        private const int DefaultDpi = 96;  // default windows dpi

        private SettingManager m_SettingManager = null;
        private DebuggerManager m_DebuggerManager = null;

        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Initialize()
        {
            GameManager.Instance.InitLogHelper();
        }

        private void InitLogHelper()
        {
            GameFrameworkLog.SetLogHelper(new DefaultLogHelper());
        }

        /// <summary>
        /// 组件初始化。
        /// </summary>
        private void InitManager()
        {
            if (m_SettingManager == null)
            {
                m_SettingManager = SettingManager.Instance;
                m_SettingManager.transform.SetParent(transform);
                m_SettingManager.Initilaize();

            }
            if (m_DebuggerManager == null)
            {
                m_DebuggerManager = DebuggerManager.Instance;
                m_DebuggerManager.transform.SetParent(transform);
            }

            Utility.Converter.ScreenDpi = Screen.dpi;
            if (Utility.Converter.ScreenDpi <= 0)
            {
                Utility.Converter.ScreenDpi = DefaultDpi;
            }
        }

        private void Awake()
        {
            GameManager.Instance.InitManager();
        }

        private void Start()
        {
        }

        /// <summary>
        /// 组件更新。
        /// </summary>
        private void Update()
        {
            var deltaTime = Time.deltaTime;
            var unscaledDeltaTime = Time.unscaledDeltaTime;
            m_SettingManager.OnUpdate(deltaTime, unscaledDeltaTime);
            m_DebuggerManager.OnUpdate(deltaTime, unscaledDeltaTime);
        }

        /// <summary>
        /// 组件销毁。
        /// </summary>
        protected override void OnDestroy()
        {
            if (m_SettingManager != null)
            {
                m_SettingManager.OnShutdown();
                m_SettingManager = null;
            }
            if (m_DebuggerManager != null)
            {
                m_DebuggerManager.OnShutdown();
                m_DebuggerManager = null;
            }

            ReferencePool.ClearAll();
            GameFrameworkLog.SetLogHelper(null);

            Destroy(gameObject);
            base.OnDestroy();
        }
    }
}
