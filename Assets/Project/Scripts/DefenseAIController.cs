using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 防守AI控制器：防守队员管理、接球、传球、出局判定
/// </summary>
public class DefenseAIController : MonoBehaviour
{
    [Header("防守队员配置")]
    public List<Defender> defenders = new List<Defender>();

    [Header("AI参数")]
    public float moveSpeed = 6f;
    public float catchRadius = 0.8f;
    public float throwSpeed = 15f;
    public float reactionTime = 0.2f;

    private Transform _ballTransform;
    private bool _isDefending = false;
    private Defender _nearestDefender;
    private Vector3 _targetBallPosition; // 新增：缓存球的目标位置，解决移动不持续的问题

    void Awake()
    {
        InitDefenders();
    }

    private void InitDefenders()
    {
        if (defenders.Count == 0)
        {
            Debug.LogWarning("请在场景中放置防守队员，拖入defenders列表");
        }
        else
        {
            foreach (var defender in defenders)
            {
                defender.defaultPosition = defender.transform.position;
            }
        }
    }

    /// <summary>
    /// 球落地后触发防守逻辑（外部击球事件调用）
    /// </summary>
    public void OnBallLanded(Vector3 ballPosition)
    {
        // 修复：棒球查找空引用保护
        GameObject baseballObj = GameObject.FindGameObjectWithTag("Baseball");
        if (baseballObj == null)
        {
            Debug.LogError("[DefenseAI] 未找到标签为Baseball的棒球对象！");
            return;
        }

        _ballTransform = baseballObj.transform;
        _targetBallPosition = ballPosition;
        _isDefending = true;
        _nearestDefender = GetNearestDefender(ballPosition);

        if (_nearestDefender != null)
        {
            Debug.Log($"防守队员{_nearestDefender.defenderName}前往接球");
        }
        else
        {
            Debug.LogWarning("[DefenseAI] 未找到可用的防守队员！");
            _isDefending = false;
        }
    }

    void FixedUpdate()
    {
        // 防守状态校验
        if (!_isDefending || _ballTransform == null || _nearestDefender == null) return;
        // 已接住球则停止移动
        if (_nearestDefender.hasCaughtBall) return;

        // 修复：持续移动逻辑（原逻辑只调用一次移动，不会持续追踪球）
        _nearestDefender.StartMoveToBall(_targetBallPosition, moveSpeed);

        // 接球判定
        float distanceToBall = Vector3.Distance(_nearestDefender.transform.position, _ballTransform.position);
        if (distanceToBall <= catchRadius)
        {
            CatchBall();
        }
    }

    private void CatchBall()
    {
        _nearestDefender.hasCaughtBall = true;
        Debug.Log($"防守队员{_nearestDefender.defenderName}接住球！");

        int targetBase = GetOutTargetBase();
        if (targetBase != -1)
        {
            ThrowBallToBase(targetBase);
        }
        else
        {
            _isDefending = false;
            Debug.Log("[DefenseAI] 无有效传球目标，防守结束");
        }
    }

    private void ThrowBallToBase(int baseIndex)
    {
        // 修复：匹配GameManager的对外访问器，解决baseRunningController字段缺失报错
        if (GameManager.Instance == null || GameManager.Instance.BaseRunningController == null)
        {
            Debug.LogError("[DefenseAI] GameManager或跑垒控制器未找到！");
            _isDefending = false;
            return;
        }

        Transform targetBase = GameManager.Instance.BaseRunningController.bases[baseIndex];
        Rigidbody ballRb = _ballTransform.GetComponent<Rigidbody>();

        if (ballRb == null || targetBase == null)
        {
            Debug.LogError("[DefenseAI] 传球目标或棒球刚体缺失！");
            _isDefending = false;
            return;
        }

        // 计算传球方向，增加向上偏移避免贴地
        Vector3 throwDir = (targetBase.position + Vector3.up * 0.5f - _ballTransform.position).normalized;
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
        ballRb.AddForce(throwDir * throwSpeed, ForceMode.Impulse);

        Debug.Log($"传球到{baseIndex}垒！");
        Invoke(nameof(JudgeOutAtBase), 0.5f);
    }

    private void JudgeOutAtBase()
    {
        int targetBase = GetOutTargetBase();
        // 修复：匹配GameManager的对外访问器
        var runningCtrl = GameManager.Instance?.BaseRunningController;

        if (runningCtrl == null)
        {
            Debug.LogError("[DefenseAI] 跑垒控制器未找到，出局判定失败");
            _isDefending = false;
            return;
        }

        // 出局判定：跑垒员未到达目标垒则出局
        if (runningCtrl.currentRunnerBase < targetBase)
        {
            runningCtrl.RunnerOut();
        }
        else
        {
            Debug.Log("安全上垒！");
        }

        _isDefending = false;
    }

    private Defender GetNearestDefender(Vector3 ballPos)
    {
        Defender nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (var defender in defenders)
        {
            if (defender.transform == null) continue;

            float distance = Vector3.Distance(defender.transform.position, ballPos);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = defender;
            }
        }
        return nearest;
    }

    private int GetOutTargetBase()
    {
        // 修复：匹配GameManager的对外访问器
        var runningCtrl = GameManager.Instance?.BaseRunningController;
        if (runningCtrl == null) return -1;

        // 优先传一垒封杀，再按垒包顺序封杀
        if (!runningCtrl.runnersOnBase.Contains(1)) return 1;
        else if (!runningCtrl.runnersOnBase.Contains(2)) return 2;
        else if (!runningCtrl.runnersOnBase.Contains(3)) return 3;
        else return 0; // 本垒封杀
    }

    /// <summary>
    /// 重置防守状态（局结束/游戏重置时调用）
    /// </summary>
    public void ResetDefense()
    {
        _isDefending = false;
        _nearestDefender = null;
        _ballTransform = null;
        CancelInvoke(); // 取消所有延迟调用

        foreach (var defender in defenders)
        {
            defender.ResetDefender();
        }

        Debug.Log("[DefenseAI] 防守状态已重置");
    }
}

[System.Serializable]
public class Defender
{
    public string defenderName;
    public Transform transform;
    [HideInInspector] public Vector3 defaultPosition;
    public bool hasCaughtBall = false;

    /// <summary>
    /// 移动到球的位置（每帧调用持续移动）
    /// </summary>
    public void StartMoveToBall(Vector3 ballPos, float speed)
    {
        if (transform == null) return;

        // 面向目标位置
        transform.LookAt(new Vector3(ballPos.x, transform.position.y, ballPos.z));
        // 持续移动
        transform.position = Vector3.MoveTowards(transform.position, ballPos, speed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// 重置防守队员到初始位置
    /// </summary>
    public void ResetDefender()
    {
        if (transform != null)
        {
            transform.position = defaultPosition;
        }
        hasCaughtBall = false;
    }
}