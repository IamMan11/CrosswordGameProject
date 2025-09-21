using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoardHighlighterUI : MonoBehaviour
{
    [Header("Canvas / Layer")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform layer;

    [Header("Visual")]
    [SerializeField] private Sprite boxSprite;
    [SerializeField] private Color letterMulColor = new Color(0.15f, 0.7f, 1f, 0.35f);
    [SerializeField] private Color wordMulColor   = new Color(1f, 0.55f, 0.1f, 0.35f);
    [SerializeField] private Color specialColor   = new Color(0.3f, 1f, 0.4f, 0.35f);
    [SerializeField] private Color cardSlotColor  = new Color(1f, 1f, 0.2f, 0.35f);
    [SerializeField] private float pulseScale = 0.07f;
    [SerializeField] private float pulseSpeed = 2.2f;

    private readonly List<Image> pool = new();
    private readonly List<Coroutine> running = new();

    private void Awake()
    {
        if (!canvas) canvas = GetComponentInParent<Canvas>();
        if (!layer)
        {
            var go = new GameObject("HighlightLayer", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            layer = (RectTransform)go.transform;
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
        }
    }

    public void Clear()
    {
        foreach (var co in running) if (co != null) StopCoroutine(co);
        running.Clear();
        foreach (var img in pool) if (img) img.gameObject.SetActive(false);
    }

    public void PulseOne(RectTransform cell, HighlightKind kind, float duration = 1.6f)
    {
        if (!cell) return;
        PulseCells(new List<RectTransform> { cell }, kind, duration);
    }

    public void PulseCells(List<RectTransform> cells, HighlightKind kind, float duration = 1.6f)
    {
        if (cells == null) return;

        Color color = specialColor;
        switch (kind)
        {
            case HighlightKind.LetterMultiplier: color = letterMulColor; break;
            case HighlightKind.WordMultiplier:   color = wordMulColor;   break;
            case HighlightKind.CardSlot:         color = cardSlotColor;  break;
            default:                             color = specialColor;   break;
        }

        foreach (var cell in cells)
        {
            if (!cell) continue;
            var img = Get();
            FitToTarget(img.rectTransform, cell);
            img.color = color;
            img.gameObject.SetActive(true);
            running.Add(StartCoroutine(CoPulse(img.rectTransform, img, duration)));
        }
    }

    public void FlashPath(List<RectTransform> cells, Color color, float duration = 1.2f)
    {
        if (cells == null) return;
        foreach (var cell in cells)
        {
            if (!cell) continue;
            var img = Get();
            FitToTarget(img.rectTransform, cell);
            img.color = color;
            img.gameObject.SetActive(true);
            running.Add(StartCoroutine(CoFlash(img, duration)));
        }
    }

    // internals
    Image Get()
    {
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] && !pool[i].gameObject.activeSelf)
                return pool[i];

        var go = new GameObject("hl", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(layer, false);
        var rt = (RectTransform)go.transform;
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = boxSprite;
        img.type = boxSprite ? Image.Type.Sliced : Image.Type.Simple;

        pool.Add(img);
        return img;
    }

    void FitToTarget(RectTransform dst, RectTransform target)
    {
        var canvasRT = (RectTransform)canvas.transform;
        var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector3[] wc = new Vector3[4];
        target.GetWorldCorners(wc);
        Vector2 a, c;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, wc[0]), cam, out a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, RectTransformUtility.WorldToScreenPoint(cam, wc[2]), cam, out c);

        dst.anchoredPosition = (a + c) * 0.5f;
        dst.sizeDelta = new Vector2(Mathf.Abs(c.x - a.x), Mathf.Abs(c.y - a.y));
        dst.localScale = Vector3.one;
        dst.localRotation = Quaternion.identity;
    }

    IEnumerator CoPulse(RectTransform rt, Image img, float time)
    {
        float t = 0f;
        Color baseCol = img.color;
        while (t < time)
        {
            t += Time.deltaTime;
            float s = 1f + Mathf.Sin(t * Mathf.PI * pulseSpeed) * pulseScale;
            rt.localScale = new Vector3(s, s, 1f);

            Color c = baseCol;
            c.a = 0.15f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * pulseSpeed));
            img.color = c;

            yield return null;
        }
        rt.gameObject.SetActive(false);
    }

    IEnumerator CoFlash(Image img, float time)
    {
        float t = 0f;
        Color c0 = img.color; c0.a = Mathf.Max(c0.a, 0.4f);
        Color c1 = c0; c1.a = 0f;
        while (t < time)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(c0, c1, t / time);
            yield return null;
        }
        img.gameObject.SetActive(false);
    }

    public enum HighlightKind { LetterMultiplier, WordMultiplier, SpecialTile, CardSlot }
}