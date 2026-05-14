using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// VR玩家移动控制器（Y轴完全锁定版）
/// 仅支持XZ平面水平移动，彻底禁止上下高度变化
/// 挂载在XR Origin (XR Rig) 根节点
/// </summary>
public class VRPlayerMovement : MonoBehaviour
{
    [Header("核心配置")]
    public InputActionAsset inputActions;

    [Header("移动参数")]
    public float moveSpeed = 2.5f;
    public float stickDeadZone = 0.1f;
    public bool enableMovement = true;

    [Header("高度锁定配置")]
    [Tooltip("固定玩家的Y轴高度，禁止任何上下移动")]
    public float fixedYHeight = 0.9f; // 匹配你的击球手站位高度

    [Header("XR组件")]
    public Camera vrMainCamera;
    public CharacterController xrCharacterController;

    private InputAction _rightJoystickAction;
    private InputActionMap _baseballActionMap;

    void Awake()
    {
        // 自动获取XR组件
        Transform xrOrigin = transform;
        if (vrMainCamera == null)
            vrMainCamera = xrOrigin.Find("Camera Offset/Main Camera")?.GetComponent<Camera>();

        if (xrCharacterController == null)
        {
            xrCharacterController = xrOrigin.GetComponent<CharacterController>();
            if (xrCharacterController == null)
            {
                xrCharacterController = gameObject.AddComponent<CharacterController>();
                xrCharacterController.height = 1.8f;
                xrCharacterController.radius = 0.3f;
                xrCharacterController.center = new Vector3(0, 0.9f, 0);
            }
        }

        // 仅初始化摇杆输入，彻底隔离按钮事件
        InitializeInput();
    }

    void InitializeInput()
    {
        if (inputActions == null)
        {
            Debug.LogError("[VR移动] 请拖入Input Action资产！");
            return;
        }

        _baseballActionMap = inputActions.FindActionMap("BaseballControls");
        if (_baseballActionMap == null)
        {
            Debug.LogError("[VR移动] 未找到BaseballControls Action Map！");
            return;
        }

        // 仅获取右摇杆Action，完全不监听任何按钮（包括A键）
        _rightJoystickAction = _baseballActionMap.FindAction("RightJoystick");
        if (_rightJoystickAction == null)
        {
            Debug.LogError("[VR移动] 未找到RightJoystick Action！");
            return;
        }

        Debug.Log("[VR移动] 摇杆输入初始化成功，仅监听右摇杆水平移动");
    }

    void OnEnable()
    {
        _rightJoystickAction?.Enable();
    }

    void OnDisable()
    {
        _rightJoystickAction?.Disable();
    }

    void Update()
    {
        if (!enableMovement || xrCharacterController == null || _rightJoystickAction == null)
            return;

        // 仅读取摇杆输入
        Vector2 joystickInput = _rightJoystickAction.ReadValue<Vector2>();

        // 死区过滤，防止摇杆漂移误触
        if (joystickInput.magnitude < stickDeadZone)
            joystickInput = Vector2.zero;

        // 基于头显朝向计算水平移动方向（完全锁定Y轴，无任何高度分量）
        Vector3 headForward = Vector3.Scale(vrMainCamera.transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 headRight = Vector3.Scale(vrMainCamera.transform.right, new Vector3(1, 0, 1)).normalized;
        Vector3 moveDir = (headForward * joystickInput.y + headRight * joystickInput.x).normalized;

        // 仅执行XZ平面水平移动，完全不修改Y轴
        if (moveDir.magnitude > 0)
        {
            xrCharacterController.Move(moveDir * moveSpeed * Time.deltaTime);
        }
    }

    // LateUpdate强制锁定Y轴高度，彻底杜绝任何上下移动（优先级最高）
    void LateUpdate()
    {
        Vector3 currentPos = transform.position;
        // 强制固定Y轴高度，无视任何其他逻辑的修改
        float headLocalY = vrMainCamera.transform.localPosition.y;
        currentPos.y = fixedYHeight - headLocalY; // 让脚底固定，头部跟随现实
        transform.position = currentPos;
    }

    /// <summary>
    /// 对外暴露的移动开关（GameManager流程调用）
    /// </summary>
    public void SetMovementActive(bool isActive)
    {
        enableMovement = isActive;
    }

    void OnDestroy()
    {
        _rightJoystickAction?.Disable();
        _rightJoystickAction?.Dispose();
    }
}