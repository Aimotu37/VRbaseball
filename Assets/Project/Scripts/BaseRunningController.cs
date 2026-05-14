using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 跑垒控制器：垒包管理、跑垒状态、上垒/出局判定
/// </summary>
public class BaseRunningController : MonoBehaviour
{
    [Header("垒包配置")]
    [Tooltip("0:本垒 1:一垒 2:二垒 3:三垒，按顺序配置")]
    public Transform[] bases;

    [Header("跑垒参数")]
    public float runSpeed = 5f;
    public float baseJudgeRadius = 0.5f;

    [Header("跑垒状态")]
    public List<int> runnersOnBase = new List<int>();
    public int currentRunnerBase = 0;
    public bool isRunning = false;

    [Header("事件")]
    public UnityEvent OnSafe;
    public UnityEvent OnOut;
    public UnityEvent OnHomeRun;

    private CharacterController _playerController;
    private Transform _playerTransform;
    private int _targetBase = 0;

    void Awake()
    {
        // 自动获取XR玩家控制器
        GameObject xrRig = GameObject.Find("XR Origin (XR Rig)");
        if (xrRig != null)
        {
            _playerTransform = xrRig.transform;
            _playerController = xrRig.GetComponent<CharacterController>();

            // 自动添加CharacterController
            if (_playerController == null)
            {
                _playerController = xrRig.AddComponent<CharacterController>();
                _playerController.height = 1.8f;
                _playerController.radius = 0.3f;
                _playerController.center = new Vector3(0, 0.9f, 0);
                Debug.LogWarning("[BaseRunning] XR Rig缺少CharacterController，已自动添加");
            }
        }
        else
        {
            Debug.LogError("[BaseRunning] 未找到XR Origin (XR Rig)，跑垒功能无法正常工作！");
        }

        // 垒包配置校验
        if (bases == null || bases.Length != 4)
        {
            Debug.LogError("[BaseRunning] 请配置4个垒包（本垒、一垒、二垒、三垒）！");
        }
    }

    /// <summary>
    /// 保送上垒（四坏球调用）
    /// </summary>
    public void WalkToBase(int targetBase)
    {
        currentRunnerBase = targetBase;
        if (!runnersOnBase.Contains(targetBase))
        {
            runnersOnBase.Add(targetBase);
        }
        OnSafe?.Invoke();
        Debug.Log($"保送上垒到{targetBase}垒");
    }

    /// <summary>
    /// 开始跑向下一垒（击球成功后调用）
    /// </summary>
    public void StartRunningToNextBase()
    {
        if (isRunning || bases == null || bases.Length != 4) return;

        _targetBase = currentRunnerBase + 1;
        // 超过三垒则跑回本垒
        if (_targetBase > 3) _targetBase = 0;

        isRunning = true;
        Debug.Log($"开始跑向{_targetBase}垒");
    }

    void Update()
    {
        if (!isRunning || _playerTransform == null || _playerController == null) return;
        if (bases == null || bases[_targetBase] == null) return;

        // 计算跑垒方向（忽略Y轴，保持水平移动）
        Vector3 targetPos = bases[_targetBase].position;
        Vector3 runDir = (targetPos - _playerTransform.position).normalized;
        runDir.y = 0;

        // 执行移动
        _playerController.Move(runDir * runSpeed * Time.deltaTime);

        // 到达垒包判定
        float distanceToBase = Vector3.Distance(_playerTransform.position, targetPos);
        if (distanceToBase <= baseJudgeRadius)
        {
            OnReachBase();
        }
    }

    /// <summary>
    /// 到达垒包处理逻辑
    /// </summary>
    private void OnReachBase()
    {
        isRunning = false;
        currentRunnerBase = _targetBase;

        // 跑回本垒（全垒打）
        if (currentRunnerBase == 0)
        {
            OnHomeRun?.Invoke();
            runnersOnBase.Clear();
            Debug.Log("全垒打！跑回本垒得分");
        }
        else
        {
            OnSafe?.Invoke();
            if (!runnersOnBase.Contains(currentRunnerBase))
            {
                runnersOnBase.Add(currentRunnerBase);
            }
            Debug.Log($"安全上垒到{currentRunnerBase}垒");
        }

        // 移除上一个垒包的记录
        int prevBase = currentRunnerBase - 1;
        if (prevBase < 0) prevBase = 3;
        runnersOnBase.Remove(prevBase);
    }

    /// <summary>
    /// 跑垒员出局（防守AI调用）
    /// </summary>
    public void RunnerOut()
    {
        isRunning = false;
        OnOut?.Invoke();

        // 修复：匹配修正后的GameManager和AtBatCounter，解决gameManager字段缺失报错
        GameManager.Instance?.AtBatCounter?.AddOut();

        Debug.Log("跑垒员出局！");
        ResetRunningState();
    }

    /// <summary>
    /// 重置跑垒状态（局结束/游戏重置时调用）
    /// </summary>
    public void ResetRunningState()
    {
        isRunning = false;
        currentRunnerBase = 0;
        _targetBase = 0;
        runnersOnBase.Clear();
        CancelInvoke();
        Debug.Log("[BaseRunning] 跑垒状态已重置");
    }
}