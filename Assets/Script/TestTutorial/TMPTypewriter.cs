using System;            // <<< สำคัญ: เพื่อใช้ event Action
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class TMPTypewriter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] TMP_Text tmp;
    [SerializeField] Animator animator;

    [Header("Animator Setup")]
    [SerializeField] string progressParam = "TypeProgress"; // ต้องตรงกับใน Animator Controller
    [SerializeField] string stateName = "TW_Progress01";    // ต้องตรงกับชื่อ State ใน Animator
    [SerializeField] float clipLengthSeconds = 1f;

    int totalChars = 0;
    bool playing = false;

    // === Fallback ด้วยตัวจับเวลา (กรณี Animator ไม่เดิน) ===
    float duration = 0.6f;
    float elapsed = 0f;
    bool animatorHealthy = false; // true เมื่อเห็นค่า progress เพิ่มจริง

    public bool IsPlaying => playing;
    public event Action OnCompleted;   // แจ้ง UI/Manager ได้

    void Reset()
    {
        tmp = GetComponent<TMP_Text>();
        animator = GetComponent<Animator>();
    }

    void Awake()
    {
        if (!tmp) tmp = GetComponent<TMP_Text>();
        if (!animator) animator = GetComponent<Animator>();
        if (tmp) tmp.maxVisibleCharacters = 0;
    }

    public void Play(string text, float seconds)
    {
        if (!tmp || !animator) return;

        // 1) ตั้งข้อความ
        tmp.text = text ?? "";
        tmp.ForceMeshUpdate();
        totalChars = Mathf.Max(0, tmp.textInfo.characterCount);
        SetVisibleChars(0);

        // 2) รีเซ็ต Animator ทุกครั้ง
        animator.SetFloat(progressParam, 0f);
        float safeSec  = Mathf.Max(0.01f, seconds);
        float safeClip = Mathf.Max(0.01f, clipLengthSeconds);
        animator.speed = safeClip / safeSec;      // ให้จบตาม seconds
        animator.Play(stateName, 0, 0f);
        animator.Update(0f);                      // ประเมินเฟรมแรกทันที

        // 3) ตั้ง fallback timer
        duration = safeSec;
        elapsed = 0f;
        animatorHealthy = false;

        playing = true;
    }

    public void RevealAllNow()
    {
        if (!tmp) return;
        SetVisibleChars(int.MaxValue);
        playing = false;
        if (animator) animator.speed = 0f;
        OnCompleted?.Invoke(); // บอกว่าจบแล้ว
    }

    void Update()
    {
        if (!playing || !tmp) return;

        float a = animator ? animator.GetFloat(progressParam) : 0f;
        if (a > 0.0001f) animatorHealthy = true;

        float p = animatorHealthy
            ? Mathf.Clamp01(a)
            : Mathf.Clamp01((elapsed += Time.unscaledDeltaTime) / Mathf.Max(0.0001f, duration));

        SetVisibleChars(Mathf.CeilToInt(totalChars * p));

        if (p >= 0.999f)
        {
            SetVisibleChars(int.MaxValue);
            playing = false;
            OnCompleted?.Invoke();
        }
    }

    void SetVisibleChars(int count)
    {
        if (tmp) tmp.maxVisibleCharacters = Mathf.Max(0, count);
    }
}
