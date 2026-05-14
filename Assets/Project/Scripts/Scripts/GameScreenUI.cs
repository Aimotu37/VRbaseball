using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Collections;

public class GameScreenUI : MonoBehaviour
{
    public GameObject logo;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI resultText;
    public GameObject dimBackground;

    int humanScore = 0;
    int alienScore = 0;

    Material resultMaterial;

    void Start()
    {
        ShowLogo();

        // 获取材质（用于控制发光）
        resultMaterial = resultText.fontMaterial;
    }

    void Update()
    {
        // ✨ 呼吸发光（仅在结果显示时生效）
        if (resultText.gameObject.activeSelf)
        {
            float glow = Mathf.PingPong(Time.time * 2f, 1f);
            resultMaterial.SetFloat("_GlowPower", glow);
        }
        // 测试输入
        if (Input.GetKeyDown(KeyCode.Alpha1)) ShowLogo();
        if (Input.GetKeyDown(KeyCode.Alpha2)) StartGame();
        if (Input.GetKeyDown(KeyCode.Alpha3)) ShowResult(true);
        if (Input.GetKeyDown(KeyCode.Q)) UpdateScore(Random.Range(0, 10), Random.Range(0, 10));
    }

    // 🟦 比赛前
    public void ShowLogo()
    {
        logo.SetActive(true);
        scoreText.gameObject.SetActive(false);
        resultText.gameObject.SetActive(false);
        dimBackground.SetActive(false);
    }

    // 🟨 比赛开始
    public void StartGame()
    {
        logo.SetActive(false);
        scoreText.gameObject.SetActive(true);
        resultText.gameObject.SetActive(false);
        dimBackground.SetActive(false);

        UpdateScore(0, 0);
    }

    // 🟩 更新比分
    public void UpdateScore(int human, int alien)
    {
        humanScore = human;
        alienScore = alien;
        scoreText.text = human + " : " + alien;
    }

    // 🟥 显示结算
    public void ShowResult(bool isHumanPlayer)
    {
        resultText.gameObject.SetActive(true);
        dimBackground.SetActive(true);

        bool win = isHumanPlayer ? humanScore > alienScore : alienScore > humanScore;

        if (win)
        {
            resultText.text = "YOU WIN";
            resultText.color = new Color(0.3f, 1f, 1f); // 青蓝
        }
        else
        {
            resultText.text = "YOU LOSE";
            resultText.color = new Color(1f, 0.3f, 0.3f); // 红
        }

        // ✨ 播放动画
        StartCoroutine(PopEffect());
        StartCoroutine(FlashEffect());
    }

    // ✨ 弹出动画
    IEnumerator PopEffect()
    {
        Transform t = resultText.transform;

        t.localScale = Vector3.zero;

        float time = 0;

        // 放大
        while (time < 0.2f)
        {
            time += Time.deltaTime;
            t.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.2f, time / 0.2f);
            yield return null;
        }

        // 回弹
        time = 0;
        while (time < 0.1f)
        {
            time += Time.deltaTime;
            t.localScale = Vector3.Lerp(Vector3.one * 1.2f, Vector3.one, time / 0.1f);
            yield return null;
        }
    }

    // ✨ 闪光效果
    IEnumerator FlashEffect()
    {
        Color original = resultText.color;

        for (int i = 0; i < 2; i++)
        {
            resultText.color = Color.white;
            yield return new WaitForSeconds(0.1f);

            resultText.color = original;
            yield return new WaitForSeconds(0.1f);
        }
    }


}
