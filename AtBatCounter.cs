//追踪当前打席的 好球数(0-2)、坏球数(0-3) 和出局数(0-2)，并根据每次投球结果（StrikeZoneResult）驱动状态迁移。
using UnityEngine;

/// <summary>
/// 好球区判断结果
/// </summary>
public struct StrikeZoneResult
{
    public bool isInStrikeZone;     // 是否在好球区内
    public bool didSwing;           // 是否挥棒
    public bool didHit;             // 是否击中球 (仅挥棒时有效)
    public float hitQuality;        // 击球质量 0-1
    
    public StrikeZoneResult(bool inZone, bool swing, bool hit = false, float quality = 0)
    {
        isInStrikeZone = inZone;
        didSwing = swing;
        didHit = hit;
        hitQuality = Mathf.Clamp01(quality);
    }
}

/// <summary>
/// 打席结果
/// </summary>
public enum PlateAppearanceResult
{
    None,           // 进行中
    Strikeout,      // 三振
    Walk,           // 保送
    Hit,            // 安打
    Out             // 出局
}

/// <summary>
/// 棒球打席计数器状态机
/// </summary>
public class AtBatCounter : MonoBehaviour
{
    [Header("当前状态")]
    [SerializeField] private int strikes = 0;   // 好球数: 0-2
    [SerializeField] private int balls = 0;     // 坏球数: 0-3
    [SerializeField] private int outs = 0;      // 出局数: 0-2
    
    private PlateAppearanceResult currentResult = PlateAppearanceResult.None;
    
    [Header("事件系统 - UI订阅这些事件来更新显示")]
    public System.Action OnStrike;                      // 好球
    public System.Action OnBall;                        // 坏球
    public System.Action OnFoul;                        // 界外球
    public System.Action OnStrikeout;                   // 三振
    public System.Action OnWalk;                        // 保送
    public System.Action OnHit;                         // 安打
    public System.Action OnOut;                         // 出局
    public System.Action<PlateAppearanceResult> OnPlateEnd;  // 打席结束
    public System.Action<int> OnOutsChanged;            // 出局数变化
    public System.Action OnInningEnd;                   // 半局结束
    
    // 新增: UI专用的数据更新事件（带具体数值）
    public System.Action<int, int, int> OnCountChanged; // (strikes, balls, outs)
    
    // 属性
    public int Strikes => strikes;
    public int Balls => balls;
    public int Outs => outs;
    public bool IsPlateActive => currentResult == PlateAppearanceResult.None;
    public bool IsInningOver => outs >= 3;
    
    private void Start()
    {
        ResetPlate();
    }
    
    /// <summary>
    /// 处理投球结果 (状态机核心)
    /// </summary>
    public void ProcessPitch(StrikeZoneResult result)
    {
        if (IsInningOver || !IsPlateActive) return;
        
        // 1. 挥棒击中
        if (result.didSwing && result.didHit)
        {
            ProcessHit(result.hitQuality);
            return;
        }
        
        // 2. 挥棒未中
        if (result.didSwing && !result.didHit)
        {
            ProcessMiss();
            return;
        }
        
        // 3. 未挥棒
        if (!result.didSwing)
        {
            ProcessNoSwing(result.isInStrikeZone);
        }
        
        // 每次状态改变后触发UI更新
        NotifyUIUpdate();
    }
    
    /// <summary>
    /// 处理击中球
    /// </summary>
    private void ProcessHit(float quality)
    {
        if (quality < 0.1f)
        {
            ProcessFoulTip();           // 擦棒
        }
        else if (quality < 0.4f)
        {
            RecordOut(PlateAppearanceResult.Out);   // 坏击球出局
            ResetPlate();
        }
        else
        {
            EndPlate(PlateAppearanceResult.Hit);    // 安打
            ResetPlate();
        }
    }
    
    /// <summary>
    /// 处理挥空
    /// </summary>
    private void ProcessMiss()
    {
        if (strikes < 2)
        {
            strikes++;
            OnStrike?.Invoke();
        }
        else
        {
            RecordOut(PlateAppearanceResult.Strikeout);
            ResetPlate();
        }
    }
    
    /// <summary>
    /// 处理未挥棒
    /// </summary>
    private void ProcessNoSwing(bool inZone)
    {
        if (inZone)  // 进好球带
        {
            if (strikes < 2)
            {
                strikes++;
                OnStrike?.Invoke();
            }
            else
            {
                RecordOut(PlateAppearanceResult.Strikeout);
                ResetPlate();
            }
        }
        else  // 坏球
        {
            if (balls < 3)
            {
                balls++;
                OnBall?.Invoke();
            }
            else
            {
                EndPlate(PlateAppearanceResult.Walk);
                ResetPlate();
            }
        }
    }
    
    /// <summary>
    /// 处理擦棒界外
    /// </summary>
    private void ProcessFoulTip()
    {
        if (strikes < 2)
        {
            strikes++;
            OnStrike?.Invoke();
            OnFoul?.Invoke();
        }
        else
        {
            RecordOut(PlateAppearanceResult.Strikeout);
            ResetPlate();
        }
    }
    
    /// <summary>
    /// 界外球 (外部调用)
    /// </summary>
    public void AddFoulBall()
    {
        if (IsInningOver || !IsPlateActive) return;
        
        if (strikes < 2)
        {
            strikes++;
            OnStrike?.Invoke();
        }
        OnFoul?.Invoke();
        NotifyUIUpdate();
    }
    
    /// <summary>
    /// 记录出局
    /// </summary>
    private void RecordOut(PlateAppearanceResult result)
    {
        outs++;
        OnOutsChanged?.Invoke(outs);
        EndPlate(result);
        
        if (IsInningOver)
            OnInningEnd?.Invoke();
    }
    
    /// <summary>
    /// 结束打席
    /// </summary>
    private void EndPlate(PlateAppearanceResult result)
    {
        currentResult = result;
        OnPlateEnd?.Invoke(result);
        
        switch (result)
        {
            case PlateAppearanceResult.Strikeout: OnStrikeout?.Invoke(); break;
            case PlateAppearanceResult.Walk: OnWalk?.Invoke(); break;
            case PlateAppearanceResult.Hit: OnHit?.Invoke(); break;
            case PlateAppearanceResult.Out: OnOut?.Invoke(); break;
        }
        
        NotifyUIUpdate();
    }
    
    /// <summary>
    /// 重置打席
    /// </summary>
    public void ResetPlate()
    {
        strikes = 0;
        balls = 0;
        currentResult = PlateAppearanceResult.None;
        NotifyUIUpdate();
    }
    
    /// <summary>
    /// 重置半局
    /// </summary>
    public void ResetInning()
    {
        outs = 0;
        ResetPlate();
        OnOutsChanged?.Invoke(0);
        NotifyUIUpdate();
    }
    
    /// <summary>
    /// 强制结束打席
    /// </summary>
    public void ForceEndPlate(PlateAppearanceResult result)
    {
        if (!IsPlateActive) return;
        RecordOut(result);
        ResetPlate();
    }
    
    /// <summary>
    /// 通知UI更新（新增方法）
    /// </summary>
    private void NotifyUIUpdate()
    {
        OnCountChanged?.Invoke(strikes, balls, outs);
    }
    
    /// <summary>
    /// 获取状态字符串
    /// </summary>
    public string GetStateString()
    {
        string status = currentResult == PlateAppearanceResult.None ? "进行中" : $"结束: {currentResult}";
        return $"{strikes}-{balls} | 出局:{outs} | {status}";
    }
    
    /// <summary>
    /// 获取UI显示数组
    /// </summary>
    public bool[] GetStrikesDisplay() => new bool[] { strikes >= 1, strikes >= 2 };
    public bool[] GetBallsDisplay() => new bool[] { balls >= 1, balls >= 2, balls >= 3 };
    public bool[] GetOutsDisplay() => new bool[] { outs >= 1, outs >= 2 };
}