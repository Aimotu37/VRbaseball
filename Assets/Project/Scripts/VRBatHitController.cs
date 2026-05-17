using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// VR棒球棒击打控制器
/// 负责处理棒球棒的VR控制、挥棒速度计算、蓄力击打、碰撞检测等核心逻辑
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class VRBatHitController : MonoBehaviour
{
    [Header("核心引用配置")]
    [Tooltip("InputSystem动作资源文件")]
    public InputActionAsset inputActions;
    [Tooltip("右手控制器Transform")]
    public Transform rightHandTransform;
    [Tooltip("游戏管理器（用于判断击球区状态）")]
    public AtBatCounter gameManager;

    [Header("击打参数配置")]
    [Tooltip("击打力量倍率")]
    public float hitForceMultiplier = 20f;
    [Tooltip("最大挥棒速度限制")]
    public float maxSwingSpeed = 40f;
    [Tooltip("最大蓄力时间")]
    public float maxChargeTime = 1.5f;

    [Header("重置位置配置")]
    [Tooltip("相对于右手控制器的重置位置偏移")]
    public Vector3 resetPositionOffset = new Vector3(0.2f, 0.5f, 0.3f);
    [Tooltip("相对于右手控制器的重置旋转偏移")]
    public Vector3 resetRotationOffset = new Vector3(0, -90, 0);

    [Header("握持偏移（两种状态通用）")]
    [Tooltip("球棒握把相对控制器的位置偏移")]
    public Vector3 holdPositionOffset = new Vector3(0.2f, 0.5f, 0.3f);
    [Tooltip("球棒握把相对控制器的旋转偏移")]
    public Vector3 holdRotationOffset = new Vector3(0, -90, 0);
    // 私有核心变量
    private Rigidbody _batRigidbody;          // 棒球棒刚体组件
    private Vector3 _lastFramePosition;       // 上一帧位置（用于计算挥棒速度）
    private float _swingSpeed;                // 当前挥棒速度
    private float _currentChargeTime = 0f;    // 当前蓄力时间
    private bool _isCharging = false;         // 是否正在蓄力
    private bool _isBatReset = true;          // 棒球棒是否已重置到初始位置

    // InputSystem动作引用
    private InputAction _triggerAction;       // 扳机键动作（释放棒球棒跟随）
    private InputAction _gripAction;          // 握把键动作（重置棒球棒）
    //private InputAction _buttonBAction;       // B键动作（蓄力）
    private InputActionMap _baseballActionMap;// 棒球控制动作映射

    // 触觉反馈组件
    private BatHapticFeedback _hapticFeedback;

    #region 生命周期函数
    private void Awake()
    {
        // 初始化刚体组件
        InitRigidbody();

        // 自动查找右手控制器（如果未手动指定）
        AutoFindRightHandTransform();

        // 自动查找游戏管理器
        AutoFindGameManager();

        // 获取触觉反馈组件
        _hapticFeedback = GetComponent<BatHapticFeedback>();

        // 初始化输入系统
        InitializeInput();
    }


    private void OnEnable()
    {
        // 启用输入动作
        _triggerAction?.Enable();
        _gripAction?.Enable();
       // _buttonBAction?.Enable();
    }

    private void OnDisable()
    {
        // 禁用输入动作
        _triggerAction?.Disable();
        _gripAction?.Disable();
        //_buttonBAction?.Disable();
    }

    private void FixedUpdate()
    {
        if (rightHandTransform == null) return;

        // 处理棒球棒位置/旋转跟随逻辑
        UpdateBatTransform();

        // 计算挥棒速度
        CalculateSwingSpeed();

        // 处理蓄力逻辑
        UpdateChargeState();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 检测是否击中棒球
        HandleBaseballHit(other);
    }

    private void OnDestroy()
    {
        // 清理输入动作监听和资源
        CleanupInput();
        CancelInvoke();
    }
    #endregion

    #region 初始化逻辑
    /// <summary>
    /// 初始化刚体组件参数
    /// </summary>
    private void InitRigidbody()
    {
        _batRigidbody = GetComponent<Rigidbody>();
        _batRigidbody.isKinematic = true;          // 运动学刚体（由代码控制运动）
        _batRigidbody.useGravity = false;          // 禁用重力
        _batRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 连续碰撞检测（提升击打精度）
        _lastFramePosition = transform.position;   // 初始化上一帧位置
    }

    /// <summary>
    /// 自动查找右手控制器Transform
    /// </summary>
    private void AutoFindRightHandTransform()
    {
        if (rightHandTransform == null)
        {
            Transform xrOrigin = FindXROrigin();
            if (xrOrigin != null)
                rightHandTransform = xrOrigin.Find("Camera Offset/Right Controller");
        }
    }

    /// <summary>
    /// 自动查找游戏管理器
    /// </summary>
    private void AutoFindGameManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<AtBatCounter>();
    }

    /// <summary>
    /// 初始化输入系统
    /// </summary>
    private void InitializeInput()
    {
        if (inputActions == null)
        {
            Debug.LogError("[棒球棒控制器] 未分配Input Action资源文件！");
            return;
        }

        // 查找棒球控制动作映射
        _baseballActionMap = inputActions.FindActionMap("BaseballControls");
        if (_baseballActionMap == null)
        {
            Debug.LogError("[棒球棒控制器] 未找到BaseballControls Action Map！");
            return;
        }

        // 绑定输入动作
        _triggerAction = _baseballActionMap.FindAction("Trigger");
        _gripAction = _baseballActionMap.FindAction("Grip");
        //_buttonBAction = _baseballActionMap.FindAction("ButtonB");

        // 检测必要动作是否存在
        if (_triggerAction == null || _gripAction == null)
        {
            Debug.LogError("[棒球棒控制器] 未找到Trigger/Grip！");
            return;
        }

        // 绑定输入事件
        _triggerAction.performed += OnTriggerPressed;
        _triggerAction.canceled += OnTriggerReleased;
        _gripAction.performed += OnGripPressed;
        //_buttonBAction.started += OnButtonBStart;
        //_buttonBAction.canceled += OnButtonBRelease;

        Debug.Log("[棒球棒控制器] 按键监听初始化成功，等待输入...");
        Debug.Log("[棒球棒控制器] Trigger/Grip 绑定成功，等待输入...");
    }
    #endregion

    #region 输入事件处理
    /// <summary>
    /// 扳机键按下事件（释放棒球棒跟随限制）
    /// </summary>
    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (!_isBatReset) return;

        _isBatReset = false;
        _isCharging = true;           // 开始蓄力
        _currentChargeTime = 0f;
        Debug.Log($"[棒球棒控制器] Trigger按下，解除跟随状态 | 当前蓄力时间：{_currentChargeTime:F2}s");
    }

    /// <summary>
    /// 握把键按下事件（重置棒球棒位置）
    /// </summary>
    private void OnGripPressed(InputAction.CallbackContext context)
    {
        if (!_isBatReset) return;
        // 只有未挥棒时才允许重置
        // 避免挥棒过程中按下握柄键会强制重置棒球棒位置，严重影响击球体验。

        _isBatReset = true;
        _currentChargeTime = 0f;
        _isCharging = false;
        _hapticFeedback?.StopChargeVibration();
        Debug.Log("[棒球棒控制器] Grip按下，棒球棒已重置");
    }

    /// <summary>
    /// B键按下事件（开始蓄力）
    /// </summary>
    /*private void OnButtonBStart(InputAction.CallbackContext context)
    {
        if (!_isBatReset) return;

        _isCharging = true;
        _currentChargeTime = 0f;
        Debug.Log("[棒球棒控制器] B键按下，开始蓄力");
    }

    /// <summary>
    /// B键释放事件（结束蓄力）
    /// </summary>
    private void OnButtonBRelease(InputAction.CallbackContext context)
    {
        _isCharging = false;
        _hapticFeedback?.StopChargeVibration();
        Debug.Log($"[棒球棒控制器] B键释放，蓄力总时长：{_currentChargeTime:F2}s");
    }*/

    private void OnTriggerReleased(InputAction.CallbackContext context)
    {
        if (!_isCharging) return;
        _isCharging = false;
        _hapticFeedback?.StopChargeVibration();
        Debug.Log($"[棒球棒控制器] Trigger释放，蓄力总时长：{_currentChargeTime:F2}s");
    }
    #endregion

    #region 核心逻辑更新
    /// <summary>
    /// 更新棒球棒的位置和旋转
    /// </summary>
    private void UpdateBatTransform()
    {
        Vector3 targetPos = rightHandTransform.position
                          + rightHandTransform.rotation * holdPositionOffset;
        Quaternion targetRot = rightHandTransform.rotation
                             * Quaternion.Euler(holdRotationOffset);

        if (_isBatReset)
        {
            // 复位：平滑插值回握持位置
            _batRigidbody.MovePosition(Vector3.Lerp(transform.position, targetPos, Time.fixedDeltaTime * 30f));
            _batRigidbody.MoveRotation(Quaternion.Lerp(transform.rotation, targetRot, Time.fixedDeltaTime * 30f));
        }
        else
        {
            // 挥棒：直接跟随（保留偏移）
            _batRigidbody.MovePosition(targetPos);
            _batRigidbody.MoveRotation(targetRot);
        }
    }


    /// <summary>
    /// 计算挥棒速度
    /// </summary>
    private void CalculateSwingSpeed()
    {
        float frameDistance = Vector3.Distance(transform.position, _lastFramePosition);
        _swingSpeed = frameDistance / Time.fixedDeltaTime;
        _lastFramePosition = transform.position;
    }

    /// <summary>
    /// 更新蓄力状态
    /// </summary>
    private void UpdateChargeState()
    {
        if (_isCharging)
        {
            // 限制蓄力时间不超过最大值
            _currentChargeTime = Mathf.Min(_currentChargeTime + Time.fixedDeltaTime, maxChargeTime);

            // 触发蓄力触觉反馈
            if (_hapticFeedback != null)
            {
                float chargeProgress = _currentChargeTime / maxChargeTime;
                _hapticFeedback.StartChargeVibration(chargeProgress);
            }
        }
    }

    /// <summary>
    /// 处理击中棒球的逻辑
    /// </summary>
    private void HandleBaseballHit(Collider other)
    {
        // 检测碰撞对象是否为棒球
        if (!other.CompareTag("Baseball")) return;

        BaseballController baseballCtrl = other.GetComponent<BaseballController>();
        Rigidbody baseballRb = other.GetComponent<Rigidbody>();

        // 验证棒球组件是否有效且未被击中过
        if (baseballCtrl == null || baseballRb == null || baseballCtrl.hasBeenHit) return;

        // 验证击球区激活状态和挥棒状态
        bool isPlateActive = gameManager != null && gameManager.IsPlateActive;
        bool isBatSwinging = !_isBatReset;
        if (!isPlateActive || !isBatSwinging) return;

        // 计算击打参数
        Vector3 hitDirection = (other.transform.position - transform.position).normalized;
        float clampedSwingSpeed = Mathf.Clamp(_swingSpeed, 0, maxSwingSpeed);
        float chargeMultiplier = 1f + (_currentChargeTime / maxChargeTime); // 蓄力倍率（1~2倍）
        float finalHitForce = clampedSwingSpeed * hitForceMultiplier * chargeMultiplier;
        float hitQuality = Mathf.Clamp01((_swingSpeed / maxSwingSpeed) * chargeMultiplier); // 击打质量（0~1）

        // 应用击打力量到棒球
        baseballRb.velocity = Vector3.zero;
        baseballRb.angularVelocity = Vector3.zero;
        baseballRb.AddForce(hitDirection * finalHitForce, ForceMode.Impulse);

        // 通知棒球被击中
        baseballCtrl.OnHit(hitQuality);

        // 延迟重置棒球棒
        Invoke(nameof(ResetBatDelayed), 0.5f);

        // 停止蓄力振动
        _hapticFeedback?.StopChargeVibration();
    }
    #endregion

    #region 辅助方法
    /// <summary>
    /// 延迟重置棒球棒状态
    /// </summary>
    private void ResetBatDelayed()
    {
        _isBatReset = true;
        _currentChargeTime = 0f;
        _isCharging = false;
    }

    /// <summary>
    /// 查找XR Origin对象
    /// </summary>
    public static Transform FindXROrigin()
    {
        return GameObject.Find("XR Origin (XR Rig)")?.transform;
    }

    /// <summary>
    /// 清理输入动作资源
    /// </summary>
    private void CleanupInput()
    {
        if (_triggerAction != null) _triggerAction.performed -= OnTriggerPressed;
        if (_gripAction != null) _gripAction.performed -= OnGripPressed;

        /*if (_buttonBAction != null)
        {
            _buttonBAction.started -= OnButtonBStart;
            _buttonBAction.canceled -= OnButtonBRelease;
        }*/

        _triggerAction?.Dispose();
        _gripAction?.Dispose();
       // _buttonBAction?.Dispose();
    }
    #endregion
}