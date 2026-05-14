using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

public class PitchTrigger : MonoBehaviour
{
    [Header("动画配置")]
    [SerializeField] private Animator pitcherAnimator;
    [SerializeField] private string pitchTriggerParam = "OnPitch";
    [SerializeField] private float animationDuration = 1.5f; // 动画持续时间

    [Header("VR Input System 配置（与AutoPitcher一致）")]
    public InputActionAsset inputActions;
    private InputActionMap _pitchActionMap;
    private InputAction _vrPitchAction;

    [Header("调试输入")]
    private InputAction keyboardPitchAction; // 保持原有空格实现

    [Header("VR流程控制")]
    private bool _isVRMode = true;

    [Header("事件")]
    public UnityEvent OnPitchTriggered;

    private bool canPitch = true;
    private Coroutine resetCoroutine;

    private void Awake()
    {
        _isVRMode = UnityEngine.XR.XRSettings.isDeviceActive;
        InitVRInputSystem();
    }

    private void Start()
    {
        if (pitcherAnimator == null)
            pitcherAnimator = GetComponent<Animator>();

        // 保持原有的键盘空格输入
        keyboardPitchAction = new InputAction("KeyboardPitch", binding: "<Keyboard>/space");
        keyboardPitchAction.performed += OnKeyboardPitchPerformed;
        keyboardPitchAction.Enable();

        if (_isVRMode)
        {
            Debug.Log("[PitchTrigger] VR模式已激活");
        }
    }

    #region VR输入系统初始化（与AutoPitcher一致）
    private void InitVRInputSystem()
    {
        if (inputActions == null)
        {
            Debug.LogWarning("[PitchTrigger] 未拖入Input Action资产，VR手柄输入将不可用");
            return;
        }

        _pitchActionMap = inputActions.FindActionMap("PitchControls");
        if (_pitchActionMap == null)
        {
            Debug.LogWarning("[PitchTrigger] 未找到PitchControls Action Map，VR手柄输入将不可用");
            return;
        }

        _vrPitchAction = _pitchActionMap.FindAction("VRPitch");
        if (_vrPitchAction != null)
            _vrPitchAction.performed += OnVRPitchPerformed;

        Debug.Log("[PitchTrigger] VR输入初始化成功");
    }

    private void OnVRPitchPerformed(InputAction.CallbackContext context)
    {
        if (canPitch)
        {
            Debug.Log("[PitchTrigger] VR手柄触发投球");
            TriggerPitch();
        }
        else
        {
            Debug.Log("[PitchTrigger] 当前无法投球，等待动画完成");
        }
    }
    #endregion

    #region 键盘输入（保持原有实现）
    private void OnKeyboardPitchPerformed(InputAction.CallbackContext context)
    {
        if (canPitch)
        {
            Debug.Log("[PitchTrigger] 键盘空格触发投球");
            TriggerPitch();
        }
        else
        {
            Debug.Log("[PitchTrigger] 当前无法投球，等待动画完成");
        }
    }
    #endregion

    #region 投球逻辑
    public void TriggerPitch()
    {
        if (!canPitch)
        {
            Debug.Log("[PitchTrigger] 投球被阻止: canPitch = false");
            return;
        }

        canPitch = false;
        Debug.Log("[PitchTrigger] 触发投球动画");

        // 取消之前的重置
        if (resetCoroutine != null)
            StopCoroutine(resetCoroutine);

        // 启动超时重置
        resetCoroutine = StartCoroutine(ResetAfterDelay(animationDuration));

        if (pitcherAnimator != null)
        {
            pitcherAnimator.ResetTrigger(pitchTriggerParam);
            pitcherAnimator.SetTrigger(pitchTriggerParam);
        }

        OnPitchTriggered?.Invoke();
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canPitch = true;
        resetCoroutine = null;
        Debug.Log("[PitchTrigger] 超时重置完成");
    }

    // 动画事件调用的方法
    public void OnPitchAnimationComplete()
    {
        Debug.Log("[PitchTrigger] 动画事件触发: OnPitchAnimationComplete");

        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }

        canPitch = true;
        Debug.Log("[PitchTrigger] 投球状态已重置，可以再次投球");
    }

    public void ResetPitchState()
    {
        canPitch = true;
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }
    #endregion

    private void OnEnable()
    {
        _vrPitchAction?.Enable();
    }

    private void OnDisable()
    {
        _vrPitchAction?.Disable();
        if (_vrPitchAction != null)
            _vrPitchAction.performed -= OnVRPitchPerformed;
    }

    private void OnDestroy()
    {
        // 清理键盘输入
        if (keyboardPitchAction != null)
        {
            keyboardPitchAction.performed -= OnKeyboardPitchPerformed;
            keyboardPitchAction.Disable();
        }
    }
}