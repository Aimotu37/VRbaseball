using UnityEngine;
using UnityEngine.InputSystem;  // 使用新 Input System
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider))]
public class VRPhotoClickable : MonoBehaviour
{
    public enum PhotoType
    {
        Earth, Mars, Moon
    }

    public PhotoType photoType;
    public string nextSceneName = "ChooseFaction";

    [Header("VR 设置（可选）")]
    // public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;  // VR 射线交互器

    private Camera mainCamera;

    private void Start()
    {
        mainCamera = Camera.main;

        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;

        Debug.Log($"✅ {photoType} 已就绪");
    }

    void Update()
    {
        // ========== 鼠标控制（默认使用） ==========
        // 使用新 Input System 检测鼠标左键
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    OnPhotoClicked();
                }
            }
        }

        // 检查 VR 射线交互器是否存在
        if (rayInteractor != null)
        {
            // 检测 VR 手柄是否按下扳机键
            if (Input.GetButtonDown("XRI_Right_Trigger") || Input.GetButtonDown("XRI_Left_Trigger"))
            {
                // 获取 VR 射线的击中点
                if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit vrHit))
                {
                    if (vrHit.collider.gameObject == gameObject)
                    {
                        Debug.Log($"🎮 VR 点击到了: {photoType}");
                        OnPhotoClicked();
                    }
                }
            }
        }
    }

    public void OnPhotoClicked()
    {
        Debug.Log($"🖱️ 点击到了: {photoType}");
        PlayerPrefs.SetString("SelectedPlanet", photoType.ToString());

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            Debug.Log($"🚀 跳转到: {nextSceneName}");
            SceneManager.LoadScene(nextSceneName);
        }
    }
}