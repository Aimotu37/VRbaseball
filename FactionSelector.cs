using UnityEngine;
using UnityEngine.InputSystem;  // 使用新 Input System
using UnityEngine.SceneManagement;

public class FactionSelector : MonoBehaviour
{
    [Header("拖拽照片物体")]
    public GameObject humanPhoto;
    public GameObject alienPhoto;

    [Header("跳转场景")]
    public string nextSceneName = "SampleScene";

    [Header("VR 设置（可选）")]
    // public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;  // VR 射线交互器

    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        string selectedPlanet = PlayerPrefs.GetString("SelectedPlanet", "Unknown");
        Debug.Log($"当前星球: {selectedPlanet}，请选择阵营");
    }

    void Update()
    {
        // ========== 鼠标控制（默认使用） ==========
        // 使用新 Input System 检测鼠标左键
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 获取鼠标位置
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log($"🎯 射线击中: {hit.collider.name}");

                if (hit.collider.gameObject == humanPhoto)
                {
                    Debug.Log("✅ 点击了人类照片");
                    SelectFaction("Human");
                }
                else if (hit.collider.gameObject == alienPhoto)
                {
                    Debug.Log("✅ 点击了外星人照片");
                    SelectFaction("Alien");
                }
            }
        }

        /*
        if (rayInteractor != null && rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit vrHit))
        {
            // 检测 VR 手柄扳机键
            if (Input.GetButtonDown("XRI_Right_Trigger") || Input.GetButtonDown("XRI_Left_Trigger"))
            {
                if (vrHit.collider.gameObject == humanPhoto)
                {
                    Debug.Log("✅ VR 点击了人类照片");
                    SelectFaction("Human");
                }
                else if (vrHit.collider.gameObject == alienPhoto)
                {
                    Debug.Log("✅ VR 点击了外星人照片");
                    SelectFaction("Alien");
                }
            }
        }
        */
    
    }

    void SelectFaction(string faction)
    {
        Debug.Log($"选择了阵营: {faction}");
        PlayerPrefs.SetString("SelectedFaction", faction);
        SceneManager.LoadScene(nextSceneName);
    }
}