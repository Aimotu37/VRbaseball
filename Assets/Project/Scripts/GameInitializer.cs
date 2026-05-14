using UnityEngine;

public class GameInitializer : MonoBehaviour
{
    [Header("投手预制体")]
    public GameObject humanPitcherPrefab;   // 人类投手
    public GameObject alienPitcherPrefab;   // 外星人投手

    [Header("投手生成位置")]
    public Transform pitcherSpawnPoint;      // 投手生成位置

    void Start()
    {
        // 读取玩家选择的阵营
        string selectedFaction = PlayerPrefs.GetString("SelectedFaction", "Human");

        // 根据阵营生成投手（互换）
        SpawnPitcher(selectedFaction);
    }

    void SpawnPitcher(string faction)
    {
        GameObject pitcherPrefab = null;

        if (faction == "Human")
        {
            // 人类玩家 → 对战外星人投手
            pitcherPrefab = alienPitcherPrefab;
        }
        else if (faction == "Alien")
        {
            // 外星人玩家 → 对战人类投手
            pitcherPrefab = humanPitcherPrefab;
        }

        if (pitcherPrefab != null && pitcherSpawnPoint != null)
        {
            Instantiate(pitcherPrefab, pitcherSpawnPoint.position, pitcherSpawnPoint.rotation);
        }
    }
}