using UnityEngine;

/// <summary>
/// 棒球控制器
/// 处理棒球碰撞、击球判定、好球区检测、投球完成逻辑
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BaseballController : MonoBehaviour
{
    [Header("Hit Settings")]
    [Tooltip("击球向上力（m/s²）")]
    public float HitUpForce = 2f;
    [Tooltip("击球力度倍率")]
    public float HitPowerMultiplier = 1.5f;
    [Tooltip("重力缩放（适配全Unity版本）")]
    public float GravityScale = 1f;

    // 外部引用
    [HideInInspector] public AutoPitcher autoPitcher;
    private AtBatCounter _gameManager;
    private Transform _strikeZoneCenter;
    private float _strikeZoneWidth;
    private float _strikeZoneHeight;

    // 状态变量
    [HideInInspector] public bool hasBeenHit = false;
    private bool _isPitchCompleted = false;
    private Rigidbody _rb;
    private Vector3 _customGravity;
    private bool _hasPassedStrikeZone = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }

        _rb.useGravity = true;
        _rb.mass = 0.145f;
        _rb.drag = 0.05f;
        _rb.angularDrag = 0.1f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        _customGravity = Physics.gravity * GravityScale;
    }

    private void FixedUpdate()
    {
        if (!_rb.useGravity) return;
        _rb.AddForce(_customGravity * _rb.mass, ForceMode.Acceleration);
    }

    /// <summary>
    /// 初始化棒球控制器（投球前调用）
    /// </summary>
    public void Initialize(AutoPitcher pitcher, AtBatCounter gameManager, Transform strikeZone, float width, float height)
    {
        hasBeenHit = false;
        _isPitchCompleted = false;
        _hasPassedStrikeZone = false;
        transform.rotation = Quaternion.identity;

        autoPitcher = pitcher;
        _gameManager = gameManager;
        _strikeZoneCenter = strikeZone;
        _strikeZoneWidth = width;
        _strikeZoneHeight = height;

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _customGravity = Physics.gravity * GravityScale;

        Debug.Log($"[BaseballController] 初始化完成，好球区：{_strikeZoneCenter.position:F2}");
    }

    /// <summary>
    /// 外部调用的击球方法（适配VRBatHitController）
    /// </summary>
    public void OnHit(float hitQuality)
    {
        if (hasBeenHit) return;
        hasBeenHit = true;

        autoPitcher?.OnBallHit(gameObject);
        _gameManager?.RecordHit(hitQuality);

        Debug.Log($"[BaseballController] 击球成功！质量：{hitQuality:F2}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.gameObject == null)
            return;

        if (!hasBeenHit && collision.gameObject.CompareTag("Bat"))
        {
            ProcessBatHit(collision);
            hasBeenHit = true;
            autoPitcher?.OnBallHit(gameObject);
            Debug.Log("球被球棒击中！");
        }

        if (!hasBeenHit && collision.gameObject.CompareTag("Ground"))
        {
            Debug.Log("棒球落地！");
            OnBallLanded();
            autoPitcher?.OnPitchCompleted();
        }
    }

    private void ProcessBatHit(Collision collision)
    {
        hasBeenHit = true;

        Vector3 batVelocity = collision.rigidbody != null ? collision.rigidbody.velocity : Vector3.forward * 10f;
        Vector3 hitDirection = (transform.position - collision.contacts[0].point).normalized;
        Vector3 finalVelocity = hitDirection * (batVelocity.magnitude * HitPowerMultiplier) + Vector3.up * HitUpForce;

        _rb.velocity = finalVelocity;
        _rb.angularVelocity = new Vector3(
            Random.Range(100f, 300f),
            Random.Range(100f, 300f),
            Random.Range(100f, 300f)
        );

        autoPitcher?.OnBallHit(gameObject);
        _gameManager?.RecordHit(Mathf.Clamp01(batVelocity.magnitude / 40f));

        Debug.Log($"[BaseballController] 击球成功！速度：{finalVelocity.magnitude:F2} m/s");
    }

    private void Update()
    {
        if (!hasBeenHit && !_isPitchCompleted && IsBallInStrikeZone())
        {
            _hasPassedStrikeZone = true;
        }

        if (!hasBeenHit && !_isPitchCompleted)
        {
            CheckPitchCompleted();
        }
    }

    private void CheckPitchCompleted()
    {
        if (_strikeZoneCenter == null) return;

        bool isBeyondStrikeZone = transform.position.z < _strikeZoneCenter.position.z - 2f;
        bool isOnGround = transform.position.y < 0f;

        if (isBeyondStrikeZone || isOnGround)
        {
            CompletePitch();
        }
    }

    private void OnBallLanded()
    {
        if (!_isPitchCompleted)
        {
            CompletePitch();
        }
    }

    private void CompletePitch()
    {
        _isPitchCompleted = true;

        if (!hasBeenHit && _strikeZoneCenter != null && _gameManager != null)
        {
            bool isStrike = IsBallInStrikeZone();
            _gameManager.RecordPitch(_hasPassedStrikeZone);
            Debug.Log($"[BaseballController] 投球完成，{(isStrike ? "好球" : "坏球")}");
        }

        autoPitcher?.OnPitchCompleted();
    }

    private bool IsBallInStrikeZone()
    {
        if (_strikeZoneCenter == null) return false;

        Vector3 localPos = _strikeZoneCenter.InverseTransformPoint(transform.position);

        bool inWidthRange = Mathf.Abs(localPos.x) <= _strikeZoneWidth / 2f;
        bool inHeightRange = Mathf.Abs(localPos.y) <= _strikeZoneHeight / 2f;
        bool inDepthRange = localPos.z > -0.5f && localPos.z < 0.5f;

        return inWidthRange && inHeightRange && inDepthRange;
    }

    public void ForceCompletePitch()
    {
        if (!_isPitchCompleted)
        {
            CompletePitch();
        }
    }

    public bool IsHit() => hasBeenHit;
    public bool IsPitchCompleted() => _isPitchCompleted;

    private void OnDrawGizmosSelected()
    {
        if (_strikeZoneCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_strikeZoneCenter.position, new Vector3(_strikeZoneWidth, _strikeZoneHeight, 1f));
        }
    }
}