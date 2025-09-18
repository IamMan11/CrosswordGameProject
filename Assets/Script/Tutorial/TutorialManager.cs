using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }
    public static System.Action OnTutorialFinished;

    const string KEY_REGISTRY = "TUT_KEYS_REGISTRY"; // เก็บคีย์ที่เราเขียนไว้ เพื่อให้ Editor/Dev tools ล้างได้

    [Header("Toggle / Persistence")]
    public bool forceTutorial = false;
    public bool startOnAwake = true;
    public bool skipIfAlreadyDone = true;
    public string playerPrefsKey = "TUT_START";
    [Header("Dev / Versioning")]
    public bool useSceneAndVersionKey = false; // ถ้าเปิดจะใช้ key แยกตามฉาก+version
    public string tutorialVersion = "v1";
    public Button skipTutorialButton;

    [Header("Overlay (พื้นดำอ่อน)")]
    [SerializeField] CanvasGroup overlay;        // ใต้ Canvas
    [SerializeField] Image overlayImage;         // Image สีดำที่ใช้เฟด (พื้นเต็มจอ)
    [SerializeField, Range(0f, 1f)] float overlayTargetAlpha = 0.78f;
    [SerializeField] float overlayFadeDuration = 0.25f;
    [SerializeField] bool useUnscaledTime = true;

    [Header("Raycast Filter (บังคลิก)")]
    [SerializeField] OverlayHoleRaycastFilter rayFilter;

    [Header("Dialog / Text")]
    [SerializeField] GameObject characterPanel;
    [SerializeField] TextMeshProUGUI bodyText;
    [SerializeField] Image portraitImage;
    [SerializeField] TextMeshProUGUI nameText;
    [SerializeField] AudioSource voiceSource;

    [Header("Highlight / Pointers")]
    [SerializeField] RectTransform highlightFrame;
    [SerializeField] RectTransform handPointer;
    [SerializeField] Button continueAnywhereBtn; // ปุ่มคลุมจอ (tap anywhere)
    [SerializeField, Range(0f,1f)] float highlightAlpha = 0.35f; // ความใสของกรอบไฮไลท์ (0=ใส,1=ทึบ)

    [Header("Spotlight (ทำสว่างเฉพาะที่กดได้)")]
    [SerializeField] Transform spotlightRoot;
    [SerializeField] Sprite spotlightSprite;
    [SerializeField, Range(0f, 1f)] float spotlightAlpha = 1f;
    [SerializeField] float spotlightPadding = 24f;
    [SerializeField] float spotlightSoftScale = 1.2f;

    [Header("Spotlight Style (ทางเลือก)")]
    [SerializeField] bool spotlightUsePulse = false;
    [SerializeField] float spotlightPulseScale = 1.08f;
    [SerializeField] float spotlightPulseSpeed = 2.0f;

    [Header("Key Targets (ถ้ามี)")]
    public Button confirmButton;
    public Button dictionaryButton;

    // ====== Cutout (รูโปร่งใส) ======
    [Header("Cutout (รูโปร่งใส)")]
    [SerializeField] Color dimColor = new Color(0, 0, 0, 0.78f); // สีพื้นรอบรู (alpha ใช้ overlayTargetAlpha)
    [SerializeField] float cutoutExtraPadding = 18f;             // ขยายรูจากปุ่มเพิ่มเล็กน้อย
    readonly List<Image> dimBlocks = new();                      // 4 บล็อกดำรอบรู

    public enum WaitType
    {
        None, Time,
        TilePlacedOnBoard, ConfirmPressed, ScoreIncreased, DictionaryOpened,
        ShopItemPurchased, CardUsed,
        WordValidated, WordRejected, ScoreDecreased,
        LetterDiscarded, SpecialLetterPlaced,
        CardRolled, CardDiscarded, CardCombined,
        LevelTimerStarted, LevelObjectiveMet
    }

    [System.Serializable]
    public class Step
    {
        [Header("ข้อความ/ตัวละคร")]
        public string id;
        [TextArea] public string message = "…";
        public bool useCharacter = true;
        public string speakerName = "โค้ชครอส";
        public Sprite portrait;

        [Header("Typewriter")]
        public bool useTypewriter = true;
        [Range(10, 120)] public float typeSpeed = 45f;
        public AudioClip voiceBeep;
        [Range(0f, 0.1f)] public float beepInterval = 0.03f;

        [Header("การรอเหตุการณ์")]
        public WaitType waitFor = WaitType.None;
        public float waitTime = 0f;

        [Header("โฟกัส / อินพุต")]
        public RectTransform highlightTarget;
        public bool blockInput = true;                 // บล็อกทั้งจอ?
        public bool tapAnywhereToContinue = false;     // แตะพื้นไปต่อ
        public bool pressNextToContinue = false;       // ต้องกดปุ่ม "ต่อไป"
        public Vector2 handOffset = new Vector2(0, -80);

        [Header("อนุญาตให้คลิก")]
        public bool allowClickOnHighlight = false;
        public RectTransform[] allowClickExtraRects;
        public float allowClickPadding = 16f;

        [Header("สปอตไลต์")]
        public bool showSpotlightOnAllowed = true;
    }

    [Header("Steps (เรียงใน Inspector)")]
    [SerializeField] private Button nextButton;
    public Step[] steps;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;

    // ---------- internal ----------
    int stepIndex = -1;
    int cachedTotalScore = 0;
    Coroutine typingCo, overlayFadeCo;
    bool typing, skipTypingThisStep, started;
    readonly List<Image> activeSpotlights = new();
    // stuck timer
    Coroutine stuckTimerCo;
    [SerializeField] float stepStuckTimeout = 6f; // seconds before consider stuck

    // flags (เผื่อระบบจริง)
    bool dictionaryOpenedFlag, shopPurchasedFlag, cardUsedFlag;
    bool wordValidated, wordRejected, scoreDecreased;
    bool letterDiscarded, specialPlaced;
    bool cardRolled, cardDiscarded, cardCombined;
    bool levelTimerStarted, levelObjectiveMet;
    bool tilePlacedFlag;

    void OnEnable()
    {
        Instance = this;
        transform.SetAsLastSibling();

        if (characterPanel) characterPanel.SetActive(false);

        if (overlay)
        {
            overlay.alpha = 1f;                 // คุมความทึบด้วย overlayImage แทน
            overlay.blocksRaycasts = false;
        }
        if (overlayImage)
        {
            var c = overlayImage.color; c.a = 0f;
            overlayImage.color = c;
            overlayImage.raycastTarget = true;
        }

        ValidateBindings();
        // If FTU flow controller exists and is active, defer to it (it will call StartTutorialNow on the proper scenes)
        bool ftuActive = false;
        try { ftuActive = FtuFlowController.IsFTUActive(); } catch { }
        if (!ftuActive && forceTutorial && startOnAwake) StartTutorialNow();
    }

    void OnDisable()
    {
        if (skipTutorialButton)
            skipTutorialButton.onClick.RemoveListener(SkipTutorialNow);
    }

    void Start()
    {
        if (debugLogs)
            Debug.Log($"[Tutorial] Start: force={forceTutorial}, startOnAwake={startOnAwake}, key={playerPrefsKey}, done={PlayerPrefs.GetInt(playerPrefsKey, 0)}");

        try
        {
            if (confirmButton == null && TurnManager.Instance != null)
                confirmButton = TurnManager.Instance.confirmBtn;
            cachedTotalScore = (TurnManager.Instance != null) ? TurnManager.Instance.TotalScore : 0;
        }
        catch { }

        bool already = IsTutorialDone();
        if (startOnAwake && (forceTutorial || !already)) StartTutorialNow();
    }

    public string GetPrefsKey()
    {
        if (useSceneAndVersionKey)
        {
            string scene = SceneManager.GetActiveScene().name;
            return playerPrefsKey + "_" + scene + "_" + tutorialVersion;
        }
        return playerPrefsKey;
    }

    bool IsTutorialDone()
    {
        if (!skipIfAlreadyDone) return false;
        try { return PlayerPrefs.GetInt(GetPrefsKey(), 0) == 1; } catch { return false; }
    }

    // Registry helpers so Editor can find which PlayerPrefs keys we touched
    public void RegisterPrefsKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        try
        {
            var existing = PlayerPrefs.GetString(KEY_REGISTRY, "");
            if (string.IsNullOrEmpty(existing))
            {
                PlayerPrefs.SetString(KEY_REGISTRY, key);
            }
            else
            {
                var list = existing.Split('|');
                foreach (var k in list) if (k == key) return; // already have
                PlayerPrefs.SetString(KEY_REGISTRY, existing + "|" + key);
            }
            PlayerPrefs.Save();
        }
        catch { }
    }

    // expose for Editor/tools
    public static string[] GetRegisteredKeys()
    {
        try
        {
            var s = PlayerPrefs.GetString(KEY_REGISTRY, "");
            if (string.IsNullOrEmpty(s)) return new string[0];
            return s.Split('|');
        }
        catch { return new string[0]; }
    }

    [ContextMenu("Start Tutorial Now")]
    public void CM_StartTutorialNow() => StartTutorialNow();

    public void StartTutorialNow()
    {
        if (started) return;
        started = true;

        ValidateBindings();
        if (steps == null || steps.Length == 0)
        {
            Debug.LogError("[Tutorial] No steps assigned.");
            return;
        }
        StopAllCoroutines();
        StartCoroutine(RunTutorial());
    }

    public void FinishNow()
    {
        StopAllCoroutines();
        ClearSpotlights();
        ClearDimBlocks();
        ShowOverlay(false, false);
        // บันทึกสถานะ tutorial ว่าเสร็จแล้ว (respect option)
        if (skipIfAlreadyDone) { PlayerPrefs.SetInt(GetPrefsKey(), 1); PlayerPrefs.Save(); }
        OnTutorialFinished?.Invoke();
    }

    public void SkipTutorialNow()
    {
        // บังคับให้ถือว่าเสร็จ และปิด UI
        try { RegisterPrefsKey(GetPrefsKey()); PlayerPrefs.SetInt(GetPrefsKey(), 1); PlayerPrefs.Save(); } catch { }
        FinishNow();
    }

    IEnumerator RunTutorial()
    {
        // Delegate to the generalized runner starting at index 0
        yield return StartCoroutine(RunTutorialFrom(0));
    }

    // Run tutorial starting from a specific step index
    public IEnumerator RunTutorialFrom(int startIndex)
    {
        ShowOverlay(true, true);

        for (int i = startIndex; i < steps.Length; i++)
        {
            stepIndex = i;
            var s = steps[i];

            SetupStepUI(s);
            yield return PlayStepIntro(s);

            switch (s.waitFor)
            {
                case WaitType.None:
                    yield return WaitTapIfNeeded(s);
                    break;

                case WaitType.Time:
                    if (useUnscaledTime) yield return new WaitForSecondsRealtime(Mathf.Max(0f, s.waitTime));
                    else yield return new WaitForSeconds(Mathf.Max(0f, s.waitTime));
                    yield return WaitTapIfNeeded(s);
                    break;

                case WaitType.TilePlacedOnBoard:
                    tilePlacedFlag = false;
                    yield return new WaitUntil(() => tilePlacedFlag);
                    yield return WaitTapIfNeeded(s);
                    break;

                case WaitType.ConfirmPressed:
                {
                    bool pressed = false;
                    UnityEngine.Events.UnityAction handler = () => pressed = true;
                    if (confirmButton != null) confirmButton.onClick.AddListener(handler);
                    yield return new WaitUntil(() => pressed);
                    if (confirmButton != null) confirmButton.onClick.RemoveListener(handler);
                    yield return WaitTapIfNeeded(s);
                    break;
                }

                case WaitType.ScoreIncreased:
                {
                    int startScore = GetTotalScore_Safe();
                    yield return new WaitUntil(() => GetTotalScore_Safe() > startScore);
                    yield return WaitTapIfNeeded(s);
                    break;
                }

                case WaitType.DictionaryOpened: dictionaryOpenedFlag = false; yield return new WaitUntil(() => dictionaryOpenedFlag); yield return WaitTapIfNeeded(s); break;
                case WaitType.ShopItemPurchased: shopPurchasedFlag = false; yield return new WaitUntil(() => shopPurchasedFlag); yield return WaitTapIfNeeded(s); break;
                case WaitType.CardUsed: cardUsedFlag = false; yield return new WaitUntil(() => cardUsedFlag); yield return WaitTapIfNeeded(s); break;

                case WaitType.WordValidated: wordValidated = false; yield return new WaitUntil(() => wordValidated); yield return WaitTapIfNeeded(s); break;
                case WaitType.WordRejected: wordRejected = false; yield return new WaitUntil(() => wordRejected); yield return WaitTapIfNeeded(s); break;
                case WaitType.ScoreDecreased: scoreDecreased = false; yield return new WaitUntil(() => scoreDecreased); yield return WaitTapIfNeeded(s); break;

                case WaitType.LetterDiscarded: letterDiscarded = false; yield return new WaitUntil(() => letterDiscarded); yield return WaitTapIfNeeded(s); break;
                case WaitType.SpecialLetterPlaced: specialPlaced = false; yield return new WaitUntil(() => specialPlaced); yield return WaitTapIfNeeded(s); break;

                case WaitType.CardRolled: cardRolled = false; yield return new WaitUntil(() => cardRolled); yield return WaitTapIfNeeded(s); break;
                case WaitType.CardDiscarded: cardDiscarded = false; yield return new WaitUntil(() => cardDiscarded); yield return WaitTapIfNeeded(s); break;
                case WaitType.CardCombined: cardCombined = false; yield return new WaitUntil(() => cardCombined); yield return WaitTapIfNeeded(s); break;

                case WaitType.LevelTimerStarted: levelTimerStarted = false; yield return new WaitUntil(() => levelTimerStarted); yield return WaitTapIfNeeded(s); break;
                case WaitType.LevelObjectiveMet: levelObjectiveMet = false; yield return new WaitUntil(() => levelObjectiveMet); yield return WaitTapIfNeeded(s); break;
            }
        }

        FinishNow();
    }

    // Public API to start tutorial at a specific step (useful for editor/tools)
    public void StartTutorialAt(int index)
    {
        if (started == false) started = true;
        ValidateBindings();
        if (steps == null || steps.Length == 0)
        {
            Debug.LogError("[Tutorial] No steps assigned.");
            return;
        }

        if (index < 0) index = 0;
        if (index >= steps.Length)
        {
            Debug.LogWarning($"[Tutorial] Requested start index {index} out of range.");
            FinishNow();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RunTutorialFrom(index));
    }

    // Advance to next step immediately (useful for editor/tools)
    public void NextStep()
    {
        int next = Mathf.Clamp(stepIndex + 1, 0, steps != null ? steps.Length : 0);
        if (steps == null || stepIndex >= steps.Length - 1)
        {
            FinishNow();
            return;
        }

        StopAllCoroutines();
        StartCoroutine(RunTutorialFrom(next));
    }

    IEnumerator WaitTapIfNeeded(Step s)
    {
        if (s.pressNextToContinue) { yield return WaitNextButton(); yield break; }

        // Prepare optional allowed click rects (highlight + extra)
        List<RectTransform> allowedRects = new List<RectTransform>();
        if (s.allowClickOnHighlight && s.highlightTarget) allowedRects.Add(s.highlightTarget);
        if (s.allowClickExtraRects != null && s.allowClickExtraRects.Length > 0) allowedRects.AddRange(s.allowClickExtraRects);

        // If tapping anywhere is not allowed and there are no allowed rects, then there's nothing to wait for here
        if (!s.tapAnywhereToContinue && allowedRects.Count == 0) yield break;

        bool tapped = false;
        UnityEngine.Events.UnityAction mark = () => tapped = true;

        // Attach continue-anywhere button if present and tap-anywhere is enabled
        if (s.tapAnywhereToContinue && continueAnywhereBtn && continueAnywhereBtn.gameObject.activeInHierarchy)
            continueAnywhereBtn.onClick.AddListener(mark);

        Canvas canvas = overlay ? overlay.GetComponentInParent<Canvas>() : null;
        Camera cam = (canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas?.worldCamera;

        while (!tapped)
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 sp = Input.mousePosition;

                // If there are allowed rects, only clicks inside them count
                if (allowedRects.Count > 0)
                {
                    bool insideAllowed = false;
                    foreach (var rt in allowedRects)
                    {
                        if (rt == null) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(rt, sp, cam)) { insideAllowed = true; break; }
                    }
                    if (insideAllowed) tapped = true;
                    // else ignore click
                }
                else
                {
                    // No allowed rects -> tap anywhere permitted
                    tapped = true;
                }
            }
            yield return null;
        }

        if (s.tapAnywhereToContinue && continueAnywhereBtn && continueAnywhereBtn.gameObject.activeInHierarchy)
            continueAnywhereBtn.onClick.RemoveListener(mark);
    }

    IEnumerator WaitNextButton()
    {
        if (nextButton == null)
        {
            while (!Input.GetMouseButtonDown(0)) yield return null;
            yield break;
        }

        bool clicked = false;
        void OnClick()
        {
            if (typing) { skipTypingThisStep = true; return; }
            clicked = true;
        }

        nextButton.gameObject.SetActive(true);
        nextButton.onClick.AddListener(OnClick);
        yield return new WaitUntil(() => clicked);
        nextButton.onClick.RemoveListener(OnClick);
        nextButton.gameObject.SetActive(false);
    }

    void SetupStepUI(Step s)
    {
        // 1) เปิดพื้นดำ + บล็อกอินพุตตามสเต็ป
        ShowOverlay(true, s.blockInput);

        // 2) ปุ่ม Tap-anywhere
        if (continueAnywhereBtn)
            continueAnywhereBtn.gameObject.SetActive(s.tapAnywhereToContinue);

        // 3) ตัวละคร/ชื่อ
        if (characterPanel) characterPanel.SetActive(true);
        if (portraitImage) portraitImage.gameObject.SetActive(s.useCharacter);
        if (nameText)
        {
            var nameRoot = nameText.transform.parent ? nameText.transform.parent.gameObject : null;
            if (nameRoot) nameRoot.SetActive(s.useCharacter);
            if (s.useCharacter) nameText.text = s.speakerName;
        }
        if (s.useCharacter && portraitImage && s.portrait) portraitImage.sprite = s.portrait;

        // 4) highlight & hand
        if (highlightFrame)
        {
            if (s.highlightTarget)
            {
                highlightFrame.gameObject.SetActive(true);
                highlightFrame.position = s.highlightTarget.position;
                highlightFrame.sizeDelta = s.highlightTarget.rect.size * 1.1f;
                // make highlight frame partially transparent
                var img = highlightFrame.GetComponent<UnityEngine.UI.Image>();
                if (img) { var col = img.color; col.a = Mathf.Clamp01(1f - highlightAlpha); img.color = col; }
            }
            else highlightFrame.gameObject.SetActive(false);
        }
        if (handPointer)
        {
            if (s.highlightTarget)
            {
                handPointer.gameObject.SetActive(true);
                handPointer.position = s.highlightTarget.position + (Vector3)s.handOffset;
            }
            else handPointer.gameObject.SetActive(false);
        }

        // 5) ข้อความ
        if (bodyText)
        {
            bodyText.text = s.message ?? "";
            bodyText.ForceMeshUpdate();
            bodyText.maxVisibleCharacters = s.useTypewriter ? 0 : bodyText.textInfo.characterCount;
        }

        // 6) “รูคลิกผ่าน” สำหรับ Input (logic)
        ApplyRaycastHoles(s);

        // 7) ทำ “รูโปร่งใส” บนพื้นดำ เพื่อให้ปุ่มสว่างชัดจริง
        BuildCutoutMask(s);

        // 8) สปอตไลต์ (สร้างทีหลังเพื่อให้อยู่บนสุด)
        BuildSpotlights(s);

        if (debugLogs)
            Debug.Log($"[Tutorial] Step {stepIndex}: block={s.blockInput} tapAny={s.tapAnywhereToContinue} holes={(rayFilter && rayFilter.holes != null ? rayFilter.holes.Length : 0)} overlayA={(overlayImage ? overlayImage.color.a : -1f)}");

        // analytics: step shown
        TutorialAnalytics.Log("TutStepShown", new System.Collections.Generic.Dictionary<string, object> {
            { "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name },
            { "stepIndex", stepIndex },
            { "stepId", s.id }
        });

        // start stuck timer
        if (stuckTimerCo != null) StopCoroutine(stuckTimerCo);
        stuckTimerCo = StartCoroutine(StuckTimerCo(s));
    }

    IEnumerator StuckTimerCo(Step s)
    {
        float t = 0f;
        while (t < stepStuckTimeout)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        // fire stuck event
        TutorialAnalytics.Log("TutStepStuck", new System.Collections.Generic.Dictionary<string, object> {
            { "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name },
            { "stepIndex", stepIndex },
            { "stepId", s.id }
        });

        // pulse highlight and hand if any
        if (highlightFrame) StartCoroutine(PulseHighlightOnce(highlightFrame));
        if (handPointer) StartCoroutine(PulseHandOnce(handPointer));
    }

    IEnumerator PulseHighlightOnce(RectTransform rt)
    {
        var img = rt.GetComponent<UnityEngine.UI.Image>();
        if (img == null) yield break;
        var orig = img.color;
        float duration = 0.8f; float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.PingPong(t * 2f, 1f);
            img.color = new Color(orig.r, orig.g, orig.b, Mathf.Lerp(orig.a, 1f, k));
            yield return null;
        }
        img.color = orig;
    }

    IEnumerator PulseHandOnce(RectTransform rt)
    {
        var baseP = rt.localScale;
        float duration = 0.8f; float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.08f;
            rt.localScale = baseP * k;
            yield return null;
        }
        rt.localScale = baseP;
    }

    void ApplyRaycastHoles(Step s)
    {
        if (!rayFilter) return;

        if (!s.blockInput)
        {
            rayFilter.holes = null;
            rayFilter.enabled = false;
            return;
        }

        List<RectTransform> list = new();
        if (s.allowClickOnHighlight && s.highlightTarget) list.Add(s.highlightTarget);
        if (s.allowClickExtraRects != null && s.allowClickExtraRects.Length > 0) list.AddRange(s.allowClickExtraRects);

        if (list.Count > 0)
        {
            rayFilter.holes = list.ToArray();
            rayFilter.padding = s.allowClickPadding;
            rayFilter.enabled = true;
        }
        else
        {
            rayFilter.holes = null; // บังทั้งจอ
            rayFilter.enabled = false;
        }
    }

    // ---------- Cutout (เจาะรูบนพื้นดำด้วย 4 บล็อก) ----------
    void BuildCutoutMask(Step s)
    {
        if (!overlayImage) return;

        bool hasAnyAllowed =
            (s.allowClickOnHighlight && s.highlightTarget) ||
            (s.allowClickExtraRects != null && s.allowClickExtraRects.Length > 0);

        bool needHole = s.blockInput && hasAnyAllowed;

        if (!needHole)
        {
            ClearDimBlocks();
            overlayImage.raycastTarget = true;
            var c = overlayImage.color; c.a = overlayTargetAlpha; overlayImage.color = c;
            return;
        }

        // --- คำนวณกรอบรู (รวมหลายเป้าหมายเป็นกรอบใหญ่เดียว) ---
        var canvas = overlay.GetComponentInParent<Canvas>();
        Camera cam = (canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas?.worldCamera;
        var overlayRT = overlayImage.rectTransform;

        // รวบรวมเป้าหมายทั้งหมด
        List<RectTransform> targets = new();
        if (s.allowClickOnHighlight && s.highlightTarget) targets.Add(s.highlightTarget);
        if (s.allowClickExtraRects != null && s.allowClickExtraRects.Length > 0) targets.AddRange(s.allowClickExtraRects);

        Rect union = GetUnionHoleRectInOverlaySpace(targets, overlayRT, cam, s.allowClickPadding + cutoutExtraPadding);

        // ปิด overlayImage แล้วสร้างบล็อกดำ 4 ชิ้นรอบรู
        overlayImage.raycastTarget = false;
        var col = overlayImage.color; col.a = 0f; overlayImage.color = col;

        BuildDimBlocks(overlayRT, union);

        // ให้สปอตไลต์/มือ อยู่บนสุด
        if (spotlightRoot) ((RectTransform)spotlightRoot).SetAsLastSibling();
    }

    Rect GetUnionHoleRectInOverlaySpace(List<RectTransform> targets, RectTransform overlayRT, Camera cam, float padding)
    {
        float xmin = float.PositiveInfinity, xmax = float.NegativeInfinity;
        float ymin = float.PositiveInfinity, ymax = float.NegativeInfinity;

        Vector3[] wc = new Vector3[4];
        for (int t = 0; t < targets.Count; t++)
        {
            var rt = targets[t];
            if (!rt) continue;

            rt.GetWorldCorners(wc);
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, wc[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRT, sp, cam, out Vector2 lp);
                xmin = Mathf.Min(xmin, lp.x);
                xmax = Mathf.Max(xmax, lp.x);
                ymin = Mathf.Min(ymin, lp.y);
                ymax = Mathf.Max(ymax, lp.y);
            }
        }

        if (float.IsInfinity(xmin)) return new Rect(0, 0, 0, 0); // safety

        xmin -= padding; xmax += padding;
        ymin -= padding; ymax += padding;
        return Rect.MinMaxRect(xmin, ymin, xmax, ymax);
    }

    void BuildDimBlocks(RectTransform overlayRT, Rect hole)
    {
        ClearDimBlocks();

        Rect full = overlayRT.rect;
        Color c = dimColor; c.a = overlayTargetAlpha; // ใช้ alpha เดียวกับ overlay

        // Top
        if (full.yMax > hole.yMax)
            CreateDim(overlayRT, new Rect(full.xMin, hole.yMax, full.width, full.yMax - hole.yMax), c);
        // Bottom
        if (hole.yMin > full.yMin)
            CreateDim(overlayRT, new Rect(full.xMin, full.yMin, full.width, hole.yMin - full.yMin), c);
        // Left
        if (hole.xMin > full.xMin)
            CreateDim(overlayRT, new Rect(full.xMin, hole.yMin, hole.xMin - full.xMin, hole.height), c);
        // Right
        if (full.xMax > hole.xMax)
            CreateDim(overlayRT, new Rect(hole.xMax, hole.yMin, full.xMax - hole.xMax, hole.height), c);
    }

    Image CreateDim(RectTransform parent, Rect r, Color color)
    {
        var go = new GameObject("DimBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true; // ต้องบังคลิก
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(r.width, r.height);
        rt.localPosition = new Vector3(r.center.x, r.center.y, 0f);
        dimBlocks.Add(img);
        return img;
    }

    void ClearDimBlocks()
    {
        for (int i = 0; i < dimBlocks.Count; i++)
            if (dimBlocks[i]) Destroy(dimBlocks[i].gameObject);
        dimBlocks.Clear();
    }

    // ---------- Spotlight ----------
    void BuildSpotlights(Step s)
    {
        ClearSpotlights();

        if (!s.showSpotlightOnAllowed) return;
        if (!overlay || spotlightRoot == null || spotlightSprite == null) return;

        bool anyAllowed = (s.allowClickOnHighlight && s.highlightTarget) ||
                          (s.allowClickExtraRects != null && s.allowClickExtraRects.Length > 0);
        if (s.blockInput && !anyAllowed) return;

        List<RectTransform> list = new();
        if (s.allowClickOnHighlight && s.highlightTarget) list.Add(s.highlightTarget);
        if (s.allowClickExtraRects != null) list.AddRange(s.allowClickExtraRects);

        Canvas canvas = overlay.GetComponentInParent<Canvas>();
        Camera cam = (canvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas?.worldCamera;
        RectTransform parentRT = (RectTransform)spotlightRoot;

        foreach (var rt in list)
        {
            var go = new GameObject("Spotlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(spotlightRoot, false);
            var img = go.GetComponent<Image>();
            img.sprite = spotlightSprite;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, spotlightAlpha);

            var srt = (RectTransform)go.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);

            // Compute world bounds accurately including anchors and pivot
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            Vector2 worldSize = max - min;

            Vector2 size = worldSize * spotlightSoftScale + Vector2.one * spotlightPadding * 2f;
            // Convert screen size to local canvas space size
            Vector2 pivotScreen = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, pivotScreen, cam, out Vector2 localPivot);

            srt.sizeDelta = size;
            srt.localPosition = localPivot;

            if (spotlightUsePulse) StartCoroutine(PulseSpotlight(srt));

            activeSpotlights.Add(img);
        }

        // ให้อยู่บนสุดของ Overlay
        ((RectTransform)spotlightRoot).SetAsLastSibling();
    }

    IEnumerator PulseSpotlight(RectTransform rt)
    {
        float t = 0f;
        Vector3 baseScale = rt.localScale;
        float minS = 1f, maxS = spotlightPulseScale;

        while (rt && rt.gameObject.activeInHierarchy)
        {
            t += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * spotlightPulseSpeed;
            float k = (Mathf.Sin(t) + 1f) * 0.5f;
            float s = Mathf.Lerp(minS, maxS, k);
            rt.localScale = baseScale * s;
            yield return null;
        }
    }

    void ClearSpotlights()
    {
        for (int i = 0; i < activeSpotlights.Count; i++)
            if (activeSpotlights[i]) Destroy(activeSpotlights[i].gameObject);
        activeSpotlights.Clear();
    }

    // ---------- Typewriter ----------
    IEnumerator PlayStepIntro(Step s)
    {
        if (s.useTypewriter && bodyText)
        {
            if (typingCo != null) StopCoroutine(typingCo);
            typingCo = StartCoroutine(TypeText(s.message, s.typeSpeed, s.voiceBeep, s.beepInterval));
            yield return typingCo;
        }
        else yield break;
    }

    IEnumerator TypeText(string text, float cps, AudioClip beep, float beepGap)
    {
        typing = true;
        skipTypingThisStep = false;

        if (!bodyText) { typing = false; yield break; }

        bodyText.text = text ?? "";
        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();
        if (bodyText.textInfo.characterCount == 0) { yield return null; bodyText.ForceMeshUpdate(); }

        int total = bodyText.textInfo.characterCount;
        if (total == 0) { typing = false; yield break; }

        float tPerChar = 1f / Mathf.Max(1f, cps);
        float timer = 0f, beepTimer = 0f, dt;

        while (typing && bodyText.maxVisibleCharacters < total)
        {
            if (Input.GetMouseButtonDown(0)) skipTypingThisStep = true;

            if (skipTypingThisStep)
            {
                bodyText.maxVisibleCharacters = total;
                break;
            }

            dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            timer += dt; beepTimer += dt;

            if (timer >= tPerChar)
            {
                timer -= tPerChar;
                bodyText.maxVisibleCharacters = Mathf.Min(bodyText.maxVisibleCharacters + 1, total);
                if (beep && voiceSource && beepTimer >= beepGap) { voiceSource.PlayOneShot(beep); beepTimer = 0f; }
            }
            yield return null;
        }
        typing = false;
    }

    // ---------- Overlay fade (ใช้ overlayImage.color.a) ----------
    void ShowOverlay(bool show, bool blockInput)
    {
        if (!overlay || !overlayImage) return;

        overlay.blocksRaycasts = show && blockInput;
        overlay.alpha = 1f;

        if (overlayFadeCo != null) StopCoroutine(overlayFadeCo);
        float targetA = show ? overlayTargetAlpha : 0f;
        overlayFadeCo = StartCoroutine(FadeOverlayImage(targetA));
    }

    IEnumerator FadeOverlayImage(float targetA)
    {
        Color c = overlayImage.color;
        float startA = c.a;
        float t = 0f, dur = Mathf.Max(0.01f, overlayFadeDuration);
        c.r = 0f; c.g = 0f; c.b = 0f;

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            c.a = Mathf.Lerp(startA, targetA, t / dur);
            overlayImage.color = c;
            yield return null;
        }
        c.a = targetA;
        overlayImage.color = c;
    }

    int GetTotalScore_Safe()
    {
        try { if (TurnManager.Instance != null) return TurnManager.Instance.TotalScore; }
        catch { }
        return cachedTotalScore;
    }

    void ValidateBindings()
    {
        if (!overlay) overlay = transform.Find("Overlay")?.GetComponent<CanvasGroup>();
        if (!overlayImage && overlay) overlayImage = overlay.GetComponent<Image>();
        if (!rayFilter && overlay) rayFilter = overlay.GetComponent<OverlayHoleRaycastFilter>();

        // Try to find DialogPanel and its children anywhere under this object (support nested layouts)
        if (!characterPanel) characterPanel = FindChildByName(transform, "DialogPanel");
        if (!bodyText)
        {
            var bt = FindComponentInChildrenByName<TextMeshProUGUI>(transform, "BodyText");
            if (bt != null) bodyText = bt;
        }
        if (!portraitImage)
        {
            var pi = FindComponentInChildrenByName<Image>(transform, "Portrait");
            if (pi != null) portraitImage = pi;
        }
        if (!nameText)
        {
            var nt = FindComponentInChildrenByName<TextMeshProUGUI>(transform, "Label"); // NameTag/Label
            if (nt != null) nameText = nt;
        }
        if (!voiceSource)
        {
            var vs = FindComponentInChildrenByName<AudioSource>(transform, "DialogPanel");
            if (vs != null) voiceSource = vs;
        }
        if (!nextButton)
        {
            var nb = FindComponentInChildrenByName<Button>(transform, "NextButton");
            if (nb != null) nextButton = nb;
        }
        if (!highlightFrame) highlightFrame = transform.Find("HighlightFrame")?.GetComponent<RectTransform>();
        if (!handPointer) handPointer = transform.Find("HandPointer")?.GetComponent<RectTransform>();
        if (!continueAnywhereBtn) continueAnywhereBtn = transform.Find("Overlay/ContinueAnywhereBtn")?.GetComponent<Button>();
        if (!spotlightRoot) spotlightRoot = transform.Find("Overlay/Spotlights");

        // Overlay เต็มจอ + ภาพดำ + รับ Raycast + อยู่หน้าสุด
        if (overlay)
        {
            var rt = overlay.GetComponent<RectTransform>();
            if (rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

            if (!overlayImage) overlayImage = overlay.GetComponent<Image>();
            if (!overlayImage) overlayImage = overlay.gameObject.AddComponent<Image>();

            if (overlayImage.sprite == null)
            {
                var tex = Texture2D.whiteTexture;
                overlayImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            overlayImage.material = null; // ไม่ใช้ Standard/Default-UI-Material
            var c = overlayImage.color; c.r = 0; c.g = 0; c.b = 0; c.a = 0;
            overlayImage.color = c;
            overlayImage.raycastTarget = true;

            overlay.alpha = 1f;
            overlay.blocksRaycasts = false;
            overlay.transform.SetAsLastSibling();
        }

        // ปุ่มแตะทั้งจอ
        if (continueAnywhereBtn)
        {
            var brt = continueAnywhereBtn.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;

            var bimg = continueAnywhereBtn.GetComponent<Image>();
            if (!bimg) bimg = continueAnywhereBtn.gameObject.AddComponent<Image>();
            bimg.color = new Color(1, 1, 1, 0f);
            bimg.raycastTarget = true;
            continueAnywhereBtn.gameObject.SetActive(false);
        }

        // ถ้าไม่มี root ของสปอตไลต์ สร้างเป็นลูกของ Overlay
        if (!spotlightRoot && overlay)
        {
            var go = new GameObject("Spotlights", typeof(RectTransform));
            go.transform.SetParent(overlay.transform, false);
            var srt = (RectTransform)go.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
            spotlightRoot = go.transform;
        }

        if (debugLogs)
            Debug.Log($"[Tutorial] ValidateBindings() overlay={(overlay != null)} img={(overlayImage != null)} rayFilter={(rayFilter != null)} bodyText={(bodyText != null)}");

        // Skip button binding (ถ้ามี)
        if (skipTutorialButton)
        {
            skipTutorialButton.onClick.RemoveListener(SkipTutorialNow);
            skipTutorialButton.onClick.AddListener(SkipTutorialNow);
            skipTutorialButton.gameObject.SetActive(true);
        }
    }

    // Recursive helpers to find children/components by name
    GameObject FindChildByName(Transform root, string name)
    {
        foreach (Transform t in root)
        {
            if (t.name == name) return t.gameObject;
            var found = FindChildByName(t, name);
            if (found != null) return found;
        }
        return null;
    }

    T FindComponentInChildrenByName<T>(Transform root, string name) where T : Component
    {
        foreach (Transform t in root)
        {
            if (t.name == name)
            {
                var comp = t.GetComponent<T>();
                if (comp) return comp;
            }
            var child = FindComponentInChildrenByName<T>(t, name);
            if (child) return child;
        }
        return null;
    }

    // --------- Notifiers ----------
    public static void NotifyDictionaryOpened() { if (Instance) Instance.dictionaryOpenedFlag = true; }
    public static void NotifyShopPurchased() { if (Instance) Instance.shopPurchasedFlag = true; }
    public static void NotifyCardUsed() { if (Instance) Instance.cardUsedFlag = true; }
    public static void NotifyWordValidated() { if (Instance) Instance.wordValidated = true; }
    public static void NotifyWordRejected() { if (Instance) Instance.wordRejected = true; }
    public static void NotifyScoreDecreased() { if (Instance) Instance.scoreDecreased = true; }
    public static void NotifyLetterDiscarded() { if (Instance) Instance.letterDiscarded = true; }
    public static void NotifySpecialLetterPlaced() { if (Instance) Instance.specialPlaced = true; }
    public static void NotifyCardRolled() { if (Instance) Instance.cardRolled = true; }
    public static void NotifyCardDiscarded() { if (Instance) Instance.cardDiscarded = true; }
    public static void NotifyCardCombined() { if (Instance) Instance.cardCombined = true; }
    public static void NotifyLevelTimerStarted() { if (Instance) Instance.levelTimerStarted = true; }
    public static void NotifyLevelObjectiveMet() { if (Instance) Instance.levelObjectiveMet = true; }
    public static void NotifyTilePlacedOnBoard() { if (Instance) Instance.tilePlacedFlag = true; }
}
