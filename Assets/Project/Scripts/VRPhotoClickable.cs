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
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;  // VR 射线交互器

    [Header("悬停效果")]
    [Tooltip("悬停时的放大倍数")]
    public float hoverScale = 1.5f;
    [Tooltip("放大动画速度")]
    public float scaleSpeed = 5f;

    private Camera mainCamera;
    private Vector3 originalScale;
    private bool isHovering = false;
    private bool isScaled = false;

    private void Start()
    {
        mainCamera = Camera.main;
        originalScale = transform.localScale;

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
        bool isPointerOver = false;

        // ========== 鼠标控制 ==========
        if (Mouse.current != null)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    isPointerOver = true;

                    // 鼠标悬停时检测点击
                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        OnPhotoClicked();
                    }
                }
            }
        }

        // ========== VR 射线交互 ==========
        if (rayInteractor != null)
        {
            // 检测 VR 射线是否悬停
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit vrHit))
            {
                if (vrHit.collider.gameObject == gameObject)
                {
                    isPointerOver = true;

                    // 检测 VR 手柄扳机键点击
                    if (Input.GetButtonDown("XRI_Right_Trigger") || Input.GetButtonDown("XRI_Left_Trigger"))
                    {
                        Debug.Log($"🎮 VR 点击到了: {photoType}");
                        OnPhotoClicked();
                    }
                }
            }
        }

        // 处理悬停放大效果
        HandleHoverEffect(isPointerOver);
    }

    private void HandleHoverEffect(bool isPointerOver)
    {
        if (isPointerOver && !isScaled)
        {
            // 开始放大
            isScaled = true;
            isHovering = true;
            Debug.Log($"🔍 悬停放大: {photoType}");
        }
        else if (!isPointerOver && isScaled)
        {
            // 开始缩小
            isScaled = false;
            isHovering = false;
        }

        // 平滑过渡缩放
        Vector3 targetScale = isScaled ? originalScale * hoverScale : originalScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
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

    // 可选：对象被禁用时恢复原始大小
    private void OnDisable()
    {
        if (isScaled)
        {
            transform.localScale = originalScale;
            isScaled = false;
            isHovering = false;
        }
    }
}