using UnityEngine;

public class BackgroundSwitcher : MonoBehaviour
{
    [Header("天空盒材质（每个星球对应一个）")]
    public Material earthSkybox;   // 地球背景材质
    public Material marsSkybox;    // 火星背景材质
    public Material moonSkybox;    // 月球背景材质


    private enum Planet { Earth, Mars, Moon }

    void Start()
    {
        // 读取玩家选择的星球
        string selectedPlanet = PlayerPrefs.GetString("SelectedPlanet", "Earth");
        Debug.Log($"🌍 玩家选择了星球: {selectedPlanet}");

        // 切换背景
        SetBackground(selectedPlanet);
    }

    void SetBackground(string planet)
    {
        // ========== 方法1：切换天空盒 ==========
        Material targetSkybox = null;

        switch (planet)
        {
            case "Earth":
                targetSkybox = earthSkybox;
                break;
            case "Mars":
                targetSkybox = marsSkybox;
                break;
            case "Moon":
                targetSkybox = moonSkybox;
                break;
        }

        if (targetSkybox != null)
        {
            RenderSettings.skybox = targetSkybox;
            Debug.Log($"✅ 天空盒已切换为: {planet}");
        }
        else if (earthSkybox != null)
        {
            Debug.LogWarning($"⚠️ {planet} 的天空盒材质未设置，使用默认");
        }
    }
}