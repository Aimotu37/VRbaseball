using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;
/// <summary>
/// 投球倒计时UI控制器
/// </summary>
public class PitchCountdownUI : MonoBehaviour
{
    [Header("UI组件")]
    public TMP_Text countdownText;
    public Image countdownFill;
    public Canvas countdownCanvas;

    [Header("倒计时配置")]
    public float countdownDuration = 3f;
    public Color normalColor = Color.white;
    public Color warningColor = Color.red;

    private Coroutine _countdownCoroutine;

    private void Awake()
    {
        if (countdownCanvas == null)
            countdownCanvas = GetComponent<Canvas>();
        if (countdownText == null)
            countdownText = GetComponentInChildren<TMP_Text>();
        if (countdownFill == null)
            countdownFill = GetComponentInChildren<Image>();

        Hide(); // 默认隐藏
    }

    /// <summary>
    /// 开始倒计时
    /// </summary>
    public IEnumerator StartCountdown(Action onComplete)
    {
        Show();
        float elapsedTime = 0f;

        while (elapsedTime < countdownDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = 1 - (elapsedTime / countdownDuration);
            UpdateCountdownUI(progress, elapsedTime);
            yield return null;
        }

        Hide();
        onComplete?.Invoke();
    }

    private void UpdateCountdownUI(float progress, float elapsedTime)
    {
        if (countdownFill != null)
        {
            countdownFill.fillAmount = progress;
            countdownFill.color = elapsedTime > countdownDuration - 1f ? warningColor : normalColor;
        }

        if (countdownText != null)
        {
            int remainingSeconds = Mathf.CeilToInt(countdownDuration - elapsedTime);
            countdownText.text = remainingSeconds.ToString();
            countdownText.color = elapsedTime > countdownDuration - 1f ? warningColor : normalColor;
        }
    }

    /// <summary>
    /// 显示倒计时UI
    /// </summary>
    public void Show()
    {
        if (countdownCanvas != null)
            countdownCanvas.enabled = true;
    }

    /// <summary>
    /// 新增：隐藏倒计时UI（适配GameManager）
    /// </summary>
    public void Hide()
    {
        if (countdownCanvas != null)
            countdownCanvas.enabled = false;

        // 重置UI状态
        if (countdownFill != null)
        {
            countdownFill.fillAmount = 1f;
            countdownFill.color = normalColor;
        }
        if (countdownText != null)
        {
            countdownText.text = countdownDuration.ToString();
            countdownText.color = normalColor;
        }
    }

    /// <summary>
    /// 强制停止倒计时
    /// </summary>
    public void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        Hide();
    }
}