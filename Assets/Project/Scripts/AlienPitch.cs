using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System.Collections;

public class AlienPitch : MonoBehaviour
{
    [Header("动画配置")]
    [SerializeField] private Animator pitcherAnimator;
    [SerializeField] private string pitchTriggerParam = "alientpitch";
    [SerializeField] private float animationDuration = 1.5f;

    [Header("事件")]
    public UnityEvent OnPitchTriggered;

    private bool canPitch = false;
    private bool hasInitialPitch = false;
    private InputAction spaceAction;
    private InputAction triggerAction;
    private Coroutine resetCoroutine;

    private void Awake()
    {
        // 确保初始状态为不可投球
        canPitch = false;
        hasInitialPitch = false;
    }

    private void OnEnable()
    {
        // 每次激活时强制重置（防止对象池等复用）
        canPitch = false;
        hasInitialPitch = false;
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
    }

    private void Start()
    {
        if (pitcherAnimator == null)
            pitcherAnimator = GetComponent<Animator>();

        // 创建输入（不立即启用）
        spaceAction = new InputAction("SpacePitch", binding: "<Keyboard>/space");
        spaceAction.performed += OnPitchInput;
        // 先禁用，等延迟后再启用
        spaceAction.Disable();

        triggerAction = new InputAction("TriggerPitch", binding: "<XRController>{LeftHand}/trigger");
        triggerAction.performed += OnPitchInput;
        triggerAction.Disable();

        // 延迟一帧再启用输入，避免生成瞬间的误触发
        StartCoroutine(DelayedEnableInput());
    }

    private IEnumerator DelayedEnableInput()
    {
        yield return null; // 等一帧
        spaceAction?.Enable();
        triggerAction?.Enable();
        Debug.Log("投手已就绪，请按左手扳机键或空格开始第一球");
    }

    private void OnDestroy()
    {
        if (spaceAction != null)
        {
            spaceAction.performed -= OnPitchInput;
            spaceAction.Disable();
        }
        if (triggerAction != null)
        {
            triggerAction.performed -= OnPitchInput;
            triggerAction.Disable();
        }
    }

    private void OnDisable()
    {
        // 禁用时关闭输入，防止后台误触
        spaceAction?.Disable();
        triggerAction?.Disable();
    }

    private void OnPitchInput(InputAction.CallbackContext context)
    {
        if (context.phase != InputActionPhase.Performed)
            return;

        // 首次扳机按下：激活并投出第一球
        if (!hasInitialPitch)
        {
            hasInitialPitch = true;
            canPitch = true;
            Debug.Log("首次扳机按下：激活投球并开始第一球！");
            TriggerPitch();
            return;
        }

        // 后续投球
        if (canPitch)
        {
            TriggerPitch();
        }
        else
        {
            Debug.Log("当前无法投球，等待动画完成");
        }
    }

    public void TriggerPitch()
    {
        if (!canPitch)
        {
            Debug.Log("投球被阻止: canPitch = false");
            return;
        }

        canPitch = false;
        Debug.Log("触发投球动画: alientpitch");

        if (resetCoroutine != null)
            StopCoroutine(resetCoroutine);
        resetCoroutine = StartCoroutine(ResetAfterDelay(animationDuration));

        if (pitcherAnimator != null)
        {
            pitcherAnimator.ResetTrigger(pitchTriggerParam);
            pitcherAnimator.SetTrigger(pitchTriggerParam);
        }
        else
        {
            Debug.LogError("Animator 组件未找到！");
        }

        OnPitchTriggered?.Invoke();
    }

    private IEnumerator ResetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        canPitch = true;
        resetCoroutine = null;
        Debug.Log("超时重置完成，可以再次投球");
    }

    public void OnPitchAnimationComplete()
    {
        Debug.Log("动画事件触发: OnPitchAnimationComplete");
        if (resetCoroutine != null)
        {
            StopCoroutine(resetCoroutine);
            resetCoroutine = null;
        }
        canPitch = true;
        Debug.Log("投球状态已重置，可以再次投球");
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
}