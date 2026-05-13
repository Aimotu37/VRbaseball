using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class FactionSelector : MonoBehaviour
{
    [Header("拖拽照片物体")]
    public GameObject humanPhoto;
    public GameObject alienPhoto;

    [Header("跳转场景")]
    public string nextSceneName = "SampleScene";

    [Header("VR 设置（可选）")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    [Header("悬停效果")]
    [Tooltip("悬停时的放大倍数")]
    public float hoverScale = 1.05f;
    [Tooltip("放大动画速度")]
    public float scaleSpeed = 8f;

    private Camera mainCamera;
    private GameObject currentHoveredObject = null;
    private Dictionary<GameObject, Vector3> originalScales = new Dictionary<GameObject, Vector3>();
    private Coroutine currentScaleCoroutine = null;

    void Start()
    {
        mainCamera = Camera.main;

        // 保存原始大小
        if (humanPhoto != null)
        {
            originalScales[humanPhoto] = humanPhoto.transform.localScale;
            // 确保照片有Collider用于射线检测
            AddColliderIfNeeded(humanPhoto);
        }
        if (alienPhoto != null)
        {
            originalScales[alienPhoto] = alienPhoto.transform.localScale;
            AddColliderIfNeeded(alienPhoto);
        }

        string selectedPlanet = PlayerPrefs.GetString("SelectedPlanet", "Unknown");
        Debug.Log($"当前星球: {selectedPlanet}，请选择阵营");
    }

    private void AddColliderIfNeeded(GameObject obj)
    {
        if (obj.GetComponent<Collider>() == null)
        {
            BoxCollider collider = obj.AddComponent<BoxCollider>();
            // 自动调整Collider大小以匹配物体
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                collider.size = renderer.bounds.size;
            }
            Debug.Log($"为 {obj.name} 添加了Collider");
        }
    }

    void Update()
    {
        // ========== 鼠标控制 ==========
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (hit.collider.gameObject == humanPhoto)
                {
                    SelectFaction("Human");
                }
                else if (hit.collider.gameObject == alienPhoto)
                {
                    SelectFaction("Alien");
                }
            }
        }

        // ========== 检测悬停 ==========
        CheckHover();
    }

    private void CheckHover()
    {
        GameObject hoveredObject = null;

        // 方法1：通过VR射线检测悬停
        if (rayInteractor != null)
        {
            // 获取射线击中的物体
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit vrHit))
            {
                hoveredObject = vrHit.collider.gameObject;
                Debug.Log($"VR射线击中: {vrHit.collider.name}");

                // 检测点击
                if (Input.GetButtonDown("XRI_Right_Trigger") || Input.GetButtonDown("XRI_Left_Trigger"))
                {
                    if (hoveredObject == humanPhoto)
                    {
                        SelectFaction("Human");
                    }
                    else if (hoveredObject == alienPhoto)
                    {
                        SelectFaction("Alien");
                    }
                }
            }
            else
            {
                Debug.Log("VR射线没有击中任何物体");
            }
        }

        // 方法2：通过摄像机中心点射线检测（用于调试）
        if (hoveredObject == null && mainCamera != null)
        {
            Ray centerRay = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            if (Physics.Raycast(centerRay, out RaycastHit centerHit, 100f))
            {
                if (centerHit.collider.gameObject == humanPhoto || centerHit.collider.gameObject == alienPhoto)
                {
                    hoveredObject = centerHit.collider.gameObject;
                    Debug.Log($"中心射线击中: {centerHit.collider.name}");
                }
            }
        }

        // 处理悬停缩放
        HandleHoverEffect(hoveredObject);
    }

    private void HandleHoverEffect(GameObject hoveredObject)
    {
        // 检查悬停的是否是照片
        bool isValidHover = hoveredObject != null &&
                            (hoveredObject == humanPhoto || hoveredObject == alienPhoto);

        GameObject targetHover = isValidHover ? hoveredObject : null;

        // 如果悬停对象改变
        if (currentHoveredObject != targetHover)
        {
            // 恢复之前的对象
            if (currentHoveredObject != null && originalScales.ContainsKey(currentHoveredObject))
            {
                if (currentScaleCoroutine != null)
                    StopCoroutine(currentScaleCoroutine);
                currentScaleCoroutine = StartCoroutine(SmoothScale(currentHoveredObject, originalScales[currentHoveredObject]));
                Debug.Log($"恢复: {currentHoveredObject.name}");
            }

            // 缩放新的对象
            if (targetHover != null && originalScales.ContainsKey(targetHover))
            {
                if (currentScaleCoroutine != null)
                    StopCoroutine(currentScaleCoroutine);
                Vector3 targetScale = originalScales[targetHover] * hoverScale;
                currentScaleCoroutine = StartCoroutine(SmoothScale(targetHover, targetScale));
                Debug.Log($"缩放: {targetHover.name} 到 {hoverScale}倍");
            }

            currentHoveredObject = targetHover;
        }
    }

    private IEnumerator SmoothScale(GameObject obj, Vector3 targetScale)
    {
        if (obj == null) yield break;

        Vector3 startScale = obj.transform.localScale;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration && obj != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 使用平滑曲线
            t = Mathf.SmoothStep(0, 1, t);
            obj.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        if (obj != null)
        {
            obj.transform.localScale = targetScale;
        }

        currentScaleCoroutine = null;
    }

    void SelectFaction(string faction)
    {
        Debug.Log($"选择了阵营: {faction}");
        PlayerPrefs.SetString("SelectedFaction", faction);
        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDestroy()
    {
        if (currentScaleCoroutine != null)
        {
            StopCoroutine(currentScaleCoroutine);
        }
    }
}