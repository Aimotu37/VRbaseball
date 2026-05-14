using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.InputSystem;

public class AutoPitcher : MonoBehaviour
{
    [Header("输入系统配置")]
    public InputActionAsset inputActions;
    private InputActionMap _pitchActionMap;
    private InputAction _vrPitchAction;
    private InputAction _debugPitchAction;

    [Header("投球基础配置（现实棒球参数）")]
    public GameObject baseballPrefab;
    public float pitchInterval = 5f;
    [Range(20f, 45f)] public float pitchSpeed = 35f;
    [Range(0.05f, 0.2f)] public float pitchVariance = 0.1f;
    public float pitchArcHeight = 0.02f;

    [Header("好球区配置")]
    public Transform strikeZoneCenter;
    public float strikeZoneWidth = 0.6f;
    public float strikeZoneHeight = 0.9f;
    [Tooltip("强制投球落入好球区（调试用）")]
    public bool forceStrikeZone = true;
    [Tooltip("好球区投球偏差比例（0-1），0=完全精准，1=最大偏差")]
    [Range(0f, 1f)] public float strikeZoneVarianceRatio = 0.3f;

    [Header("系统配置")]
    public AtBatCounter gameManager;
    public Transform batterPosition;
    public PitchCountdownUI countdownUI;
    public UnityEvent onPitchComplete;
    public UnityEvent<GameObject> onBallHit;

    [Header("VR流程控制")]
    public bool forceAutoPitch = false;
    private bool _isWaitingForStart = true;
    private bool _isCountingDown = false;

    [Header("棒球拖尾特效配置")]
    public float pitchTrailTime = 0.5f;               // 投球拖尾持续时间
    public float hitTrailTime = 1.5f;                 // 击球拖尾持续时间
    public float pitchTrailWidth = 0.04f;             // 投球拖尾起始宽度
    public float hitTrailStartWidth = 0.08f;          // 击球拖尾起始宽度
    public float hitTrailEndWidth = 0.02f;            // 击球拖尾末端宽度
    public Color baseballTrajectoryColor = Color.cyan;
    public Color hitTrajectoryColor = Color.red;

    [Header("动画配置")]
    public Animator pitcherAnimator;

    private float _nextPitchTime;
    private float _gravity = Physics.gravity.y;
    private bool _isVRMode = true;

    void Awake()
    {
        _isVRMode = UnityEngine.XR.XRSettings.isDeviceActive;
        InitInputSystem();

        if (pitcherAnimator == null)
            pitcherAnimator = GetComponent<Animator>();

        if (_isVRMode)
        {
            pitchSpeed = Mathf.Clamp(pitchSpeed, 25f, 40f);
            pitchVariance = Mathf.Clamp(pitchVariance, 0.05f, 0.15f);
        }
    }

    void Start()
    {
        InitBatterPosition();
        InitGameManager();
        InitStrikeZone();
        _isWaitingForStart = true;
        _nextPitchTime = Mathf.Infinity;
    }

    void Update()
    {
        bool canAutoPitch = forceAutoPitch && !_isWaitingForStart && !_isCountingDown && Time.time >= _nextPitchTime;
        if (canAutoPitch)
        {
            ThrowPitch();
            _isWaitingForStart = true;
            _nextPitchTime = Mathf.Infinity;
        }
    }

    #region 输入系统初始化
    private void InitInputSystem()
    {
        if (inputActions == null)
        {
            Debug.LogError("请先拖入Input Action资产！");
            return;
        }

        _pitchActionMap = inputActions.FindActionMap("PitchControls");
        if (_pitchActionMap == null)
        {
            Debug.LogError("未找到PitchControls Action Map！");
            return;
        }

        _vrPitchAction = _pitchActionMap.FindAction("VRPitch");
        _debugPitchAction = _pitchActionMap.FindAction("DebugPitch");

        if (_vrPitchAction != null)
            _vrPitchAction.performed += OnVRPitchPerformed;
        if (_debugPitchAction != null)
            _debugPitchAction.performed += OnDebugPitchPerformed;

        Debug.Log("[投球] 投球输入初始化成功");
    }

    private void OnVRPitchPerformed(InputAction.CallbackContext context)
    {
        if (_isWaitingForStart && !_isCountingDown)
        {
            Debug.Log("VR手柄A键触发投球");
            OnStartButtonClicked();
        }
    }

    private void OnDebugPitchPerformed(InputAction.CallbackContext context)
    {
        if (_isWaitingForStart && !_isCountingDown)
        {
            Debug.Log("键盘空格触发投球");
            ManualPitch();
        }
    }

    void OnEnable()
    {
        _vrPitchAction?.Enable();
        _debugPitchAction?.Enable();
    }

    void OnDisable()
    {
        _vrPitchAction?.Disable();
        _debugPitchAction?.Disable();
        if (_vrPitchAction != null)
            _vrPitchAction.performed -= OnVRPitchPerformed;
        if (_debugPitchAction != null)
            _debugPitchAction.performed -= OnDebugPitchPerformed;
    }
    #endregion

    #region 棒球拖尾特效（TrailRenderer）
    /// <summary>
    /// 为棒球创建拖尾特效（投球或击球）
    /// </summary>
    private void CreateBaseballTrajectory(GameObject baseball, bool isHitTrajectory = false)
    {
        // 移除已有的 TrailRenderer，避免重叠
        TrailRenderer oldTrail = baseball.GetComponent<TrailRenderer>();
        if (oldTrail != null) Destroy(oldTrail);

        TrailRenderer trail = baseball.AddComponent<TrailRenderer>();

        // 拖尾显示时间
        trail.time = isHitTrajectory ? hitTrailTime : pitchTrailTime;

        // 最小顶点距离，越小拖尾越平滑
        trail.minVertexDistance = 0.1f;
        trail.autodestruct = false;

        // ---------- 宽度曲线 ----------
        AnimationCurve widthCurve = new AnimationCurve();
        if (isHitTrajectory)
        {
            // 击球拖尾：头粗尾细
            widthCurve.AddKey(0.0f, hitTrailStartWidth);
            widthCurve.AddKey(0.3f, hitTrailStartWidth * 0.7f);
            widthCurve.AddKey(1.0f, hitTrailEndWidth);
        }
        else
        {
            // 投球拖尾：较细且快速消失
            widthCurve.AddKey(0.0f, pitchTrailWidth);
            widthCurve.AddKey(1.0f, 0.0f);
        }
        trail.widthCurve = widthCurve;

        // ---------- 颜色渐变 ----------
        Gradient gradient = new Gradient();
        if (isHitTrajectory)
        {
            // 红色 → 橙色 → 完全透明
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(hitTrajectoryColor, 0.0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0f), 0.7f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
        }
        else
        {
            // 青色 → 青色透明
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(baseballTrajectoryColor, 0.0f),
                    new GradientColorKey(baseballTrajectoryColor, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
        }
        trail.colorGradient = gradient;

        // ---------- 材质（发光粒子效果） ----------
        Material trailMat = new Material(Shader.Find("Particles/Standard Unlit"));
        trailMat.SetFloat("_Mode", 2); // 透明混合
        trailMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        trailMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        trailMat.SetInt("_ZWrite", 0);
        trailMat.EnableKeyword("_EMISSION");
        Color emissionColor = isHitTrajectory ? hitTrajectoryColor * 2f : baseballTrajectoryColor * 1.5f;
        trailMat.SetColor("_EmissionColor", emissionColor);
        trail.material = trailMat;

        // 始终面向玩家（Billboard），VR 中视觉最佳
        trail.alignment = LineAlignment.View;
    }

    /// <summary>
    /// 球被击中时调用：替换成击球拖尾
    /// </summary>
    public void OnBallHit(GameObject baseball)
    {
        // 重新创建击球拖尾（会自动移除旧的投球拖尾）
        CreateBaseballTrajectory(baseball, true);
        onBallHit?.Invoke(baseball);
    }
    #endregion

    #region 初始化逻辑
    private void InitBatterPosition()
    {
        if (batterPosition == null)
        {
            GameObject defaultBatterPos = new GameObject("VR_DefaultBatterPosition");
            defaultBatterPos.transform.position = new Vector3(0, 1.2f, 8.0f);
            batterPosition = defaultBatterPos.transform;
        }
    }

    private void InitGameManager()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<AtBatCounter>();
            if (gameManager == null)
            {
                GameObject gmObj = new GameObject("VR_AutoAtBatCounter");
                gameManager = gmObj.AddComponent<AtBatCounter>();
                gameManager.ResetInning();
            }
        }
        else
        {
            gameManager.ResetInning();
        }
    }

    private void InitStrikeZone()
    {
        if (strikeZoneCenter == null)
        {
            GameObject defaultStrikeZone = new GameObject("VR_DefaultStrikeZoneCenter");
            defaultStrikeZone.transform.position = batterPosition.position + new Vector3(0, 1.1f, -0.1f);
            strikeZoneCenter = defaultStrikeZone.transform;
        }
    }
    #endregion

    #region 投球核心逻辑
    public void OnStartButtonClicked()
    {
        if (_isWaitingForStart && !_isCountingDown)
        {
            if (countdownUI != null && _isVRMode)
            {
                StartCoroutine(StartPitchCountdown());
            }
            else
            {
                TriggerPitchLogic();
            }
        }
    }

    private IEnumerator StartPitchCountdown()
    {
        _isCountingDown = true;
        _isWaitingForStart = false;
        yield return countdownUI.StartCountdown(() =>
        {
            TriggerPitchLogic();
            _isCountingDown = false;
        });
    }

    private void TriggerPitchLogic()
    {
        if (pitcherAnimator != null)
        {
            pitcherAnimator.ResetTrigger("OnPitch");
            pitcherAnimator.SetTrigger("OnPitch");
        }
        else
        {
            ThrowPitch();
        }
    }

    public void OnPitchAnimationEvent()
    {
        ThrowPitch();
    }

    void ThrowPitch()
    {
        // 动态创建基础棒球预制体（仅在未配置时）
        if (baseballPrefab == null)
        {
            GameObject defaultBaseball = new GameObject("VR_DefaultBaseball");
            MeshRenderer mr = defaultBaseball.AddComponent<MeshRenderer>();
            mr.material = new Material(Shader.Find("Unlit/Color"));
            mr.material.color = Color.white;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            MeshFilter mf = defaultBaseball.AddComponent<MeshFilter>();
            mf.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            Rigidbody rb = defaultBaseball.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 0.145f;
            rb.drag = 0.005f;
            rb.angularDrag = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            SphereCollider collider = defaultBaseball.AddComponent<SphereCollider>();
            collider.radius = 0.073f;
            collider.material = new PhysicMaterial("BaseballMat");
            collider.material.bounciness = 0.3f;
            collider.material.staticFriction = 0.1f;

            BaseballController bc = defaultBaseball.AddComponent<BaseballController>();
            bc.autoPitcher = this;

            defaultBaseball.tag = "Baseball";
            defaultBaseball.layer = LayerMask.NameToLayer("Interactive");
            baseballPrefab = defaultBaseball;
        }

        GameObject baseball = Instantiate(baseballPrefab, transform.position, Quaternion.identity);
        baseball.transform.rotation = Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(0f, 360f), Random.Range(-3f, 3f));

        Rigidbody baseballRb = baseball.GetComponent<Rigidbody>();
        if (baseballRb == null)
        {
            baseballRb = baseball.AddComponent<Rigidbody>();
            baseballRb.useGravity = true;
            baseballRb.mass = 0.145f;
            baseballRb.drag = 0.005f;
            baseballRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        // 1. 先初始化棒球控制器（会重置刚体，但此时还没有速度，所以安全）
        BaseballController baseballCtrl = baseball.GetComponent<BaseballController>();
        if (baseballCtrl == null)
            baseballCtrl = baseball.AddComponent<BaseballController>();
        baseballCtrl.Initialize(this, gameManager, strikeZoneCenter, strikeZoneWidth, strikeZoneHeight);

        // 2. 添加投球拖尾
        CreateBaseballTrajectory(baseball, false);

        // 3. 计算投球目标与速度
        Vector3 startPoint = transform.position;
        Vector3 targetPoint = strikeZoneCenter.position;

        if (forceStrikeZone)
        {
            targetPoint = new Vector3(
                strikeZoneCenter.position.x + Random.Range(-strikeZoneWidth / 2 * strikeZoneVarianceRatio, strikeZoneWidth / 2 * strikeZoneVarianceRatio),
                strikeZoneCenter.position.y + Random.Range(-strikeZoneHeight / 2 * strikeZoneVarianceRatio, strikeZoneHeight / 2 * strikeZoneVarianceRatio),
                strikeZoneCenter.position.z
            );
        }
        else
        {
            targetPoint += new Vector3(
                Random.Range(-pitchVariance * 0.2f, pitchVariance * 0.2f),
                Random.Range(-pitchVariance * 0.15f, pitchVariance * 0.15f),
                Random.Range(-pitchVariance * 0.1f, pitchVariance * 0.1f)
            );
        }

        Vector3 horizontalStart = new Vector3(startPoint.x, 0, startPoint.z);
        Vector3 horizontalTarget = new Vector3(targetPoint.x, 0, targetPoint.z);
        float horizontalDistance = Vector3.Distance(horizontalStart, horizontalTarget);
        if (horizontalDistance < 5f)
        {
            horizontalDistance = 8f;
            horizontalTarget = horizontalStart + Vector3.forward * horizontalDistance;
            targetPoint = new Vector3(horizontalTarget.x, targetPoint.y, horizontalTarget.z);
        }

        float flightTime = horizontalDistance / pitchSpeed;
        flightTime = Mathf.Clamp(flightTime, 0.3f, 0.6f);

        float heightDifference = targetPoint.y - startPoint.y;
        float requiredVerticalVelocity = (heightDifference + 0.5f * Mathf.Abs(_gravity) * flightTime * flightTime) / flightTime;
        requiredVerticalVelocity += pitchArcHeight;

        Vector3 horizontalDirection = (horizontalTarget - horizontalStart).normalized;
        Vector3 horizontalVelocity = horizontalDirection * pitchSpeed;
        Vector3 verticalVelocity = Vector3.up * requiredVerticalVelocity;
        Vector3 randomOffset = forceStrikeZone ? Vector3.zero : new Vector3(
            Random.Range(-pitchVariance * 0.03f, pitchVariance * 0.03f),
            Random.Range(-pitchVariance * 0.02f, pitchVariance * 0.02f),
            0
        );

        Vector3 finalVelocity = horizontalVelocity + verticalVelocity + randomOffset;

        // 4. 赋予最终速度（此时刚体已被 Initialize 清零，可以安全赋速）
        baseballRb.velocity = Vector3.zero;
        baseballRb.angularVelocity = Vector3.zero;
        baseballRb.AddForce(finalVelocity, ForceMode.VelocityChange);
        baseballRb.angularVelocity = new Vector3(
            Random.Range(50f, 80f),
            Random.Range(50f, 80f),
            Random.Range(50f, 80f)
        );

        Destroy(baseball, _isVRMode ? 5f : 4f);
        _nextPitchTime = Time.time + pitchInterval;
        _isWaitingForStart = false;

        Debug.Log($"投球：速度{pitchSpeed:F1}m/s | 飞行时间{flightTime:F2}s | 水平距离{horizontalDistance:F1}m");
    }

    public void OnPitchCompleted()
    {
        _isWaitingForStart = true;
        onPitchComplete?.Invoke();
    }

    public void ManualPitch()
    {
        if (!_isWaitingForStart) return;
        ThrowPitch();
        _isWaitingForStart = true;
    }
    #endregion

    #region 编辑器与内存管理
    private void OnDrawGizmos()
    {
        if (strikeZoneCenter != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(strikeZoneCenter.position, new Vector3(strikeZoneWidth, strikeZoneHeight, 1f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, strikeZoneCenter.position);

            Gizmos.color = Color.cyan;
            Vector3 startPoint = transform.position;
            Vector3 targetPoint = strikeZoneCenter.position;
            Vector3 direction = (targetPoint - startPoint).normalized;
            float distance = Vector3.Distance(startPoint, targetPoint);
            float flightTime = distance / pitchSpeed;
            float yOffset = 0.5f * _gravity * flightTime * flightTime;
            Vector3 midPoint = startPoint + direction * distance * 0.5f + Vector3.up * (pitchArcHeight - yOffset * 0.5f);
            Gizmos.DrawLine(startPoint, midPoint);
            Gizmos.DrawLine(midPoint, targetPoint);
        }
    }

    private void OnDestroy()
    {
        // TrailRenderer 随棒球自动销毁，无需手动清理
    }
    #endregion
}