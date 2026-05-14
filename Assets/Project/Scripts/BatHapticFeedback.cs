using UnityEngine;
using System.Collections;

/// <summary>
/// 棒球棒碰撞震动反馈组件（支持 Trigger 和 Collision 两种检测方式）
/// 适配 Pico 4 Pro 及其他 XR 设备，含蓄力持续震动与击球瞬间震动
/// </summary>
[RequireComponent(typeof(Collider))]
public class BatHapticFeedback : MonoBehaviour
{
    [Header("碰撞检测配置")]
    [Tooltip("球棒碰撞中心点（甜点区）")]
    public Transform batCenter;
    [Tooltip("完美击球半径（甜点区）")]
    public float perfectRadius = 0.15f;
    [Tooltip("最大有效击球半径（超出此半径不震动）")]
    public float maxRadius = 0.4f;

    [Header("震动控制配置")]
    [Tooltip("是否右手持棒（左手无效时自动取反）")]
    public bool useRightHand = true;
    [Tooltip("全局启用触觉反馈")]
    public bool enableHaptic = true;

    // 震动参数常量
    private const float PerfectHitAmplitude = 1.0f;
    private const int PerfectHitDuration = 220;
    private const int PerfectHitFrequency = 160;
    private const float WeakHitAmplitude = 0.4f;
    private const int WeakHitDuration = 110;
    private const int WeakHitFrequency = 90;

    // 蓄力震动参数
    private const float ChargeAmplitudeMin = 0.1f;
    private const float ChargeAmplitudeMax = 0.6f;
    private const int ChargeFrequency = 80;
    private const float ChargeUpdateInterval = 0.05f;

    private Coroutine _chargeCoroutine;
    private float _currentChargeProgress;
    private bool _isCharging;

    #region 初始化

    private void Awake()
    {
        // 自动查找球棒中心点
        if (batCenter == null)
        {
            batCenter = transform.Find("SweetSpot") ?? transform;
            Debug.LogWarning("[BatHaptic] 未指定 batCenter，已自动使用自身 Transform");
        }

        // 确保碰撞体存在且不为触发器（这里不强制，因为触发器也能用 OnTriggerEnter 检测）
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogError("[BatHaptic] 缺少 Collider 组件！");
        }
    }

    private void OnDisable()
    {
        StopChargeVibration();
    }

    private void OnDestroy()
    {
        StopChargeVibration();
    }

    #endregion

    #region 蓄力震动（供 VRBatHitController 调用）

    /// <summary>
    /// 开始/更新蓄力震动（每帧调用，自动优化）
    /// </summary>
    public void StartChargeVibration(float chargeProgress)
    {
        if (!enableHaptic) return;

        _currentChargeProgress = Mathf.Clamp01(chargeProgress);

        if (!_isCharging)
        {
            _isCharging = true;
            if (_chargeCoroutine != null) StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = StartCoroutine(ChargeVibrationLoop());
        }
    }

    /// <summary>
    /// 停止蓄力震动
    /// </summary>
    public void StopChargeVibration()
    {
        if (_chargeCoroutine != null)
        {
            StopCoroutine(_chargeCoroutine);
            _chargeCoroutine = null;
        }
        _isCharging = false;
        SendHapticImpulse(0f, 0, 0); // 立即停止手柄震动
    }

    private IEnumerator ChargeVibrationLoop()
    {
        WaitForSeconds wait = new WaitForSeconds(ChargeUpdateInterval);
        while (_isCharging)
        {
            float amplitude = Mathf.Lerp(ChargeAmplitudeMin, ChargeAmplitudeMax, _currentChargeProgress);
            SendHapticImpulse(amplitude, (int)(ChargeUpdateInterval * 1000), ChargeFrequency);
            yield return wait;
        }
    }

    #endregion

    #region 碰撞震动（支持 OnTriggerEnter 和 OnCollisionEnter）

    /// <summary>
    /// 触发器进入（若球棒 Collider 为 Trigger，则使用此事件）
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!enableHaptic) return;
        if (!ValidateBaseConfig()) return;
        if (!other.CompareTag("Baseball")) return;

        // 击球时停止蓄力震动
        StopChargeVibration();

        // 计算击球点到球棒中心距离
        Vector3 hitPoint = other.ClosestPoint(batCenter.position);
        float distance = Vector3.Distance(hitPoint, batCenter.position);
        TriggerHapticByDistance(distance);
    }

    /// <summary>
    /// 碰撞进入（若球棒 Collider 为非触发器，则使用此事件）
    /// 保留以兼容非触发器模式
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        if (!enableHaptic) return;
        if (!ValidateBaseConfig()) return;
        if (!collision.gameObject.CompareTag("Baseball")) return;

        StopChargeVibration();

        if (collision.contacts.Length > 0)
        {
            Vector3 hitPoint = collision.contacts[0].point;
            float distance = Vector3.Distance(hitPoint, batCenter.position);
            TriggerHapticByDistance(distance);
        }
    }

    private bool ValidateBaseConfig()
    {
        if (batCenter == null)
        {
            Debug.LogError("[BatHaptic] 球棒碰撞中心点（batCenter）未设置");
            return false;
        }
        if (perfectRadius < 0 || maxRadius < perfectRadius)
        {
            Debug.LogError("[BatHaptic] 半径配置错误：perfectRadius 必须 <= maxRadius 且 >= 0");
            return false;
        }
        return true;
    }

    private void TriggerHapticByDistance(float distance)
    {
        if (distance <= perfectRadius)
            TriggerPerfectHitHaptic();
        else if (distance <= maxRadius)
            TriggerWeakHitHaptic();
        // 超出 maxRadius 不震动
    }

    private void TriggerPerfectHitHaptic()
    {
        SendHapticImpulse(PerfectHitAmplitude, PerfectHitDuration, PerfectHitFrequency);
        Debug.Log($"[BatHaptic] 完美碰撞震动 (距离:{Vector3.Distance(GetComponent<Collider>().ClosestPoint(batCenter.position), batCenter.position):F2})");
    }

    private void TriggerWeakHitHaptic()
    {
        SendHapticImpulse(WeakHitAmplitude, WeakHitDuration, WeakHitFrequency);
        Debug.Log("[BatHaptic] 普通碰撞震动");
    }

    #endregion

    #region 硬件震动接口（Pico / OpenXR / 编辑器模拟）

    private void SendHapticImpulse(float amplitude, int durationMs, int frequency)
    {
        float clampedAmplitude = Mathf.Clamp01(amplitude);
        int clampedDuration = Mathf.Clamp(durationMs, 0, 1000);
        ushort clampedFrequency = (ushort)Mathf.Clamp(frequency, 1, 320);

#if PICO_XR || UNITY_XR_PXR
        // Pico XR Plugin
        using Unity.XR.PXR;
        PXR_Input.VibrateType targetHand = useRightHand
            ? PXR_Input.VibrateType.RightController
            : PXR_Input.VibrateType.LeftController;

        try
        {
            if (clampedDuration > 0)
                PXR_Input.SendHapticImpulse(targetHand, clampedFrequency, clampedAmplitude, clampedDuration);
            else
                PXR_Input.StopHapticImpulse(targetHand);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BatHaptic] Pico 震动异常: {e.Message}");
        }
#elif UNITY_OPENXR
        // OpenXR 标准震动
        UnityEngine.XR.InputDevice device = useRightHand
            ? UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand)
            : UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
        {
            device.SendHapticImpulse(0, clampedAmplitude, clampedDuration / 1000f);
        }
        else
        {
            Debug.LogWarning("[BatHaptic] 当前设备不支持震动或未找到手柄");
        }
#else
        // 编辑器模拟
        if (clampedDuration > 0)
        {
            Debug.Log($"[BatHaptic] 模拟震动 | 手:{(useRightHand ? "右" : "左")} | 强度:{clampedAmplitude:F2} | 时长:{clampedDuration}ms | 频率:{clampedFrequency}Hz");
        }
#endif
    }

    #endregion

    #region 编辑器辅助

    private void OnDrawGizmosSelected()
    {
        if (batCenter == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(batCenter.position, perfectRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(batCenter.position, maxRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(batCenter.position, 0.05f);
    }

    #endregion
}