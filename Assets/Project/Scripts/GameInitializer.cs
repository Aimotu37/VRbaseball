using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    [Header("投手预制体")]
    public GameObject humanPitcherPrefab;
    public GameObject alienPitcherPrefab;

    [Header("投手生成位置")]
    public Transform pitcherSpawnPoint;

    public static GameInitializer Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        // 不再自动生成，等待 FactionSelector 调用
        Debug.Log("GameInitializer 就绪，等待阵营选择...");
    }

    public void SpawnPitcher(string faction)
    {
        GameObject prefab = null;

        if (faction == "Human")
            prefab = humanPitcherPrefab;
        else if (faction == "Alien")
            prefab = alienPitcherPrefab;

        if (prefab != null && pitcherSpawnPoint != null)
        {
            // 在原有生成点旋转基础上，绕 Y 轴顺时针（-90°）旋转
            Quaternion rotation = pitcherSpawnPoint.rotation * Quaternion.Euler(0, 90, 0);
            Instantiate(prefab, pitcherSpawnPoint.position, rotation);
            Debug.Log($"已生成 {faction} 阵营投手");
        }
        else
        {
            Debug.LogError("投手生成失败：预制体或生成位置为空");
        }
    }
}