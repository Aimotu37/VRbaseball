using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class VRBatHitController : MonoBehaviour
{
    [Header("核心引用配置")]
    public InputActionAsset inputActions;
    public Transform rightHandTransform;
    public AtBatCounter gameManager;

    [Header("击打参数配置")]
    public float hitForceMultiplier = 20f;
    public float maxSwingSpeed = 40f;
    public float maxChargeTime = 1.5f;

    [Header("握持偏移")]
    public Vector3 holdPositionOffset = new Vector3(0.2f, 0.5f, 0.3f);
    public Vector3 holdRotationOffset = new Vector3(0, -90, 0);

    private Rigidbody _batRigidbody;
    private Vector3 _lastFramePosition;
    private float _swingSpeed;
    private float _currentChargeTime = 0f;
    private bool _isCharging = false;
    private bool _isBatReset = true;

    private InputAction _triggerAction;
    private InputAction _gripAction;
    private InputAction _buttonBAction;
    private InputActionMap _baseballActionMap;
    private BatHapticFeedback _hapticFeedback;

    private void Awake()
    {
        InitRigidbody();
        AutoFindRightHandTransform();
        AutoFindGameManager();
        _hapticFeedback = GetComponent<BatHapticFeedback>();
        InitializeInput();
    }

    private void OnEnable()
    {
        _triggerAction?.Enable();
        _gripAction?.Enable();
        _buttonBAction?.Enable();
    }

    private void OnDisable()
    {
        _triggerAction?.Disable();
        _gripAction?.Disable();
        _buttonBAction?.Disable();
    }

    private void FixedUpdate()
    {
        if (rightHandTransform == null) return;
        UpdateBatTransform();
        CalculateSwingSpeed();
        UpdateChargeState();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleBaseballHit(other);
    }

    private void OnDestroy()
    {
        CleanupInput();
        CancelInvoke();
    }

    private void InitRigidbody()
    {
        _batRigidbody = GetComponent<Rigidbody>();
        _batRigidbody.isKinematic = true;
        _batRigidbody.useGravity = false;
        _batRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        _lastFramePosition = transform.position;
    }

    private void AutoFindRightHandTransform()
    {
        if (rightHandTransform == null)
        {
            Transform xrOrigin = FindXROrigin();
            if (xrOrigin != null)
                rightHandTransform = xrOrigin.Find("Camera Offset/Right Controller");
        }
    }

    private void AutoFindGameManager()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<AtBatCounter>();
    }

    private void InitializeInput()
    {
        if (inputActions == null)
        {
            Debug.LogError("[棒球棒控制器] 未分配Input Action资源文件！");
            return;
        }
        _baseballActionMap = inputActions.FindActionMap("BaseballControls");
        if (_baseballActionMap == null)
        {
            Debug.LogError("[棒球棒控制器] 未找到BaseballControls Action Map！");
            return;
        }
        _triggerAction = _baseballActionMap.FindAction("Trigger");
        _gripAction = _baseballActionMap.FindAction("Grip");
        _buttonBAction = _baseballActionMap.FindAction("ButtonB");

        if (_triggerAction == null || _gripAction == null || _buttonBAction == null)
        {
            Debug.LogError("[棒球棒控制器] 未找到Trigger/Grip/ButtonB Action！");
            return;
        }

        _triggerAction.performed += OnTriggerPressed;
        _gripAction.performed += OnGripPressed;
        _buttonBAction.started += OnButtonBStart;
        _buttonBAction.canceled += OnButtonBRelease;
        Debug.Log("[棒球棒控制器] 按键监听初始化成功");
    }

    private void OnTriggerPressed(InputAction.CallbackContext context)
    {
        if (!_isBatReset) return;
        _isBatReset = false;
    }

    private void OnGripPressed(InputAction.CallbackContext context)
    {
        _isBatReset = true;
        _currentChargeTime = 0f;
        _isCharging = false;
        _hapticFeedback?.StopChargeVibration();
    }

    private void OnButtonBStart(InputAction.CallbackContext context)
    {
        if (!_isBatReset) return;
        _isCharging = true;
        _currentChargeTime = 0f;
    }

    private void OnButtonBRelease(InputAction.CallbackContext context)
    {
        _isCharging = false;
        _hapticFeedback?.StopChargeVibration();
    }

    private void UpdateBatTransform()
    {
        Vector3 targetPos = rightHandTransform.position + rightHandTransform.rotation * holdPositionOffset;
        Quaternion targetRot = rightHandTransform.rotation * Quaternion.Euler(holdRotationOffset);

        if (_isBatReset)
        {
            _batRigidbody.MovePosition(Vector3.Lerp(transform.position, targetPos, Time.fixedDeltaTime * 30f));
            _batRigidbody.MoveRotation(Quaternion.Lerp(transform.rotation, targetRot, Time.fixedDeltaTime * 30f));
        }
        else
        {
            _batRigidbody.MovePosition(targetPos);
            _batRigidbody.MoveRotation(targetRot);
        }
    }

    private void CalculateSwingSpeed()
    {
        float frameDistance = Vector3.Distance(transform.position, _lastFramePosition);
        _swingSpeed = frameDistance / Time.fixedDeltaTime;
        _lastFramePosition = transform.position;
    }

    private void UpdateChargeState()
    {
        if (_isCharging)
        {
            _currentChargeTime = Mathf.Min(_currentChargeTime + Time.fixedDeltaTime, maxChargeTime);
            if (_hapticFeedback != null)
            {
                float progress = _currentChargeTime / maxChargeTime;
                _hapticFeedback.StartChargeVibration(progress);
            }
        }
    }

    private void HandleBaseballHit(Collider other)
    {
        if (!other.CompareTag("Baseball")) return;

        BaseballController baseballCtrl = other.GetComponent<BaseballController>();
        Rigidbody baseballRb = other.GetComponent<Rigidbody>();

        if (baseballCtrl == null || baseballRb == null || baseballCtrl.hasBeenHit) return;

        // ★ 核心修复：出生无敌期直接忽略，防止误触
        if (Time.time - baseballCtrl.spawnTime < 0.3f)
        {
            Debug.Log("[VRBat] 球处于无敌期，忽略触发器击打");
            return;
        }

        bool isPlateActive = gameManager != null && gameManager.IsPlateActive;
        bool isBatSwinging = !_isBatReset;
        if (!isPlateActive || !isBatSwinging) return;

        Vector3 hitDirection = (other.transform.position - transform.position).normalized;
        float clampedSwingSpeed = Mathf.Clamp(_swingSpeed, 0, maxSwingSpeed);
        float chargeMultiplier = 1f + (_currentChargeTime / maxChargeTime);
        float finalHitForce = clampedSwingSpeed * hitForceMultiplier * chargeMultiplier;
        float hitQuality = Mathf.Clamp01((_swingSpeed / maxSwingSpeed) * chargeMultiplier);

        baseballRb.velocity = Vector3.zero;
        baseballRb.angularVelocity = Vector3.zero;
        baseballRb.AddForce(hitDirection * finalHitForce, ForceMode.Impulse);

        baseballCtrl.OnHit(hitQuality);

        Invoke(nameof(ResetBatDelayed), 0.5f);
        _hapticFeedback?.StopChargeVibration();
    }

    private void ResetBatDelayed()
    {
        _isBatReset = true;
        _currentChargeTime = 0f;
        _isCharging = false;
    }

    public static Transform FindXROrigin()
    {
        return GameObject.Find("XR Origin (XR Rig)")?.transform;
    }

    private void CleanupInput()
    {
        if (_triggerAction != null) _triggerAction.performed -= OnTriggerPressed;
        if (_gripAction != null) _gripAction.performed -= OnGripPressed;
        if (_buttonBAction != null)
        {
            _buttonBAction.started -= OnButtonBStart;
            _buttonBAction.canceled -= OnButtonBRelease;
        }
        _triggerAction?.Dispose();
        _gripAction?.Dispose();
        _buttonBAction?.Dispose();
    }
}