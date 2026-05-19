using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 游戏全局管理器：统一管理游戏核心模块、全局事件和游戏流程
/// </summary>
public class GameManager : MonoBehaviour
{
    // 单例实例（修复 Lazy 初始化的线程安全 + 场景唯一逻辑）
    private static GameManager _instance;
    private static readonly object _lock = new object();
    public static GameManager Instance
    {
        get
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<GameManager>();

                    // 场景中未找到时自动创建（可选，根据项目需求调整）
                    if (_instance == null)
                    {
                        GameObject managerObj = new GameObject("[GameManager]");
                        _instance = managerObj.AddComponent<GameManager>();
                        DontDestroyOnLoad(managerObj);
                        Debug.Log("[GameManager] 场景中未找到实例，已自动创建全局实例");
                    }
                }
                return _instance;
            }
        }
    }

    [Header("核心游戏模块（自动绑定，可手动指定）")]
    [SerializeField] private AtBatCounter _atBatCounter;
    [SerializeField] private AutoPitcher _autoPitcher;
    [SerializeField] private BaseRunningController _baseRunningController;
    [SerializeField] private DefenseAIController _defenseAIController;
    [SerializeField] private PitchCountdownUI _pitchCountdownUI;
    [SerializeField] private VRPlayerMovement _vrPlayerMovement;

    // 全局游戏事件（解耦模块间通信）
    public event Action OnHitSuccessEvent;
    public event Action OnInningEndEvent;
    public event Action OnGameEndEvent;

    // 移除冗余的公有字段（改用封装的访问器）
    // 原代码中 atBatCounter/pitchCountdownUI 公有字段与私有字段重复，已删除

    // 模块可用性校验（避免空引用）
    private readonly Dictionary<string, bool> _moduleStatus = new Dictionary<string, bool>();

    private void Awake()
    {
        // 修复单例逻辑：确保场景中唯一实例
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        AutoBindAndValidateModules(); // 自动绑定并校验模块
    }

    private void Start()
    {
        InitGame();
        BindGlobalEvents();
        LogModuleStatus(); // 打印模块状态（调试用）
    }

    /// <summary>
    /// 自动绑定模块 + 校验模块可用性（核心优化点）
    /// </summary>
    private void AutoBindAndValidateModules()
    {
        // 1. 自动绑定优先级：手动指定 > 子物体查找（包含非激活） > 全局查找
        _atBatCounter = _atBatCounter ?? GetComponentInChildren<AtBatCounter>(true);
        _autoPitcher = _autoPitcher ?? FindAnyObjectByType<AutoPitcher>();
        _baseRunningController = _baseRunningController ?? GetComponentInChildren<BaseRunningController>(true);
        _defenseAIController = _defenseAIController ?? GetComponentInChildren<DefenseAIController>(true);
        _pitchCountdownUI = _pitchCountdownUI ?? FindAnyObjectByType<PitchCountdownUI>();
        _vrPlayerMovement = _vrPlayerMovement ?? FindAnyObjectByType<VRPlayerMovement>();

        // 2. 校验模块并记录状态
        UpdateModuleStatus(nameof(_atBatCounter), _atBatCounter);
        UpdateModuleStatus(nameof(_autoPitcher), _autoPitcher);
        UpdateModuleStatus(nameof(_baseRunningController), _baseRunningController);
        UpdateModuleStatus(nameof(_defenseAIController), _defenseAIController);
        UpdateModuleStatus(nameof(_pitchCountdownUI), _pitchCountdownUI);
        UpdateModuleStatus(nameof(_vrPlayerMovement), _vrPlayerMovement);

        // 3. 空模块警告（便于调试）
        foreach (var module in _moduleStatus)
        {
            if (!module.Value)
            {
                Debug.LogWarning($"[GameManager] 模块 {module.Key} 未找到！请检查预制体或场景配置");
            }
        }
    }

    /// <summary>
    /// 辅助方法：更新模块状态（简化重复代码）
    /// </summary>
    private void UpdateModuleStatus(string moduleName, object moduleInstance)
    {
        if (_moduleStatus.ContainsKey(moduleName))
        {
            _moduleStatus[moduleName] = moduleInstance != null;
        }
        else
        {
            _moduleStatus.Add(moduleName, moduleInstance != null);
        }
    }

    /// <summary>
    /// 初始化游戏（重置所有模块状态）
    /// </summary>
    public void InitGame()
    {
        TryExecuteModuleAction(() => _atBatCounter?.ResetInning(), nameof(_atBatCounter));
        TryExecuteModuleAction(() => _baseRunningController?.ResetRunningState(), nameof(_baseRunningController));
        TryExecuteModuleAction(() => _defenseAIController?.ResetDefense(), nameof(_defenseAIController));
        TryExecuteModuleAction(() => _vrPlayerMovement?.SetMovementActive(false), nameof(_vrPlayerMovement));

        Debug.Log("[GameManager] 游戏初始化完成，等待投球开始");
    }

    /// <summary>
    /// 绑定全局事件（解耦原有的直接监听，改用事件委托）
    /// </summary>
    private void BindGlobalEvents()
    {
        // 原有模块事件转发到全局事件（增加空引用保护）
        _atBatCounter?.OnHitSuccess?.AddListener(TriggerHitSuccess);
        _atBatCounter?.OnInningEnd?.AddListener(TriggerInningEnd);
        _atBatCounter?.OnGameEnd?.AddListener(TriggerGameEnd);
    }

    // 触发全局事件（封装事件调用逻辑）
    private void TriggerHitSuccess()
    {
        OnHitSuccessEvent?.Invoke();
        OnHitSuccessHandler();
    }

    private void TriggerInningEnd()
    {
        OnInningEndEvent?.Invoke();
        OnInningEndHandler();
    }

    private void TriggerGameEnd()
    {
        OnGameEndEvent?.Invoke();
        OnGameEndHandler();
    }

    /// <summary>
    /// 击球成功处理逻辑
    /// </summary>
    private void OnHitSuccessHandler()
    {
        TryExecuteModuleAction(() => _vrPlayerMovement?.SetMovementActive(true), nameof(_vrPlayerMovement));
        TryExecuteModuleAction(() => _baseRunningController?.StartRunningToNextBase(), nameof(_baseRunningController));
    }

    /// <summary>
    /// 半局结束处理逻辑
    /// </summary>
    private void OnInningEndHandler()
    {
        TryExecuteModuleAction(() => _vrPlayerMovement?.SetMovementActive(false), nameof(_vrPlayerMovement));
        TryExecuteModuleAction(() => _baseRunningController?.ResetRunningState(), nameof(_baseRunningController));
        TryExecuteModuleAction(() => _defenseAIController?.ResetDefense(), nameof(_defenseAIController));
      
    }

    /// <summary>
    /// 游戏结束处理逻辑
    /// </summary>
    private void OnGameEndHandler()
    {
        TryExecuteModuleAction(() => _vrPlayerMovement?.SetMovementActive(false), nameof(_vrPlayerMovement));
        TryExecuteModuleAction(() => _atBatCounter?.DisablePlate(), nameof(_atBatCounter));
        TryExecuteModuleAction(() => _pitchCountdownUI?.Hide(), nameof(_pitchCountdownUI));
    }

    /// <summary>
    /// 安全执行模块方法（防止空引用异常 + 异常捕获）
    /// </summary>
    /// <param name="action">要执行的模块方法</param>
    /// <param name="moduleName">模块名称（用于日志）</param>
    private void TryExecuteModuleAction(Action action, string moduleName)
    {
        if (action == null)
        {
            Debug.LogWarning($"[GameManager] 模块 {moduleName} 执行动作为空！");
            return;
        }

        if (_moduleStatus.TryGetValue(moduleName, out bool isAvailable) && isAvailable)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] 执行 {moduleName} 方法失败：{e.Message}\n{e.StackTrace}");
            }
        }
        else
        {
            Debug.LogWarning($"[GameManager] 模块 {moduleName} 不可用，跳过方法执行");
        }
    }

    /// <summary>
    /// 打印模块状态（调试用）
    /// </summary>
    private void LogModuleStatus()
    {
        string log = "[GameManager] 模块状态：\n";
        foreach (var module in _moduleStatus)
        {
            log += $"- {module.Key}: {(module.Value ? "✅ 可用" : "❌ 缺失")}\n";
        }
        Debug.Log(log);
    }

    /// <summary>
    /// 清理事件监听（防止内存泄漏）
    /// </summary>
    private void OnDestroy()
    {
        // 移除所有事件监听（增加空引用保护）
        _atBatCounter?.OnHitSuccess?.RemoveListener(TriggerHitSuccess);
        _atBatCounter?.OnInningEnd?.RemoveListener(TriggerInningEnd);
        _atBatCounter?.OnGameEnd?.RemoveListener(TriggerGameEnd);

        // 清空全局事件（防止空引用回调）
        OnHitSuccessEvent = null;
        OnInningEndEvent = null;
        OnGameEndEvent = null;

        // 单例实例置空（避免销毁后引用失效）
        if (_instance == this)
        {
            _instance = null;
        }
    }

    // ========== 对外暴露的模块访问器（封装内部字段，提高可控性） ==========
    public AtBatCounter AtBatCounter => _atBatCounter;
    public AutoPitcher AutoPitcher => _autoPitcher;
    public BaseRunningController BaseRunningController => _baseRunningController;
    public DefenseAIController DefenseAIController => _defenseAIController;
    public PitchCountdownUI PitchCountdownUI => _pitchCountdownUI;
    public VRPlayerMovement VRPlayerMovement => _vrPlayerMovement;

    // ========== 对外暴露的游戏流程控制方法（统一入口） ==========
    /// <summary>
    /// 手动触发游戏结束（外部模块调用）
    /// </summary>
    public void ManualTriggerGameEnd()
    {
        TriggerGameEnd();
    }

    /// <summary>
    /// 检查所有核心模块是否就绪
    /// </summary>
    public bool IsAllCoreModulesReady()
    {
        foreach (var status in _moduleStatus.Values)
        {
            if (!status) return false;
        }
        return true;
    }

    /// <summary>
    /// 手动刷新模块绑定（用于动态加载场景后重新绑定）
    /// </summary>
    public void RefreshModuleBinding()
    {
        AutoBindAndValidateModules();
        LogModuleStatus();
        Debug.Log("[GameManager] 模块绑定已刷新");
    }   
}