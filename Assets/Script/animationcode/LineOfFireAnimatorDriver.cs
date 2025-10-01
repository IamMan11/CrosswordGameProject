using System;
using UnityEngine;

[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public class LineOfFireAnimatorDriver : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;

    [Header("State names (ต้องตรงกับชื่อ state ใน Animator)")]
    public string idleState = "Idel";   // จากรูปคุณสะกด Idel
    public string fire1State = "fire1";
    public string fire2State = "fire2";
    public string fire3State = "fire3";

    [Header("Stack thresholds (>= ค่านี้จะเล่น state นั้น)")]
    public int stackForFire1 = 1;
    public int stackForFire2 = 3;
    public int stackForFire3 = 5;

    [Header("Playback")]
    public float crossFade = 0.05f;     // เวลาเฟดเปลี่ยน state
    public bool debugLog = false;

    int _currentTier = -1;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        // ให้เล่นได้แม้ timeScale = 0 (ตอนคิดคะแนน)
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    int TierForStack(int stack)
    {
        int tier = 0;
        if (stack >= stackForFire1) tier = 1;
        if (stack >= stackForFire2) tier = 2;
        if (stack >= stackForFire3) tier = 3;
        return tier;
    }

    string StateNameForTier(int tier)
    {
        switch (tier)
        {
            case 1: return fire1State;
            case 2: return fire2State;
            case 3: return fire3State;
            default: return idleState;
        }
    }

    // เรียกทุกครั้งที่ค่า "stack ต่อเทิร์น" เปลี่ยน
    public void OnStackChanged(int newStack)
    {
        int newTier = TierForStack(newStack);
        if (newTier == _currentTier) return; // ยังอยู่ tier เดิม ไม่ต้องเปลี่ยน
        _currentTier = newTier;

        string state = StateNameForTier(newTier);
        if (debugLog) Debug.Log($"[FireFX] stack={newStack} -> tier={newTier} -> {state}");

        // ข้าม/เฟดไป state เป้าหมาย (คลิปต้อง Loop Time = true)
        animator.CrossFadeInFixedTime(state, crossFade, 0, 0f, 1f);
        animator.Update(0f); // รีเฟรชทันที 1 เฟรม
    }

    // เรียกเมื่อ "สตรีคแตก" หรืออยากหยุด
    public void ResetFx()
    {
        _currentTier = 0;
        animator.Play(idleState, 0, 0f);
        animator.Update(0f);
    }
}
