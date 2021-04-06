using UnityEngine;
using GameFramework.Debugger;

public class GameDebugger : MonoBehaviour
{
    private enum DebugType
    {
        None,
        Inspector,
        Performance,
        Debugger
    }

    public GameObject DebugSwitcher;
	public GameObject RuntimeInspector;
	public GameObject RuntimePerformance;

    private float m_LastTime = 0.0f;
    private uint m_CurrentClickCount = 0;
    private const uint MAX_CLICK_COUNT = 3;
    private const float MAX_CLICK_INTERVAL = 0.55f;

    private Vector3 m_SwitcherLocalScale = Vector3.one;
    private RectTransform m_SwitcherTransform = null;

    private DebugType m_DebugType = DebugType.None;

#if UI_NGUI
    private static UICamera m_UICamera = null;
    private int m_UIEventMask = 0;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Initialize()
    {
//#if DEVELOPMENT_BUILD
        var gameDebugger = Instantiate(Resources.Load("GameDebugger"));
        gameDebugger.name = "GameDebugger";
        GameFramework.GameManager.Initialize();
        Debug.Log($"Enable Game Debugger!");
//#endif
    }

    private void Awake()
    {
        m_SwitcherTransform = DebugSwitcher?.GetComponent(typeof(RectTransform)) as RectTransform;
        m_SwitcherLocalScale = m_SwitcherTransform.localScale;
        m_SwitcherTransform.localScale = Vector3.zero;

        DontDestroyOnLoad(this);
    }

    private void Update()
    {
#if UI_NGUI
        if (m_UICamera == null)
        {
            m_UICamera = UICamera.first;
            if (m_UICamera == null)
            {
                return;
            }
            m_UIEventMask = m_UICamera.eventReceiverMask;
        }
#endif

        if (m_DebugType == DebugType.Debugger)
        {
            if (DebuggerManager.Instance.ShowFullWindow)
            {
#if UI_NGUI
                m_UICamera.eventReceiverMask = 0;
#endif
                m_SwitcherTransform.localScale = Vector3.zero;
            }
            else
            {
#if UI_NGUI
                m_UICamera.eventReceiverMask = m_UIEventMask;
#endif
                m_SwitcherTransform.localScale = m_SwitcherLocalScale;
            }
        }
    }

    public void EnableDebugger()
    {
        if (m_SwitcherTransform.localScale == m_SwitcherLocalScale)
        {
            return;
        }

        var timeDelta = Time.unscaledTime - m_LastTime;
        m_LastTime = Time.unscaledTime;
        if (m_CurrentClickCount != 0 && timeDelta > MAX_CLICK_INTERVAL)
        {
            m_CurrentClickCount = 0;
            return;
        }

        if (++m_CurrentClickCount >= MAX_CLICK_COUNT)
        {
            m_CurrentClickCount = 0;
            DebugSwitcher?.SetActive(true);
            m_SwitcherTransform.localScale = m_SwitcherLocalScale;
        }
    }

    public void ChangeMode(int value)
    {
        m_DebugType = (DebugType)value;
        switch (m_DebugType)
		{
			case DebugType.None:
                m_SwitcherTransform.localScale = Vector3.zero;
                RuntimeInspector?.SetActive(false);
                RuntimePerformance?.SetActive(false);
                DebuggerManager.Instance.ActiveWindow = false;
                break;
			case DebugType.Inspector:
                RuntimeInspector?.SetActive(true);
                RuntimePerformance?.SetActive(false);
                DebuggerManager.Instance.ActiveWindow = false;
                break;
			case DebugType.Performance:
                RuntimeInspector?.SetActive(false);
                RuntimePerformance?.SetActive(true);
                DebuggerManager.Instance.ActiveWindow = false;
                break;
            case DebugType.Debugger:
                RuntimeInspector?.SetActive(false);
                RuntimePerformance?.SetActive(false);
                DebuggerManager.Instance.ActiveWindow = true;
                break;
		}

#if UI_NGUI
        if (m_UICamera != null)
        {
            if (m_DebugType == DebugType.Inspector)
            {
                m_UICamera.eventReceiverMask = 0;
            }
            else
            {
                m_UICamera.eventReceiverMask = m_UIEventMask;
            }
        }
#endif
    }
}
