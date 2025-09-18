using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lightweight controller for running the game in "Tutorial Mode" (isolated playtest environment).
/// When enabled, it forces a specific tutorial asset to run and can lock progression until tutorial complete.
/// </summary>
public class TutorialGameMode : MonoBehaviour
{
    public TutorialDialogAsset tutorialAsset;
    public bool forceTutorialOnStart = true;
    public bool lockProgressUntilComplete = true;

    [Header("Auto-disable GameObjects (names/tags)")]
    public string[] disableByName;
    public string[] disableByTag;

    [Header("Fixture (optional)")]
    public int initialCoins = 100;
    public int initialCardsInHand = 3;

    [Header("Inspector runtime controls")]
    public bool showInspectorControls = true;

    // Module toggles for composing a quick-play tutorial (if tutorialAsset not set)
    [Header("Quick Modules (create runtime asset if no asset provided)")]
    public bool moduleDragTile = true;
    public bool moduleConfirm = true;
    public bool moduleScore = true;
    public bool moduleDictionary = true;
    public bool moduleUseCard = true;
    public bool moduleReroll = true;
    public bool moduleLevelGoal = true;

    TutorialManager tm;
    GameObject runtimeBinderGO;
    System.Collections.Generic.List<GameObject> disabledObjects = new System.Collections.Generic.List<GameObject>();

    void Awake()
    {
        tm = FindObjectOfType<TutorialManager>();
        if (!tm) Debug.LogWarning("TutorialGameMode: no TutorialManager found in scene.");
    }

    void Start()
    {
        if (forceTutorialOnStart && tm != null)
        {
            StartRuntimeTutorial();
        }
    }

    // Start runtime tutorial and setup runtime binder
    public void StartRuntimeTutorial()
    {
        if (tm == null) tm = FindObjectOfType<TutorialManager>();
        if (tm == null) { Debug.LogWarning("TutorialGameMode: no TutorialManager found."); return; }
        tm.startOnAwake = false;
        if (runtimeBinderGO != null) DestroyImmediate(runtimeBinderGO);
        runtimeBinderGO = new GameObject("GameplayTutorialBinder_RunTime");
        var binder = runtimeBinderGO.AddComponent<GameplayTutorialBinder>();
        binder.tm = tm;
        var runtimeAsset = (tutorialAsset == null) ? CreateRuntimeAssetFromToggles() : tutorialAsset;
        binder.dialogAsset = runtimeAsset;
        if (lockProgressUntilComplete) TutorialManager.OnTutorialFinished += OnTutorialFinished;

        ApplyDisableList();
        ApplyFixture();

        // Convert dialogAsset into TutorialManager.Step[] and assign BEFORE starting tutorial to avoid race
        try
        {
            if (runtimeAsset != null && runtimeAsset.steps != null && runtimeAsset.steps.Length > 0)
            {
                var list = new System.Collections.Generic.List<TutorialManager.Step>();
                foreach (var sd in runtimeAsset.steps)
                {
                    var s = new TutorialManager.Step();
                    s.id = sd.id; s.message = sd.message; s.useCharacter = sd.useCharacter; s.speakerName = sd.speakerName; s.portrait = sd.portrait;
                    s.useTypewriter = sd.useTypewriter; s.typeSpeed = sd.typeSpeed; s.voiceBeep = sd.voiceBeep; s.beepInterval = sd.beepInterval;
                    s.waitFor = sd.waitFor; s.waitTime = sd.waitTime; s.blockInput = sd.blockInput; s.tapAnywhereToContinue = sd.tapAnywhereToContinue;
                    s.pressNextToContinue = sd.pressNextToContinue; s.handOffset = sd.handOffset; s.allowClickOnHighlight = sd.allowClickOnHighlight;
                    s.allowClickPadding = sd.allowClickPadding; s.showSpotlightOnAllowed = sd.showSpotlightOnAllowed;

                    if (!string.IsNullOrEmpty(sd.highlightTargetName))
                    {
                        var go = GameObject.Find(sd.highlightTargetName);
                        if (go) s.highlightTarget = go.GetComponent<RectTransform>();
                    }

                    if (sd.allowClickExtraNames != null && sd.allowClickExtraNames.Length > 0)
                    {
                        var rtlist = new System.Collections.Generic.List<RectTransform>();
                        foreach (var n in sd.allowClickExtraNames)
                        {
                            var go = GameObject.Find(n);
                            if (go) rtlist.Add(go.GetComponent<RectTransform>());
                        }
                        if (rtlist.Count > 0) s.allowClickExtraRects = rtlist.ToArray();
                    }

                    list.Add(s);
                }
                tm.steps = list.ToArray();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[TutorialGameMode] Failed to convert runtime asset to steps: " + ex.Message);
        }

        tm.StartTutorialNow();
    }

    public void StopRuntimeTutorial()
    {
        if (tm != null) tm.FinishNow();
        if (runtimeBinderGO != null) { DestroyImmediate(runtimeBinderGO); runtimeBinderGO = null; }
        TutorialManager.OnTutorialFinished -= OnTutorialFinished;
        RestoreDisabledObjects();
    }

    public void ApplyFixturePublic() { ApplyFixture(); }
    public void RestoreDisabledPublic() { RestoreDisabledObjects(); }
    public TutorialDialogAsset GenerateRuntimeAsset() { return CreateRuntimeAssetFromToggles(); }

    public void JumpToStep(int index)
    {
        if (tm == null) tm = FindObjectOfType<TutorialManager>();
        if (tm == null) return;
        tm.StartTutorialAt(index);
    }

    void OnDestroy()
    {
        TutorialManager.OnTutorialFinished -= OnTutorialFinished;
    }

    void OnTutorialFinished()
    {
        if (lockProgressUntilComplete)
        {
            // placeholder: unlock game progression (project-specific)
            Debug.Log("[TutorialGameMode] Tutorial finished - unlocking progression.");
            RestoreDisabledObjects();
            // Mark FTU as done so first-time flow won't replay
            try { PlayerPrefs.SetInt("FTU_DONE", 1); PlayerPrefs.Save(); } catch { }

            // Register FTU key in TutorialManager if available
            try { if (TutorialManager.Instance != null) TutorialManager.Instance.RegisterPrefsKey("FTU_DONE"); } catch { }

            // Try to enable common main-menu buttons if present
            string[] candidates = new string[] { "PlayButton", "ContinueButton", "StartButton", "LevelSelect", "MainPlayButton" };
            foreach (var name in candidates)
            {
                var go = GameObject.Find(name);
                if (go)
                {
                    go.SetActive(true);
                    var btn = go.GetComponent<UnityEngine.UI.Button>();
                    if (btn) btn.interactable = true;
                }
            }

            // Try to call LevelManager.SetupLevel(0) if we are in gameplay scene and LevelManager exists
            try
            {
                var lm = FindObjectOfType<LevelManager>();
                if (lm != null) {
                    var mi = lm.GetType().GetMethod("SetupLevel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi != null) mi.Invoke(lm, new object[] { 0 });
                }
            }
            catch { }
        }
    }

    // Disable GameObjects by name or tag to reduce distraction
    void ApplyDisableList()
    {
        foreach (var n in disableByName)
        {
            if (string.IsNullOrEmpty(n)) continue;
            var go = GameObject.Find(n);
            if (go) go.SetActive(false);
        }
        foreach (var t in disableByTag)
        {
            if (string.IsNullOrEmpty(t)) continue;
            var gos = GameObject.FindGameObjectsWithTag(t);
            foreach (var g in gos) g.SetActive(false);
        }
    }

    void RestoreDisabledObjects()
    {
        foreach (var n in disableByName)
        {
            if (string.IsNullOrEmpty(n)) continue;
            var go = GameObject.Find(n);
            if (go) go.SetActive(true);
        }
        foreach (var t in disableByTag)
        {
            if (string.IsNullOrEmpty(t)) continue;
            var gos = GameObject.FindGameObjectsWithTag(t);
            foreach (var g in gos) g.SetActive(true);
        }
    }

    void ApplyFixture()
    {
        // Try to configure common managers if present so tutorial scene behaves predictably
        try
        {
            // CurrencyManager: set coins to initialCoins (prefer reflection, fallback to Add)
            var cur = FindObjectOfType<CurrencyManager>();
            if (cur != null)
            {
                try
                {
                    var fi = cur.GetType().GetField("coins", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (fi != null) fi.SetValue(cur, initialCoins);
                    else
                    {
                        int c = cur.Coins;
                        if (initialCoins > c) cur.Add(initialCoins - c);
                        else if (initialCoins < c)
                        {
                            // try to set by reflection even if property exists but no field
                            var pi = cur.GetType().GetProperty("Coins");
                            if (pi == null) { /* can't lower safely */ }
                        }
                    }
                }
                catch { }
            }

            // BoardManager: regenerate board if available
            var bm = FindObjectOfType<BoardManager>();
            if (bm != null)
            {
                var mi = bm.GetType().GetMethod("GenerateBoard");
                if (mi != null) mi.Invoke(bm, null);
            }

            // BenchManager: ensure bench has initialCardsInHand tiles
            var bench = FindObjectOfType<BenchManager>();
            if (bench != null)
            {
                // count current tiles
                int count = 0;
                foreach (var t in bench.slotTransforms) if (t.childCount > 0) count++;
                while (count < initialCardsInHand)
                {
                    bench.RefillOneSlot();
                    count++;
                }
            }

            // TileBag: try to refill
            var tb = FindObjectOfType<TileBag>();
            if (tb != null)
            {
                var mi = tb.GetType().GetMethod("RefillTileBag");
                if (mi != null) mi.Invoke(tb, null);
            }

            // TurnManager: reset turn/score state
            var tmgr = FindObjectOfType<TurnManager>();
            if (tmgr != null)
            {
                var mi = tmgr.GetType().GetMethod("ResetForNewLevel");
                if (mi != null) mi.Invoke(tmgr, null);
                // ensure confirm button state updated
                var ubi = tmgr.GetType().GetMethod("EnableConfirm", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (ubi != null) ubi.Invoke(tmgr, null);
            }
        }
        catch { }
    }

    // Build a temporary TutorialDialogAsset at runtime based on toggles
    TutorialDialogAsset CreateRuntimeAssetFromToggles()
    {
        var asset = ScriptableObject.CreateInstance<TutorialDialogAsset>();
        var list = new System.Collections.Generic.List<TutorialDialogAsset.StepData>();

        void Add(string id, string msg, string target, TutorialManager.WaitType wait, bool allowClick=false)
        {
            var s = new TutorialDialogAsset.StepData();
            s.id = id; s.message = msg; s.useCharacter = true; s.speakerName = "โค้ช";
            s.highlightTargetName = target; s.blockInput = true; s.tapAnywhereToContinue = !allowClick; s.allowClickOnHighlight = allowClick; s.waitFor = wait;
            list.Add(s);
        }

        if (moduleDragTile) Add("GT_DRAG_FIRST_TILE","ลากตัวอักษรตัวแรกมาวางที่บอร์ด","TileSlot1", TutorialManager.WaitType.TilePlacedOnBoard, true);
        if (moduleConfirm) Add("GT_PRESS_CONFIRM","กดปุ่มยืนยันเมื่อพร้อม","ConfirmButton", TutorialManager.WaitType.ConfirmPressed, true);
        if (moduleScore) Add("GT_SCORE_INCREASE","คะแนนของคุณเพิ่มขึ้นแล้ว", null, TutorialManager.WaitType.ScoreIncreased, false);
        if (moduleDictionary) Add("GT_OPEN_DICT","เปิดพจนานุกรมเพื่อตรวจคำ","DictionaryButton", TutorialManager.WaitType.DictionaryOpened, true);
        if (moduleUseCard) Add("GT_USE_CARD","ลองใช้การ์ดพิเศษนี้","CardSlot1", TutorialManager.WaitType.CardUsed, true);
        if (moduleReroll) Add("GT_REROLL","กด REROLL เพื่อสุ่มการ์ดใหม่","RerollButton", TutorialManager.WaitType.CardRolled, true);
        if (moduleLevelGoal) Add("GT_LEVEL_GOAL","เป้าหมายของด่านนี้คือ: ทำคะแนนให้ครบ", null, TutorialManager.WaitType.None, false);

        asset.steps = list.ToArray();
        return asset;
    }
}



