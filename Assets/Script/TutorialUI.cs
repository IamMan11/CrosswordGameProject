using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialUI : MonoBehaviour
{
    [Header("Root / Overlay")]
    // --- Cutout overlays (4 แผ่น) ---
    RectTransform cutTop, cutBottom, cutLeft, cutRight;

    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private CanvasGroup overlay;       // พื้นมืด (Image + CanvasGroup)
    [SerializeField] private Image dimImage;            // ต้องมี RaycastTarget = true

    [Header("Stack (Bubble -> Card -> Buttons)")]
    [SerializeField] private RectTransform stackRoot;          // กลุ่มสแต็ก

    [Header("Card")]
    [SerializeField] private RectTransform cardRoot;    // กล่องการ์ด
    [SerializeField] private Image cardBg;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text leftVerticalText;   // ข้อความแนวตั้งซ้าย
    [SerializeField] private TMP_Text rightVerticalText;  // ข้อความแนวตั้งขวา

    [Header("Texts / Buttons")]
    [SerializeField] private TMP_Text titleText;       // ใช้กับ Bubble/Title
    [SerializeField] private TMP_Text bodyText;        // ใช้กับ Bubble/Body
    [SerializeField] private Button nextBtn;
    [SerializeField] private Button backBtn;
    [SerializeField] private Button skipBtn;
    [SerializeField] private TMP_Text pageText;

    [Header("Focus Frame (สี่เหลี่ยมครอบ)")]
    [SerializeField] private RectTransform focusFrame;
    [SerializeField] private Image focusImage;          // ใส่สี/ขอบตามชอบ
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseScale = 0.03f;

    private Coroutine pulseCo;

    void Awake()
    {
        HideImmediate();
    }

    // -------------------- Public API (TutorialManager เรียกใช้) --------------------

    public void BindButtons(System.Action onNext, System.Action onBack, System.Action onSkip)
    {
        if (nextBtn != null) { nextBtn.onClick.RemoveAllListeners(); nextBtn.onClick.AddListener(() => onNext?.Invoke()); }
        if (backBtn != null) { backBtn.onClick.RemoveAllListeners(); backBtn.onClick.AddListener(() => onBack?.Invoke()); }
        if (skipBtn != null) { skipBtn.onClick.RemoveAllListeners(); skipBtn.onClick.AddListener(() => onSkip?.Invoke()); }
    }

    public void SetBlockInput(bool block)
    {
        // บล็อก/ปล่อยอินพุตข้างหลัง
        overlay.blocksRaycasts = block;
        if (dimImage) dimImage.raycastTarget = block;
    }

    public void SetPage(int index, int total)
    {
        if (pageText != null) pageText.text = $"{index + 1}/{total}";
        if (backBtn) backBtn.gameObject.SetActive(index > 0);
    }

    public void SetCard(string title, string body, Sprite icon)
    {
        if (titleText) titleText.text = title;
        if (bodyText) bodyText.text = body;
        if (iconImage != null)
        {
            iconImage.enabled = icon != null;
            iconImage.sprite = icon;
        }
    }

    public void ShowCard(bool animated = true)
    {
        gameObject.SetActive(true);
        if (animated)
            StartCoroutine(FadeInAndPop());
        else
            overlay.alpha = 1f;
    }

    public void HideImmediate()
    {
        overlay.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void HideCard(bool animated = true)
    {
        if (animated)
            StartCoroutine(FadeOut());
        else
            HideImmediate();
        ClearSpotlightHole();
    }

    // ---- Style helpers (Balatro-like) ----

    public void SetDimAlpha(float a)
    {
        if (!dimImage) return;
        var c = dimImage.color;
        dimImage.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
    }

    public void SetSkipVisible(bool show)
    {
        if (skipBtn) skipBtn.gameObject.SetActive(show);
    }

    public void SetButtonLabels(string next, string back, string skip)
    {
        if (!string.IsNullOrEmpty(next) && nextBtn)
        {
            var t = nextBtn.GetComponentInChildren<TMP_Text>(true);
            if (t) t.text = next;
        }
        if (!string.IsNullOrEmpty(back) && backBtn)
        {
            var t = backBtn.GetComponentInChildren<TMP_Text>(true);
            if (t) t.text = back;
        }
        if (!string.IsNullOrEmpty(skip) && skipBtn)
        {
            var t = skipBtn.GetComponentInChildren<TMP_Text>(true);
            if (t) t.text = skip;
        }
    }

    public void SetCardVerticalTexts(string left, string right)
    {
        if (leftVerticalText && !string.IsNullOrEmpty(left)) leftVerticalText.text = left;
        if (rightVerticalText && !string.IsNullOrEmpty(right)) rightVerticalText.text = right;
    }

    public void SetCardTilt(float degrees)
    {
        if (cardRoot) cardRoot.localRotation = Quaternion.Euler(0f, 0f, degrees);
    }

    /// <summary>
    /// ย้าย Stack (Bubble→Card→Buttons) ไปกลางจอ หรือไปยัง center ของ target
    /// </summary>
    public void SetStackAnchor(RectTransform targetOrNull, bool anchorToTarget, Vector2 offsetPx)
    {
        if (!stackRoot) return;

        var canvasRT = (RectTransform)mainCanvas.transform;
        var cam = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
        Vector2 anchored;

        if (anchorToTarget && targetOrNull != null && targetOrNull != canvasRT)
        {
            // หา center ของ target ใน local space ของ canvas
            Vector3[] wc = new Vector3[4];
            targetOrNull.GetWorldCorners(wc);
            Vector3 centerWorld = (wc[0] + wc[2]) * 0.5f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRT,
                RectTransformUtility.WorldToScreenPoint(cam, centerWorld),
                cam,
                out anchored
            );
        }
        else
        {
            anchored = Vector2.zero; // กลางจอ
        }

        stackRoot.anchoredPosition = anchored + offsetPx;
    }

    // ---- Focus frame ----

    public void SetFocus(RectTransform target, Vector2 padding)
    {
        if (target == null)
        {
            if (focusFrame) focusFrame.gameObject.SetActive(false);
            StopPulse();
            return;
        }

        if (!focusFrame) return;

        focusFrame.gameObject.SetActive(true);

        // แปลงมุมทั้ง 4 จาก world -> local ของ canvas เพื่อความแม่นยำกับ scaler
        var canvasRT = (RectTransform)mainCanvas.transform;
        var cam = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;

        Vector3[] world = new Vector3[4];
        target.GetWorldCorners(world);

        Vector2 a, b, c, d;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, world[0]), cam, out a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, world[1]), cam, out b);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, world[2]), cam, out c);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, world[3]), cam, out d);

        // หา min/max ใน local
        float minX = Mathf.Min(a.x, b.x, c.x, d.x);
        float maxX = Mathf.Max(a.x, b.x, c.x, d.x);
        float minY = Mathf.Min(a.y, b.y, c.y, d.y);
        float maxY = Mathf.Max(a.y, b.y, c.y, d.y);

        Vector2 size = new Vector2(maxX - minX, maxY - minY) + padding * 2f;
        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);

        focusFrame.anchoredPosition = center;
        focusFrame.sizeDelta = size;

        StartPulse();
    }

    // -------------------- Animations --------------------

    IEnumerator FadeInAndPop()
    {
        overlay.alpha = 0f;
        if (cardRoot) cardRoot.localScale = Vector3.one * 0.85f;
        gameObject.SetActive(true);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            overlay.alpha = Mathf.SmoothStep(0f, 1f, t);
            if (cardRoot) cardRoot.localScale = Vector3.one * Mathf.SmoothStep(0.85f, 1f, t);
            yield return null;
        }
    }

    IEnumerator FadeOut()
    {
        float t = 0f;
        float startA = overlay.alpha;
        Vector3 startS = cardRoot ? cardRoot.localScale : Vector3.one;
        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            overlay.alpha = Mathf.Lerp(startA, 0f, t);
            if (cardRoot) cardRoot.localScale = Vector3.Lerp(startS, Vector3.one * 0.95f, t);
            yield return null;
        }
        HideImmediate();
    }

    // -------------------- Focus Pulse --------------------

    void StartPulse()
    {
        StopPulse();
        pulseCo = StartCoroutine(Pulse());
    }

    void StopPulse()
    {
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = null;
        if (focusFrame) focusFrame.localScale = Vector3.one;
        if (focusImage)
        {
            var c = focusImage.color;
            focusImage.color = new Color(c.r, c.g, c.b, 1f);
        }
    }

    IEnumerator Pulse()
    {
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * pulseSpeed;
            float s = 1f + Mathf.Sin(t) * pulseScale;
            if (focusFrame) focusFrame.localScale = new Vector3(s, s, 1f);

            if (focusImage)
            {
                var c = focusImage.color;
                float a = 0.6f + (Mathf.Sin(t) * 0.2f);
                focusImage.color = new Color(c.r, c.g, c.b, a);
            }

            yield return null;
        }
    }

    public void ClearSpotlightHole()
    {
        if (dimImage) dimImage.enabled = true;
        if (cutTop) cutTop.gameObject.SetActive(false);
        if (cutBottom) cutBottom.gameObject.SetActive(false);
        if (cutLeft) cutLeft.gameObject.SetActive(false);
        if (cutRight) cutRight.gameObject.SetActive(false);
    }

    // รับ “กลุ่มเป้า” แล้วคำนวณกรอบรวม → เจาะรู
    public void SpotlightTargets(IEnumerable<RectTransform> targets, float padding = 8f)
    {
        if (targets == null) { ClearSpotlightHole(); return; }

        EnsureCutout();

        var canvasRT = (RectTransform)mainCanvas.transform;
        Camera cam = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;

        bool any = false;
        Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
        var wc = new Vector3[4];

        foreach (var t in targets)
        {
            if (!t) continue;
            t.GetWorldCorners(wc);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, wc[0]), cam, out var A);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, wc[2]), cam, out var C);
            min = Vector2.Min(min, Vector2.Min(A, C));
            max = Vector2.Max(max, Vector2.Max(A, C));
            any = true;
        }
        if (!any) { ClearSpotlightHole(); return; }

        min -= new Vector2(padding, padding);
        max += new Vector2(padding, padding);
        SetCutoutRect(min, max);
    }

    void EnsureCutout()
    {
        if (cutTop) return;
        cutTop = CreatePane("CutoutTop");
        cutBottom = CreatePane("CutoutBottom");
        cutLeft = CreatePane("CutoutLeft");
        cutRight = CreatePane("CutoutRight");
    }

    RectTransform CreatePane(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(overlay.transform, false);
        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();
        img.raycastTarget = dimImage && dimImage.raycastTarget;
        img.color = dimImage ? dimImage.color : new Color(0, 0, 0, 0.6f);
        return rt;
    }

    void SetCutoutRect(Vector2 min, Vector2 max)
    {
        dimImage.enabled = false; // ซ่อนพื้นดำเต็มจอ แล้วใช้ 4 แผ่นแทน

        var canvasRT = (RectTransform)mainCanvas.transform;
        var rect = canvasRT.rect;

        // Top
        cutTop.gameObject.SetActive(true);
        cutTop.anchorMin = new Vector2(0, Mathf.InverseLerp(rect.yMin, rect.yMax, max.y));
        cutTop.anchorMax = new Vector2(1, 1);
        cutTop.offsetMin = cutTop.offsetMax = Vector2.zero;

        // Bottom
        cutBottom.gameObject.SetActive(true);
        cutBottom.anchorMin = new Vector2(0, 0);
        cutBottom.anchorMax = new Vector2(1, Mathf.InverseLerp(rect.yMin, rect.yMax, min.y));
        cutBottom.offsetMin = cutBottom.offsetMax = Vector2.zero;

        // Left
        cutLeft.gameObject.SetActive(true);
        cutLeft.anchorMin = new Vector2(0, Mathf.InverseLerp(rect.yMin, rect.yMax, min.y));
        cutLeft.anchorMax = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, min.x), Mathf.InverseLerp(rect.yMin, rect.yMax, max.y));
        cutLeft.offsetMin = cutLeft.offsetMax = Vector2.zero;

        // Right
        cutRight.gameObject.SetActive(true);
        cutRight.anchorMin = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, max.x), Mathf.InverseLerp(rect.yMin, rect.yMax, min.y));
        cutRight.anchorMax = new Vector2(1, Mathf.InverseLerp(rect.yMin, rect.yMax, max.y));
        cutRight.offsetMin = cutRight.offsetMax = Vector2.zero;
    }
}