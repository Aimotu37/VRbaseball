using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.InputSystem;

public class AutoPitcher : MonoBehaviour
{
    [Header("输入系统配置")]
    public InputActionAsset inputActions;
    private InputAction _vrPitchAction;
    private InputAction _debugPitchAction;

    [Header("投球基础配置")]
    public GameObject baseballPrefab;
    public float pitchInterval = 5f;
    [Range(20f, 45f)] public float pitchSpeed = 28f;
    [Range(0.05f, 0.2f)] public float pitchVariance = 0.1f;
    public float pitchArcHeight = 0.3f;

    [Header("好球区配置")]
    public Transform strikeZoneCenter;
    public float strikeZoneWidth = 0.6f;
    public float strikeZoneHeight = 0.9f;
    public bool forceStrikeZone = true;
    [Range(0f, 1f)] public float strikeZoneVarianceRatio = 0.3f;

    [Header("系统配置")]
    public AtBatCounter gameManager;
    public PitchCountdownUI countdownUI;
    public UnityEvent onPitchComplete;
    public UnityEvent<GameObject> onBallHit;

    [Header("VR流程控制")]
    public bool forceAutoPitch = false;
    private bool _isWaitingForStart = true;
    private bool _isCountingDown = false;

    [Header("棒球拖尾特效")]
    public float pitchTrailTime = 0.5f;
    public float hitTrailTime = 1.5f;
    public float pitchTrailWidth = 0.04f;
    public float hitTrailStartWidth = 0.08f;
    public float hitTrailEndWidth = 0.02f;
    public Color baseballTrajectoryColor = Color.cyan;
    public Color hitTrajectoryColor = Color.red;

    [Header("动画配置")]
    public Animator pitcherAnimator;

    private float _gravity = Physics.gravity.y;
    private bool _isVRMode = true;

    void Awake()
    {
        _isVRMode = UnityEngine.XR.XRSettings.isDeviceActive;
        InitInput();
        if (pitcherAnimator == null)
            pitcherAnimator = GetComponent<Animator>();
    }

    void Start()
    {
        if (forceAutoPitch)
            StartCoroutine(AutoPitchLoop());
    }

    #region 输入初始化
    private void InitInput()
    {
        if (inputActions == null) return;
        var map = inputActions.FindActionMap("PitchControls");
        if (map == null) return;
        _vrPitchAction = map.FindAction("VRPitch");
        _debugPitchAction = map.FindAction("DebugPitch");
        if (_vrPitchAction != null)
            _vrPitchAction.performed += OnVRPitchPerformed;
        if (_debugPitchAction != null)
            _debugPitchAction.performed += OnDebugPitchPerformed;
    }

    private void OnVRPitchPerformed(InputAction.CallbackContext ctx)
    {
        if (_isWaitingForStart && !_isCountingDown)
            OnStartButtonClicked();
    }

    private void OnDebugPitchPerformed(InputAction.CallbackContext ctx)
    {
        if (_isWaitingForStart && !_isCountingDown)
            ManualPitch();
    }

    void OnEnable() { _vrPitchAction?.Enable(); _debugPitchAction?.Enable(); }
    void OnDisable()
    {
        _vrPitchAction?.Disable(); _debugPitchAction?.Disable();
        if (_vrPitchAction != null) _vrPitchAction.performed -= OnVRPitchPerformed;
        if (_debugPitchAction != null) _debugPitchAction.performed -= OnDebugPitchPerformed;
    }
    #endregion

    #region 拖尾特效
    private void CreateBaseballTrajectory(GameObject baseball, bool isHit = false)
    {
        TrailRenderer old = baseball.GetComponent<TrailRenderer>();
        if (old) Destroy(old);
        TrailRenderer t = baseball.AddComponent<TrailRenderer>();
        t.time = isHit ? hitTrailTime : pitchTrailTime;
        t.minVertexDistance = 0.1f;
        t.autodestruct = false;

        AnimationCurve wCurve = new AnimationCurve();
        if (isHit)
        {
            wCurve.AddKey(0, hitTrailStartWidth);
            wCurve.AddKey(0.3f, hitTrailStartWidth * 0.7f);
            wCurve.AddKey(1, hitTrailEndWidth);
        }
        else
        {
            wCurve.AddKey(0, pitchTrailWidth);
            wCurve.AddKey(1, 0);
        }
        t.widthCurve = wCurve;

        Gradient g = new Gradient();
        if (isHit)
        {
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(hitTrajectoryColor, 0), new GradientColorKey(new Color(1, 0.5f, 0), 0.7f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }
            );
        }
        else
        {
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(baseballTrajectoryColor, 0), new GradientColorKey(baseballTrajectoryColor, 1) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) }
            );
        }
        t.colorGradient = g;

        Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
        mat.SetFloat("_Mode", 2);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", isHit ? hitTrajectoryColor * 2f : baseballTrajectoryColor * 1.5f);
        t.material = mat;
        t.alignment = LineAlignment.View;
    }

    public void OnBallHit(GameObject baseball)
    {
        CreateBaseballTrajectory(baseball, true);
        onBallHit?.Invoke(baseball);
    }
    #endregion

    #region 投球逻辑
    public void OnStartButtonClicked()
    {
        if (_isWaitingForStart && !_isCountingDown)
        {
            if (countdownUI != null && _isVRMode)
                StartCoroutine(StartPitchCountdown());
            else
                TriggerPitchLogic();
        }
    }

    private IEnumerator StartPitchCountdown()
    {
        _isCountingDown = true;
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

    // 由动画事件在 2 秒时调用
    public void OnPitchAnimationEvent()
    {
        ThrowPitch();
    }

    void ThrowPitch()
    {
        // 生成球（使用预制体或动态创建）
        if (baseballPrefab == null)
        {
            // ...动态创建逻辑（保留之前的，此处省略）
            return;
        }

        Vector3 spawnPos = transform.position + new Vector3(0, 0.5f, 2.5f); // 前移远离模型
        GameObject ball = Instantiate(baseballPrefab, spawnPos, Quaternion.identity);
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb == null) rb = ball.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 0.145f;
        rb.drag = 0.005f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        BaseballController bc = ball.GetComponent<BaseballController>();
        if (bc == null) bc = ball.AddComponent<BaseballController>();
        bc.Initialize(this, gameManager, strikeZoneCenter, strikeZoneWidth, strikeZoneHeight);

        CreateBaseballTrajectory(ball, false);

        Vector3 target = strikeZoneCenter.position;
        if (forceStrikeZone)
        {
            target = new Vector3(
                strikeZoneCenter.position.x + Random.Range(-strikeZoneWidth / 2 * strikeZoneVarianceRatio, strikeZoneWidth / 2 * strikeZoneVarianceRatio),
                strikeZoneCenter.position.y + Random.Range(-strikeZoneHeight / 2 * strikeZoneVarianceRatio, strikeZoneHeight / 2 * strikeZoneVarianceRatio),
                strikeZoneCenter.position.z
            );
        }

        Vector3 hStart = new Vector3(spawnPos.x, 0, spawnPos.z);
        Vector3 hTarget = new Vector3(target.x, 0, target.z);
        float hDist = Vector3.Distance(hStart, hTarget);
        if (hDist < 5f) hDist = 8f;

        float flightTime = Mathf.Clamp(hDist / pitchSpeed, 0.8f, 2f);
        float hSpeed = hDist / flightTime;
        float absGrav = Mathf.Abs(_gravity);
        float yDiff = target.y - spawnPos.y;
        float vVel = (yDiff + 0.5f * absGrav * flightTime * flightTime) / flightTime + pitchArcHeight;

        Vector3 hDir = (hTarget - hStart).normalized;
        Vector3 finalVel = hDir * hSpeed + Vector3.up * vVel;

        // 延迟启用碰撞器避免出生重叠
        Collider[] cols = ball.GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        StartCoroutine(ActivateBall(ball, cols, finalVel));

        Destroy(ball, 5f);
        _isWaitingForStart = false;
        onPitchComplete?.Invoke();   // 注意：这里直接标记投球完成，允许下一次按X
        _isWaitingForStart = true;   // 投球后可立即再次投球（可根据需要调整）
    }

    private IEnumerator ActivateBall(GameObject ball, Collider[] cols, Vector3 vel)
    {
        yield return new WaitForFixedUpdate();
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        rb.velocity = vel;
        rb.angularVelocity = new Vector3(Random.Range(50, 80), Random.Range(50, 80), Random.Range(50, 80));
        foreach (var c in cols) c.enabled = true;
    }

    public void ManualPitch()
    {
        if (_isWaitingForStart)
            OnStartButtonClicked();
    }

    private IEnumerator AutoPitchLoop()
    {
        while (forceAutoPitch)
        {
            yield return new WaitUntil(() => _isWaitingForStart);
            yield return new WaitForSeconds(0.2f);
            TriggerPitchLogic();
            yield return new WaitForSeconds(pitchInterval);
        }
    }
    #endregion
}