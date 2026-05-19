using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class FactionSelector : MonoBehaviour
{
    [Header("阵营按钮")]
    public Button humanButton;
    public Button alienButton;

    [Header("选择后隐藏的根面板")]
    public GameObject selectionPanel;

    [Header("悬停效果")]
    public float hoverScale = 1.05f;
    public float scaleSpeed = 8f;

    private Vector3 humanOriginalScale;
    private Vector3 alienOriginalScale;
    private Coroutine humanCoroutine;
    private Coroutine alienCoroutine;

    void Start()
    {
        if (humanButton != null)
            humanOriginalScale = humanButton.transform.localScale;
        if (alienButton != null)
            alienOriginalScale = alienButton.transform.localScale;

        // 绑定点击
        if (humanButton != null)
            humanButton.onClick.AddListener(() => SelectFaction("Human"));
        if (alienButton != null)
            alienButton.onClick.AddListener(() => SelectFaction("Alien"));

        // 为两个按钮分别添加悬停
        SetupButtonHover(humanButton, humanOriginalScale,
            (c) => humanCoroutine = c, () => humanCoroutine);
        SetupButtonHover(alienButton, alienOriginalScale,
            (c) => alienCoroutine = c, () => alienCoroutine);
    }

    private void SetupButtonHover(Button button, Vector3 originalScale,
        System.Action<Coroutine> setCoroutine, System.Func<Coroutine> getCoroutine)
    {
        if (button == null) return;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        // PointerEnter
        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) =>
        {
            if (getCoroutine() != null) StopCoroutine(getCoroutine());
            setCoroutine(StartCoroutine(SmoothScale(button.gameObject, originalScale * hoverScale)));
        });
        trigger.triggers.Add(enterEntry);

        // PointerExit
        EventTrigger.Entry exitEntry = new EventTrigger.Entry();
        exitEntry.eventID = EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) =>
        {
            if (getCoroutine() != null) StopCoroutine(getCoroutine());
            setCoroutine(StartCoroutine(SmoothScale(button.gameObject, originalScale)));
        });
        trigger.triggers.Add(exitEntry);
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
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            obj.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        if (obj != null)
            obj.transform.localScale = targetScale;
    }

    void SelectFaction(string faction)
    {
        Debug.Log($"选择了阵营: {faction}");
        PlayerPrefs.SetString("SelectedFaction", faction);

        if (GameInitializer.Instance != null)
            GameInitializer.Instance.SpawnPitcher(faction);
        else
            Debug.LogError("GameInitializer 实例未找到！");

        if (selectionPanel != null)
            selectionPanel.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (humanCoroutine != null) StopCoroutine(humanCoroutine);
        if (alienCoroutine != null) StopCoroutine(alienCoroutine);
    }
}