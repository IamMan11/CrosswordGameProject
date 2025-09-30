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
    [SerializeField] private CanvasGroup fadeGroup;

    [Header("Timings (sec)")]
    [SerializeField] private float fadeOutTime = 0.40f;
    [SerializeField] private float fadeInTime  = 0.40f;

    [Header("Block Input While Transition")]
    [SerializeField] private bool blockRaycastsDuringTransition = true;

    [Header("Sorting")]
    [SerializeField] private bool autoRaiseOrder = true;
    [SerializeField] private int fixedSortingOrder = 32760;

    Canvas _canvas;
    bool _busy;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);

        EnsureOverlayReady();
        BringToFront();

        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += (_, __) => BringToFront();
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= (_, __) => BringToFront();
    }

    void OnActiveSceneChanged(Scene oldS, Scene newS)
    {
        BringToFront();
        // บันทึกชื่อซีนล่าสุด + เซฟ progress เมื่อมีการเปลี่ยนซีน
        PlayerProgressSO.Instance?.SetLastScene(newS.name);
        PlayerProgressSO.Instance?.SaveToPrefs();
    }

    public static void LoadScene(string sceneName)
    {
        if (I == null) { Debug.LogError("[SceneTransitioner] No instance."); return; }
        if (I._busy) return;
        I.StartCoroutine(I.LoadRoutine(sceneName));
    }
    public void LoadSceneButton(string sceneName) => LoadScene(sceneName);

    IEnumerator LoadRoutine(string sceneName)
    {
        _busy = true;
        BringToFront();

        if (blockRaycastsDuringTransition)
        {
            fadeGroup.blocksRaycasts = true;
            fadeGroup.interactable = true;
        }

        yield return FadeAlpha(1f, fadeOutTime);   // 0 -> 1

        // เซฟ progress ก่อนย้ายซีน เผื่อเกมปิดระหว่างโหลด
        PlayerProgressSO.Instance?.SaveToPrefs();

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        yield return new WaitForEndOfFrame();
        BringToFront();

        yield return FadeAlpha(0f, fadeInTime);    // 1 -> 0

        if (blockRaycastsDuringTransition)
        {
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
        }

        _busy = false;
    }

    IEnumerator FadeAlpha(float target, float duration)
    {
        if (duration <= 0f) { fadeGroup.alpha = target; yield break; }

        float start = fadeGroup.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            fadeGroup.alpha = Mathf.Lerp(start, target, k);
            yield return null;
        }
        fadeGroup.alpha = target;
    }

    void EnsureOverlayReady()
    {
        _canvas = GetComponentInChildren<Canvas>(true);
        if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.overrideSorting = true;

        if (fadeGroup == null)
        {
            var go = new GameObject("FadeOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            fadeGroup = go.GetComponent<CanvasGroup>();
            var img = go.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
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
