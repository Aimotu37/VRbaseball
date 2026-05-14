using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIManager_start : MonoBehaviour
{
    public GameObject startMenu;
    public GameObject teamSelect;

    // 当前打开的界面
    private GameObject currentUI;

    void Start()
    {
        // 初始化：只显示开始界面
        ShowUI(startMenu);
    }

    // 核心方法：切换UI（防黑屏关键）
    public void ShowUI(GameObject target)
    {
        if (target == null) return;

        // 关闭当前UI
        if (currentUI != null)
        {
            currentUI.SetActive(false);
        }

        // 打开新UI
        target.SetActive(true);
        currentUI = target;
    }

    // 按钮调用：开始游戏
    public void GoToTeamSelect()
    {
        ShowUI(teamSelect);
    }

    // 按钮调用：返回开始界面
    public void GoToStartMenu()
    {
        ShowUI(startMenu);
    }
}
