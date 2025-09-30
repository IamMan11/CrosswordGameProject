using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SimpleTutorialUI : MonoBehaviour
{
    public static SimpleTutorialUI Instance { get; private set; }
    [Header("Refs")]
    [SerializeField] Canvas mainCanvas;
    [SerializeField] RectTransform overlay;    // ครอบเต็มจอ เอาไว้จับคลิก
    [SerializeField] Image dimImage;           // มืดพื้นหลัง (optional)
    [SerializeField] RectTransform subtitleRoot;
    [SerializeField] TMP_Text subtitleText;
    [SerializeField] RectTransform focusFrame; // 9-sliced border image
    [Header("Skip Button")]
    [SerializeField] Button skipButton;     // ← ลากปุ่ม Skip ที่คุณตั้งไว้มาใส่ Inspector
    System.Action onSkip;                   // ← callback สำหรับผู้ควบคุม tutorial
    [Header("Speaker (Character Image)")]
    [SerializeField] RectTransform speakerRoot;  // ลาก RectTransform ของกรุ๊ปตัวละคร (อยู่ใต้ Canvas)
    [SerializeField] Image speakerImage;         // ลาก Image ที่ใช้แสดงรูปตัวละคร
    [SerializeField] bool speakerOnTop = true; // ให้ลอยอยู่บนสุดเหนือ overlay/กรอบ/ซับ ฯลฯ

    List<RectTransform> _frames = new List<RectTransform>(); // index 0 = focusFrame เดิม
    Image focusImg;
    static Sprite _fallbackSprite;
    static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite) return _fallbackSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f,0.5f), 100f);
        return _fallbackSprite;
    }

    System.Action onOverlayClick;
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);          // ← อยู่ข้ามซีน

        if (overlay)
        {
            var btn = overlay.GetComponent<Button>() ?? overlay.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onOverlayClick?.Invoke());
        }
        HideAll();

        // ---- ensure focus frame has a drawable image ----
        if (focusFrame)
        {
            focusImg = focusFrame.GetComponent<Image>() ?? focusFrame.gameObject.AddComponent<Image>();
            if (!focusImg.sprite) focusImg.sprite = GetFallbackSprite();
            focusImg.raycastTarget = false;
            if (focusImg.color.a <= 0.01f) focusImg.color = new Color(1, 1, 1, 0.9f);
            if (_frames.Count == 0) _frames.Add(focusFrame);
        }
        if (skipButton)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(ClickSkip);
            skipButton.gameObject.SetActive(false); // เริ่มต้นซ่อน
        }
    }
    public bool HasOverlayHandler() => onOverlayClick != null;
    public void HideAll()
    {
        if (subtitleRoot) subtitleRoot.gameObject.SetActive(false);
        if (dimImage) dimImage.enabled = false;
        if (overlay) overlay.gameObject.SetActive(false);
        onOverlayClick = null;
        // ซ่อนกรอบทั้งหมด
        foreach (var f in _frames) if (f) f.gameObject.SetActive(false);
        gameObject.SetActive(false);
        if (skipButton) skipButton.gameObject.SetActive(false);
        onSkip = null;
    }
    /// <summary>ตั้ง callback และกำหนดให้ปุ่ม Skip แสดง/ซ่อน</summary>
    public void SetSkip(System.Action onSkipCallback, bool show = true)
    {
        onSkip = onSkipCallback;
        if (skipButton)
        {
            skipButton.gameObject.SetActive(show);
            if (show) skipButton.transform.SetAsLastSibling(); // ให้อยู่เหนือ overlay
        }
    }
        // == API ==
    public void ShowSpeaker(Sprite sprite, RectTransform anchorOrNull, Vector2 offset, bool mirrorX = false, float alpha = 1f)
    {
        if (!speakerRoot || !speakerImage || sprite == null) return;

        gameObject.SetActive(true);
        speakerRoot.gameObject.SetActive(true);   // ← ปลุกตัวละครขึ้น
        speakerImage.enabled = true;

        speakerImage.sprite = sprite;
        var c = speakerImage.color; c.a = alpha; speakerImage.color = c;
        speakerImage.raycastTarget = false;

        PositionTo(anchorOrNull, offset, speakerRoot);

        var s = speakerRoot.localScale;
        s.x = Mathf.Abs(s.x) * (mirrorX ? -1f : 1f);
        speakerRoot.localScale = s;

        if (speakerOnTop) speakerRoot.SetAsLastSibling();
    }
    public void HideSpeaker()
    {
        if (!speakerRoot || !speakerImage) return;
        speakerRoot.gameObject.SetActive(false);
    }

    /// <summary>เรียกจากปุ่ม Skip</summary>
    public void ClickSkip()
    {
        // ให้สิทธิผู้ควบคุมจัดการ state ก่อน
        onSkip?.Invoke();
        // แล้วซ่อนทุกอย่าง
        HideAll();
    }
    // === helper สำหรับดึงกรอบตาม index (โคลนจากกรอบแรก) ===
    RectTransform GetFrame(int index)
    {
        if (_frames.Count == 0 && focusFrame) _frames.Add(focusFrame);
        while (_frames.Count <= index)
        {
            var src = _frames[0];
            var inst = Instantiate(src, src.parent);
            inst.name = $"FocusFrame_{_frames.Count}";
            var img = inst.GetComponent<Image>() ?? inst.gameObject.AddComponent<Image>();
            if (!img.sprite) img.sprite = GetFallbackSprite();
            img.raycastTarget = false;
            _frames.Add(inst);
        }
        return _frames[index];
    }
    // === แปลงตำแหน่ง/ขนาด ===
    void LayoutFrame(RectTransform frame, RectTransform target, float padding)
    {
        if (!frame || !target) return;
        var canvasRT = (RectTransform)mainCanvas.transform;
        var cam = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;

        Vector3[] wc = new Vector3[4];
        target.GetWorldCorners(wc);
        Vector2 a, b;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
            RectTransformUtility.WorldToScreenPoint(cam, wc[0]), cam, out a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT,
            RectTransformUtility.WorldToScreenPoint(cam, wc[2]), cam, out b);

        var center = (a + b) * 0.5f;
        var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y)) + Vector2.one * (padding * 2f);

        frame.anchoredPosition = center;
        frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        frame.SetAsLastSibling();
        frame.gameObject.SetActive(true);
    }
    public void ConfigureOverlay(System.Action onClick, bool enableDim, float dimAlpha = 0.6f)
    {
        gameObject.SetActive(true);
        if (overlay)
        {
            overlay.gameObject.SetActive(true);
            var img = overlay.GetComponent<Image>() ?? overlay.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);     // โปร่งใส แค่รับ Raycast
            img.raycastTarget = true;
        }
        onOverlayClick = onClick;

        if (dimImage)
        {
            dimImage.enabled = enableDim;
            var c = dimImage.color; c.a = enableDim ? dimAlpha : 0f; dimImage.color = c;
        }
    }

    public void ShowSubtitle(string text, RectTransform anchorOrNull, Vector2 offset)
    {
        gameObject.SetActive(true);
        if (subtitleRoot) subtitleRoot.gameObject.SetActive(true);
        if (subtitleText) subtitleText.text = text;
        PositionTo(anchorOrNull, offset, subtitleRoot);
    }

    // === API เดิม (single) → เรียก multi ===
    public void ShowHighlight(RectTransform target, float padding)
    {
        if (target == null) { ShowHighlights(null, padding); return; }
        ShowHighlights(new List<RectTransform> { target }, padding);
    }

    // === NEW: multi-highlight ===
    public void ShowHighlights(IList<RectTransform> targets, float padding)
    {
        // ซ่อนทั้งหมดก่อน
        int used = 0;
        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == null) continue;
                var f = GetFrame(used);
                LayoutFrame(f, targets[i], padding);
                used++;
            }
        }
        // ซ่อนกรอบที่เกิน
        for (int i = used; i < _frames.Count; i++)
            if (_frames[i]) _frames[i].gameObject.SetActive(false);
    }


    void PositionTo(RectTransform targetOrNull, Vector2 offset, RectTransform what)
    {
        if (!what) return;
        var canvasRT = (RectTransform)mainCanvas.transform;
        var cam = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;

        Vector2 anchored = Vector2.zero; // กลางจอเมื่อไม่มี anchor
        if (targetOrNull && targetOrNull != canvasRT)
        {
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
        what.anchoredPosition = anchored + offset;
    }
    public void SetMainCanvas(Canvas c)
    {
        if (c) mainCanvas = c;
    }
}   
