using UnityEngine;

/// <summary>
/// 棒球控制器
/// 处理棒球碰撞、击球判定、好球区检测、投球完成逻辑
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BaseballController0 : MonoBehaviour
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

    // 状态变量（适配VRBatHitController，统一命名）
    [HideInInspector] public bool hasBeenHit = false; // 补全缺失字段
    private bool _isPitchCompleted = false;
    private Rigidbody _rb;
    private Vector3 _customGravity; // 兼容低版本Unity重力缩放
    private bool _hasPassedStrikeZone = false;
    private void Awake()
    {
        // 初始化刚体组件
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
        }

        // 初始化刚体默认参数
        _rb.useGravity = true;
        _rb.mass = 0.145f; // 标准棒球重量
        _rb.drag = 0.05f;
        _rb.angularDrag = 0.1f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // 防止穿模

        // 初始化自定义重力
        _customGravity = Physics.gravity * GravityScale;
    }

    private void FixedUpdate()
    {
        // 兼容低版本Unity的重力缩放（替代gravityScale属性）
        if (!_rb.useGravity) return;
        _rb.AddForce(_customGravity * _rb.mass, ForceMode.Acceleration);
    }

    /// <summary>
    /// 初始化棒球控制器（投球前调用）
    /// </summary>
    public void Initialize(AutoPitcher pitcher, AtBatCounter gameManager, Transform strikeZone, float width, float height)
    {
        // 重置状态
        hasBeenHit = false;
        _isPitchCompleted = false;
        _hasPassedStrikeZone = false;
        transform.rotation = Quaternion.identity;

        // 赋值外部引用
        autoPitcher = pitcher;
        _gameManager = gameManager;
        _strikeZoneCenter = strikeZone;
        _strikeZoneWidth = width;
        _strikeZoneHeight = height;

        // 重置刚体状态
        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // 更新重力缩放
        _customGravity = Physics.gravity * GravityScale;

        Debug.Log($"[BaseballController] 初始化完成，好球区：{_strikeZoneCenter.position:F2}");
    }

    /// <summary>
    /// 新增：外部调用的击球方法（适配VRBatHitController）
    /// </summary>
    /// <param name="hitQuality">击球质量（0~1）</param>
    public void OnHit(float hitQuality)
    {
        if (hasBeenHit) return;
        hasBeenHit = true;

        // 通知投球控制器击球命中
        autoPitcher?.OnBallHit(gameObject);
        // 通知打席计数器击球成功
        _gameManager?.RecordHit(hitQuality);

        Debug.Log($"[BaseballController] 击球成功！质量：{hitQuality:F2}");
    }

    /// <summary>
    /// 碰撞检测（击球/落地判定）
    /// </summary>
    private void OnCollisionEnter(Collision collision)
    {
        // 防御性判断：防止空对象报错
        if (collision == null || collision.gameObject == null)
            return;

        // 击球判定：仅未被击中时，才响应球棒碰撞
        if (!hasBeenHit && collision.gameObject.CompareTag("Bat"))
        {
            ProcessBatHit(collision);
            hasBeenHit = true;
            autoPitcher?.OnBallHit(gameObject);
            Debug.Log("球被球棒击中！");
        }

       
    }

    /// <summary>
    /// 处理击球逻辑
    /// </summary>
    private void ProcessBatHit(Collision collision)
    {
        hasBeenHit = true;

        // 获取球棒速度（无刚体时使用默认速度）
        Vector3 batVelocity = collision.rigidbody != null ? collision.rigidbody.velocity : Vector3.forward * 10f;
        // 计算击球方向（从球棒指向棒球）
        Vector3 hitDirection = (transform.position - collision.contacts[0].point).normalized;
        // 计算最终击球速度
        Vector3 finalVelocity = hitDirection * (batVelocity.magnitude * HitPowerMultiplier) + Vector3.up * HitUpForce;

        // 应用击球速度
        _rb.velocity = finalVelocity;
        _rb.angularVelocity = new Vector3(
            Random.Range(100f, 300f),
            Random.Range(100f, 300f),
            Random.Range(100f, 300f)
        );

        // 通知投球控制器击球命中
        autoPitcher?.OnBallHit(gameObject);
        // 通知打席计数器击球成功
        _gameManager?.RecordHit(Mathf.Clamp01(batVelocity.magnitude / 40f));

        Debug.Log($"[BaseballController] 击球成功！速度：{finalVelocity.magnitude:F2} m/s");
    }

    private void Update()
    {
        if (!hasBeenHit && !_isPitchCompleted && IsBallInStrikeZone())
        {
            _hasPassedStrikeZone = true;
        }
        // 投球完成判定（未被击中且未完成时）
        if (!hasBeenHit && !_isPitchCompleted)
        {
            CheckPitchCompleted();
        }

    }

    /// <summary>
    /// 判定投球是否完成（超出好球区/落地）
    /// </summary>
    private void CheckPitchCompleted()
    {
        if (_strikeZoneCenter == null) return;

        // 判定条件：1. 超出好球区后方2米  2. 落地（Y<0）
        bool isBeyondStrikeZone = transform.position.z < _strikeZoneCenter.position.z - 2f;
        bool isOnGround = transform.position.y < 0f;

        if (isBeyondStrikeZone || isOnGround)
        {
            CompletePitch();
        }
    }

    /// <summary>
    /// 棒球落地回调
    /// </summary>
    private void OnBallLanded()
    {
        if (!_isPitchCompleted)
        {
            CompletePitch();
        }
    }

    /// <summary>
    /// 完成投球（判定好球/坏球）
    /// </summary>
    private void CompletePitch()
    {
        _isPitchCompleted = true;

        // 未被击中时判定好球/坏球
        if (!hasBeenHit && _strikeZoneCenter != null && _gameManager != null)
        {
            bool isStrike = IsBallInStrikeZone();
            _gameManager.RecordPitch(_hasPassedStrikeZone);
            Debug.Log($"[BaseballController] 投球完成，{(isStrike ? "好球" : "坏球")}");
        }

        // 通知投球控制器投球完
    }

    /// <summary>
    /// 判定棒球是否在好球区内
    /// </summary>
    private bool IsBallInStrikeZone()
    {
        if (_strikeZoneCenter == null) return false;

        // 转换为好球区本地坐标
        Vector3 localPos = _strikeZoneCenter.InverseTransformPoint(transform.position);

        // 判定X（宽）、Y（高）、Z（深度）是否在好球区内
        bool inWidthRange = Mathf.Abs(localPos.x) <= _strikeZoneWidth / 2f;
        bool inHeightRange = Mathf.Abs(localPos.y) <= _strikeZoneHeight / 2f;
        bool inDepthRange = localPos.z > -0.5f && localPos.z < 0.5f;

        return inWidthRange && inHeightRange && inDepthRange;
    }

    /// <summary>
    /// 外部强制完成投球
    /// </summary>
    public void ForceCompletePitch()
    {
        if (!_isPitchCompleted)
        {
            CompletePitch();
        }
    }

    #region 状态获取
    /// <summary>
    /// 是否已被击中
    /// </summary>
    public bool IsHit() => hasBeenHit;
    /// <summary>
    /// 投球是否完成
    /// </summary>
    public bool IsPitchCompleted() => _isPitchCompleted;
    #endregion

    /// <summary>
    /// 场景视图绘制好球区（调试用）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (_strikeZoneCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_strikeZoneCenter.position, new Vector3(_strikeZoneWidth, _strikeZoneHeight, 1f));
        }
    }
}