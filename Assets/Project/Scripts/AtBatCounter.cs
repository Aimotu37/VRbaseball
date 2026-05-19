using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 打席计数器 - 记录好球、坏球、击球数，并实时更新 UI (TextMeshPro)
/// </summary>
public class AtBatCounter : MonoBehaviour
{
    [Header("计数配置")]
    public int strikes = 0;      // 好球数
    public int balls = 0;        // 坏球数
    public int hits = 0;         // 击球数
    public int outs = 0;         // 出局数
    public int currentInning = 1;// 当前局数
    public int homeScore = 0;    // 主队得分
    public int guestScore = 0;   // 客队得分
    public bool isTopInning = true; // 是否为上半局

    [Header("规则配置")]
    public int maxStrikes = 3;   // 最大好球数（出局）
    public int maxBalls = 4;     // 最大坏球数（保送上垒）
    public int maxOutsPerInning = 3; // 每半局最大出局数

    [Header("UI 文本 (TextMeshPro)")]
    [SerializeField] private TMP_Text strikeText;
    [SerializeField] private TMP_Text ballText;
    [SerializeField] private TMP_Text outText;
    [SerializeField] private TMP_Text timeText;   // 显示局数 / 上下半局
    [SerializeField] private TMP_Text homeText;   // 主队得分
    [SerializeField] private TMP_Text guestText;  // 客队得分

    [Header("全局事件（适配GameManager）")]
    public UnityEvent OnHitSuccess;    // 击球成功事件
    public UnityEvent OnInningEnd;     // 半局结束事件
    public UnityEvent OnGameEnd;       // 游戏结束事件

    // 打席激活状态（适配VRBatHitController）
    [SerializeField] private bool _isPlateActive = true;
    public bool IsPlateActive => _isPlateActive;

    private void Awake()
    {
        // 事件初始化，避免空引用
        OnHitSuccess ??= new UnityEvent();
        OnInningEnd ??= new UnityEvent();
        OnGameEnd ??= new UnityEvent();
        UpdateAllUI(); // 初始显示
    }

    private void Start()
    {
        UpdateAllUI();
    }

    /// <summary>
    /// 统一刷新所有 UI 文本
    /// </summary>
    private void UpdateAllUI()
    {
        if (strikeText != null)
            strikeText.text = strikes.ToString();
        if (ballText != null)
            ballText.text = balls.ToString();
        if (outText != null)
            outText.text = outs.ToString();
        if (timeText != null)
            timeText.text = $"{(isTopInning ? "Top" : "Bot")} {currentInning}";
        if (homeText != null)
            homeText.text = homeScore.ToString();
        if (guestText != null)
            guestText.text = guestScore.ToString();
    }

    /// <summary>
    /// 增加主队 / 客队得分（由跑垒控制器等调用）
    /// </summary>
    public void AddRun(bool isHome)
    {
        if (isHome)
            homeScore++;
        else
            guestScore++;
        UpdateAllUI();
        Debug.Log($"[AtBatCounter] 得分！{(isHome ? "主队" : "客队")}：{(isHome ? homeScore : guestScore)}");
    }

    /// <summary>
    /// 重置局数
    /// </summary>
    public void ResetInning()
    {
        strikes = 0;
        balls = 0;
        outs = 0;
        hits = 0;
        _isPlateActive = true;
        Debug.Log($"[AtBatCounter] 局数重置，当前局：{currentInning}");
        UpdateAllUI();
    }

    /// <summary>
    /// 记录投球（好球/坏球）
    /// </summary>
    /// <param name="isStrike">是否好球</param>
    public void RecordPitch(bool isStrike)
    {
        if (!_isPlateActive) return;

        if (isStrike)
        {
            strikes++;
            Debug.Log($"[AtBatCounter] 好球！累计：{strikes}/{maxStrikes}");

            if (strikes >= maxStrikes)
            {
                AddOut();
                EndAtBat("三振出局");
            }
        }
        else
        {
            balls++;
            Debug.Log($"[AtBatCounter] 坏球！累计：{balls}/{maxBalls}");

            if (balls >= maxBalls)
            {
                EndAtBat("四坏球保送上垒");
            }
        }
        UpdateAllUI();
    }

    /// <summary>
    /// 记录击球（适配BaseballController）
    /// </summary>
    /// <param name="hitQuality">击球质量（0~1）</param>
    public void RecordHit(float hitQuality)
    {
        if (!_isPlateActive) return;

        hits++;
        strikes = 0;
        balls = 0;

        string hitType = hitQuality >= 0.8f ? "全垒打" : hitQuality >= 0.5f ? "二垒安打" : "一垒安打";
        Debug.Log($"[AtBatCounter] 击球成功！类型：{hitType} | 质量：{hitQuality:F2} | 累计击球：{hits}");

        OnHitSuccess.Invoke();
        EndAtBat(hitType);
        UpdateAllUI();
    }

    /// <summary>
    /// 累计出局数，自动处理半局轮换
    /// </summary>
    public void AddOut()
    {
        outs++;
        Debug.Log($"[AtBatCounter] 出局！累计出局数：{outs}/{maxOutsPerInning}");

        if (outs >= maxOutsPerInning)
        {
            // 半局结束，切换上下半局或进入下一局
            if (isTopInning)
            {
                isTopInning = false;
                outs = 0;
                Debug.Log("[AtBatCounter] 上半局结束，下半局开始");
            }
            else
            {
                currentInning++;
                isTopInning = true;
                outs = 0;
                Debug.Log($"[AtBatCounter] 下半局结束，进入第 {currentInning} 局上半");
            }
            OnInningEnd.Invoke();
        }
        UpdateAllUI();
    }

    /// <summary>
    /// 禁用打席（适配GameManager）
    /// </summary>
    public void DisablePlate()
    {
        _isPlateActive = false;
        CancelInvoke();
        Debug.Log("[AtBatCounter] 打席已禁用，击球判定已关闭");
    }

    /// <summary>
    /// 手动切换到下一局（例如调用 NextInning 强制推进）
    /// </summary>
    public void NextInning()
    {
        currentInning++;
        isTopInning = true;
        ResetInning();
        Debug.Log($"[AtBatCounter] 进入第 {currentInning} 局");
        UpdateAllUI();
    }

    /// <summary>
    /// 结束当前打席
    /// </summary>
    private void EndAtBat(string reason)
    {
        _isPlateActive = false;
        Debug.Log($"[AtBatCounter] 打席结束：{reason}");
        Invoke(nameof(ResetAtBat), 3f);
    }

    /// <summary>
    /// 重置打席（保留局数统计）
    /// </summary>
    private void ResetAtBat()
    {
        strikes = 0;
        balls = 0;
        _isPlateActive = true;
        Debug.Log("[AtBatCounter] 新打席开始");
        UpdateAllUI();
    }

    /// <summary>
    /// 手动触发游戏结束
    /// </summary>
    public void TriggerGameEnd()
    {
        DisablePlate();
        OnGameEnd.Invoke();
        Debug.Log("[AtBatCounter] 游戏结束事件已触发");
    }

    /// <summary>
    /// 强制重置所有计数
    /// </summary>
    public void ForceResetAll()
    {
        strikes = 0;
        balls = 0;
        outs = 0;
        hits = 0;
        homeScore = 0;
        guestScore = 0;
        currentInning = 1;
        isTopInning = true;
        _isPlateActive = true;
        Debug.Log("[AtBatCounter] 所有计数已强制重置");
        UpdateAllUI();
    }

    private void OnDestroy()
    {
        OnHitSuccess.RemoveAllListeners();
        OnInningEnd.RemoveAllListeners();
        OnGameEnd.RemoveAllListeners();
        CancelInvoke();
    }
}