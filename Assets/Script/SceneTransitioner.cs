// Assets/Script/SceneTransitioner.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public class SceneTransitioner : MonoBehaviour
{
    public static SceneTransitioner I { get; private set; }

    [Header("Overlay (สร้างอัตโนมัติถ้าไม่ใส่)")]
    [SerializeField] private CanvasGroup fadeGroup;   // ม่านดำเต็มจอ (คุมด้วย alpha)

    [Header("Timings (sec)")]
    [SerializeField] private float fadeOutTime = 0.40f; // เวลาดำเข้า
    [SerializeField] private float fadeInTime  = 0.40f; // เวลาเฟดออกหลังโหลด

    [Header("Block Input While Transition")]
    [SerializeField] private bool blockRaycastsDuringTransition = true;

    [Header("Sorting")]
    [SerializeField] private bool autoRaiseOrder = true;  // ยก overlay ให้อยู่บนสุดเสมอ
    [SerializeField] private int fixedSortingOrder = 32760;

    Canvas _canvas;
    bool _busy;

    void Awake()
    {
        // singleton + ค้างข้ามซีน
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (transform.parent != null) transform.SetParent(null, true); // ต้องเป็น root
        DontDestroyOnLoad(gameObject);

        EnsureOverlayReady(); // มี Canvas/CanvasGroup/ภาพดำครบแน่
        BringToFront();

        // เริ่มโปร่งและไม่บังคลิก
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        SceneManager.activeSceneChanged += (_, __) => BringToFront();
        SceneManager.sceneLoaded += (_, __) => BringToFront();
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= (_, __) => BringToFront();
        SceneManager.sceneLoaded -= (_, __) => BringToFront();
    }

    // ===== API =====
    public static void LoadScene(string sceneName)
    {
        if (I == null) { Debug.LogError("[SceneTransitioner] No instance."); return; }
        if (I._busy) return;
        I.StartCoroutine(I.LoadRoutine(sceneName));
    }
    public void LoadSceneButton(string sceneName) => LoadScene(sceneName);

    // ===== Flow หลัก: เฟดดำ -> โหลด -> เฟดออก =====
    IEnumerator LoadRoutine(string sceneName)
    {
        _busy = true;
        BringToFront();

        if (blockRaycastsDuringTransition)
        {
            fadeGroup.blocksRaycasts = true;
            fadeGroup.interactable = true;
        }

        // 1) เฟดดำเต็มจอ
        yield return FadeAlpha(1f, fadeOutTime);   // 0 -> 1

        // 2) โหลดซีนใต้ม่านดำ
        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // ให้ซีนใหม่วาด 1 เฟรม โดยยังมืดสนิท
        yield return new WaitForEndOfFrame();
        BringToFront();

        // 3) เฟดออกจนเห็นทั้งซีน
        yield return FadeAlpha(0f, fadeInTime);    // 1 -> 0

        if (blockRaycastsDuringTransition)
        {
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }

        _busy = false;
    }

    // ===== Helpers =====
    IEnumerator FadeAlpha(float target, float duration)
    {
        if (duration <= 0f)
        {
            fadeGroup.alpha = target;
            yield break;
        }

        float start = fadeGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // ไม่ขึ้นกับ Time.timeScale
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            fadeGroup.alpha = Mathf.Lerp(start, target, k);
            yield return null;
        }
        fadeGroup.alpha = target;
    }

    void EnsureOverlayReady()
    {
        // หา/สร้าง Canvas ลูก + CanvasGroup + ภาพดำเต็มจอ
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;

        if (fadeGroup == null)
        {
            // สร้างโครง UI
            var go = new GameObject("FadeOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            fadeGroup = go.GetComponent<CanvasGroup>();
            var img = go.GetComponent<Image>();
            img.color = Color.black;      // ดำทึบ
            img.raycastTarget = false;    // ภาพไม่ต้องรับคลิก (ให้ CanvasGroup จัดการ)
        }
    }

    void BringToFront()
    {
        if (_canvas == null) return;

        if (autoRaiseOrder)
        {
            int maxOrder = 0;
            var canvases = FindObjectsOfType<Canvas>(false);
            for (int i = 0; i < canvases.Length; i++)
                if (canvases[i] != _canvas)
                    maxOrder = Mathf.Max(maxOrder, canvases[i].sortingOrder);

            _canvas.sortingOrder = Mathf.Clamp(maxOrder + 1, -32760, 32760);
        }
        else
        {
            _canvas.sortingOrder = fixedSortingOrder;
        }
    }
}
