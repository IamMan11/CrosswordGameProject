using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SimpleTutorialUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Canvas mainCanvas;
    [SerializeField] RectTransform overlay;    // ครอบเต็มจอ เอาไว้จับคลิก
    [SerializeField] Image dimImage;           // มืดพื้นหลัง (optional)
    [SerializeField] RectTransform subtitleRoot;
    [SerializeField] TMP_Text subtitleText;
    [SerializeField] RectTransform focusFrame; // 9-sliced border image
    Image focusImg;
    static Sprite _fallbackSprite;
    static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite) return _fallbackSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        return _fallbackSprite;
    }

    System.Action onOverlayClick;

    void Awake()
    {
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
            if (focusImg.color.a <= 0.01f) focusImg.color = new Color(1, 1, 1, 0.9f); // มองเห็นชัด
        }
    }

    public void HideAll()
    {
        if (subtitleRoot) subtitleRoot.gameObject.SetActive(false);
        if (focusFrame) focusFrame.gameObject.SetActive(false);
        if (dimImage) dimImage.enabled = false;
        if (overlay) overlay.gameObject.SetActive(false);
        onOverlayClick = null;
        gameObject.SetActive(false);
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

    public void ShowHighlight(RectTransform target, float padding)
    {
        if (!focusFrame)
            return;

        if (target == null)
        {
            focusFrame.gameObject.SetActive(false);
            return;
        }

        if (!focusImg)
            focusImg = focusFrame.GetComponent<Image>() ?? focusFrame.gameObject.AddComponent<Image>();
        if (!focusImg.sprite)
            focusImg.sprite = GetFallbackSprite();
        focusImg.raycastTarget = false;

        // คำนวณกรอบรอบ target
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

        focusFrame.anchoredPosition = center;
        focusFrame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        focusFrame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);

        // ให้กรอบอยู่บนสุดเสมอ (เหนือ overlay/dim/subtitle)
        focusFrame.SetAsLastSibling();
        focusFrame.gameObject.SetActive(true);
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
}   
