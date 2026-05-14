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

    private bool canPitch = true;
    private InputAction spaceAction;      // 空格键
    private InputAction triggerAction;    // 左手扳机键
    private Coroutine resetCoroutine;

    private void Start()
    {
        if (pitcherAnimator == null)
            pitcherAnimator = GetComponent<Animator>();

        // 创建空格键输入
        spaceAction = new InputAction("SpacePitch", binding: "<Keyboard>/space");
        spaceAction.performed += OnPitchInput;
        spaceAction.Enable();

        // 创建VR左手扳机键输入
        triggerAction = new InputAction("TriggerPitch", binding: "<XRController>{LeftHand}/trigger");
        triggerAction.performed += OnPitchInput;
        triggerAction.Enable();

        Debug.Log("投球控制已启动 | 空格键 或 VR左手扳机键 触发");
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

    private void OnPitchInput(InputAction.CallbackContext context)
    {
        if (canPitch && context.phase == InputActionPhase.Performed)
        {
            Debug.Log($"收到输入: {context.action.name}");
            TriggerPitch();
        }
        else if (!canPitch)
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

        // 取消之前的重置
        if (resetCoroutine != null)
            StopCoroutine(resetCoroutine);

        // 启动超时重置
        resetCoroutine = StartCoroutine(ResetAfterDelay(animationDuration));

        // 播放动画
        if (pitcherAnimator != null)
        {
            pitcherAnimator.ResetTrigger(pitchTriggerParam);
            pitcherAnimator.SetTrigger(pitchTriggerParam);
        }
        else
        {
            Debug.LogError("Animator 组件未找到！");
        }

        // 触发事件
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