using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class CountdownUI : MonoBehaviour
{
    public GameObject logo;
    public TextMeshProUGUI countdownText;

    void Start()
    {
        // 初始状态：显示Logo，隐藏倒计时
        logo.SetActive(true);
        countdownText.gameObject.SetActive(false);

        // 开始模拟流程
        StartCoroutine(AutoTest());
    }

    IEnumerator AutoTest()
    {
        // 等待2秒（模拟游戏准备阶段）
        yield return new WaitForSeconds(2f);

        // 播放倒计时
        yield return StartCoroutine(PlayCountdown());

        // 倒计时结束后回到Logo（可选再停留几秒）
        yield return new WaitForSeconds(1f);

        logo.SetActive(true);
    }

    IEnumerator PlayCountdown()
    {
        logo.SetActive(false);
        countdownText.gameObject.SetActive(true);

        string[] steps = { "3", "2", "1", "GO!" };

        foreach (string s in steps)
        {
            countdownText.text = s;

            // 简单动画（放大效果）
            countdownText.transform.localScale = Vector3.one * 0.5f;
            yield return new WaitForSeconds(0.1f);

            countdownText.transform.localScale = Vector3.one * 1.2f;
            yield return new WaitForSeconds(0.1f);

            countdownText.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(0.8f);
        }

        countdownText.gameObject.SetActive(false);
    }
}