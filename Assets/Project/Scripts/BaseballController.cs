using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class BaseballController : MonoBehaviour
{
    [Header("Hit Settings")]
    public float HitUpForce = 2f;
    public float HitPowerMultiplier = 1.5f;
    public float GravityScale = 1f;

    [HideInInspector] public AutoPitcher autoPitcher;
    private AtBatCounter _gameManager;
    private Transform _strikeZoneCenter;
    private float _strikeZoneWidth;
    private float _strikeZoneHeight;

    [HideInInspector] public bool hasBeenHit = false;
    private bool _isPitchCompleted = false;
    private Rigidbody _rb;
    private bool _hasPassedStrikeZone = false;

    // ★ 改为 public，方便外部读取进行无敌判定
    [HideInInspector] public float spawnTime;
    private const float INVINCIBLE_DURATION = 1.0f;   // 延长至 0.3 秒更安全

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();

        _rb.useGravity = true;
        _rb.mass = 0.145f;
        _rb.drag = 0.05f;
        _rb.angularDrag = 0.1f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void FixedUpdate()
    {
        if (!_rb.useGravity)
        {
            _rb.AddForce(Physics.gravity * GravityScale * _rb.mass, ForceMode.Acceleration);
        }
    }

    public void Initialize(AutoPitcher pitcher, AtBatCounter gameManager, Transform strikeZone, float width, float height)
    {
        spawnTime = Time.time;
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

        Debug.Log($"[BaseballController] 初始化完成，无敌至 {spawnTime + INVINCIBLE_DURATION:F2}");
    }

    public void OnHit(float hitQuality)
    {
        if (Time.time - spawnTime < INVINCIBLE_DURATION)
        {
            Debug.Log("[BaseballController] 无敌期内 OnHit 被拦截");
            return;
        }
        if (hasBeenHit) return;
        hasBeenHit = true;

        autoPitcher?.OnBallHit(gameObject);
        _gameManager?.RecordHit(hitQuality);
        Debug.Log($"[BaseballController] 击球成功！质量：{hitQuality:F2}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.gameObject == null) return;

        if (Time.time - spawnTime < INVINCIBLE_DURATION)
        {
            Debug.Log("[BaseballController] 无敌期内碰撞忽略");
            return;
        }

        if (!hasBeenHit && collision.gameObject.CompareTag("Bat"))
        {
            ProcessBatHit(collision);
            hasBeenHit = true;
            autoPitcher?.OnBallHit(gameObject);
            Debug.Log("球被球棒击中！");
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
    }

    private void Update()
    {
        if (!hasBeenHit && !_isPitchCompleted && IsBallInStrikeZone())
            _hasPassedStrikeZone = true;

        if (!hasBeenHit && !_isPitchCompleted)
            CheckPitchCompleted();
    }

    private void CheckPitchCompleted()
    {
        if (_strikeZoneCenter == null) return;
        bool isBeyondStrikeZone = transform.position.z < _strikeZoneCenter.position.z - 2f;
        bool isOnGround = transform.position.y < 0f;
        if (isBeyondStrikeZone || isOnGround)
            CompletePitch();
    }

    private void OnBallLanded()
    {
        if (!_isPitchCompleted) CompletePitch();
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
    }

    private bool IsBallInStrikeZone()
    {
        if (_strikeZoneCenter == null) return false;
        Vector3 localPos = _strikeZoneCenter.InverseTransformPoint(transform.position);
        bool inWidth = Mathf.Abs(localPos.x) <= _strikeZoneWidth / 2f;
        bool inHeight = Mathf.Abs(localPos.y) <= _strikeZoneHeight / 2f;
        bool inDepth = localPos.z > -0.5f && localPos.z < 0.5f;
        return inWidth && inHeight && inDepth;
    }

    public void ForceCompletePitch()
    {
        if (!_isPitchCompleted) CompletePitch();
    }

    public bool IsHit() => hasBeenHit;
    public bool IsPitchCompleted() => _isPitchCompleted;

    private void OnDrawGizmosSelected()
    {
        if (_strikeZoneCenter != null)
            Gizmos.DrawWireCube(_strikeZoneCenter.position, new Vector3(_strikeZoneWidth, _strikeZoneHeight, 1f));
    }
}