using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UICardInfo : MonoBehaviour
{
    public static UICardInfo Instance { get; private set; }

    [Header("Refs")]
    public RectTransform panel;     // กล่อง info
    public CanvasGroup group;
    public Image icon;
    public TMP_Text title;
    public TMP_Text desc;
    public TMP_Text manaText;    // "Mana: X"
    public TMP_Text usingText;   // "Using: used/max"
    private CardData _current;

    [Header("Anim")]
    public float slideDur = 0.18f;
    public float offsetX = 1000f;    // ระยะซ่อนด้านขวา

    Coroutine co;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!panel) panel = transform as RectTransform;
        if (!group) group = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        HideImmediate();
    }

    public void Show(CardData data)
    {
        if (!panel || !group || data == null) return;

        _current = data; // ✅ จำตัวที่แสดงอยู่
        if (icon)  icon.sprite   = data.icon;
        if (title) title.text    = data.displayName;
        if (desc)  desc.text     = data.description;
        if (manaText)  manaText.text = $"Mana: {data.Mana}";
        if (usingText)
        {
            int used = (TurnManager.Instance != null) ? TurnManager.Instance.GetUsageCount(data) : 0;
            string limit = (data.maxUsagePerTurn <= 0) ? "∞" : data.maxUsagePerTurn.ToString();
            usingText.text = $"Using: {used}/{limit}";
        }

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Slide(true));
    }

    public void Hide()
    {
        if (!panel || !group) return;
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Slide(false));
        _current = null; // ✅ ล้างเมื่อซ่อน
    }

    void HideImmediate()
    {
        if (!panel || !group) return;
        group.alpha = 0f;
        var p = panel.anchoredPosition; p.x = offsetX;
        panel.anchoredPosition = p;
        group.blocksRaycasts = false;
        group.interactable = false;
        _current = null; // ✅ ล้างเมื่อซ่อนทันที
    }
    public void HideIfShowing(CardData data)
    {
        if (data != null && _current == data) Hide();
    }

    IEnumerator Slide(bool show)
    {
        float t = 0f, dur = Mathf.Max(0.01f, slideDur);
        Vector2 from = panel.anchoredPosition;
        Vector2 to = from; to.x = show ? 0f : offsetX;

        float aFrom = group.alpha, aTo = show ? 1f : 0f;

        if (show) { group.blocksRaycasts = true; group.interactable = true; }

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // easeOutCubic
            panel.anchoredPosition = Vector2.LerpUnclamped(from, to, e);
            group.alpha = Mathf.LerpUnclamped(aFrom, aTo, e);
            yield return null;
        }

        if (!show) { group.blocksRaycasts = false; group.interactable = false; }
        co = null;
    }
    // (ออปชัน) เรียกรีเฟรชตัวเลข Using ภายหลัง เช่น หลังใช้การ์ดไปแล้ว
    public void RefreshUsage(CardData data)
    {
        if (!usingText || data == null) return;
        int used = (TurnManager.Instance != null) ? TurnManager.Instance.GetUsageCount(data) : 0;
        string limit = (data.maxUsagePerTurn <= 0) ? "∞" : data.maxUsagePerTurn.ToString();
        usingText.text = $"Using: {used}/{limit}";
    }
}
