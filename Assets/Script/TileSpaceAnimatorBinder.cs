using UnityEngine;

[DisallowMultipleComponent]
public class TileAnimatorBinder : MonoBehaviour
{
    public string inSpaceBool = "InSpace";
    public string waveStateName = "SpaceWave";
    [Range(0f, 1f)] public float phaseStep = 0.08f; // เหลื่อมเฟสต่อ 1 ช่อง (~28-30°)

    Animator anim;
    bool lastInSpace;

    void Awake()
    {
        // หา Animator บนตัวไทล์ (หรือบนลูก ถ้ามี)
        anim = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        if (anim) anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    void LateUpdate()
    {
        if (!anim) return;

        bool inSpace = IsUnderSpaceSlot();
        anim.SetBool(inSpaceBool, inSpace);

        // เพิ่งเข้า Space → เล่น SpaceWave ด้วย cycle offset ตาม index ช่อง
        if (inSpace && !lastInSpace)
        {
            int idx = GetSpaceIndex();
            float offset = Mathf.Repeat(idx * phaseStep, 1f);
            if (!string.IsNullOrEmpty(waveStateName))
                anim.Play(waveStateName, 0, offset); // ชื่อ state ต้องตรงเป๊ะ
        }

        lastInSpace = inSpace;
    }

    bool IsUnderSpaceSlot()
    {
        var p = transform.parent;
        return p && (p.GetComponent<SpaceSlot>() != null);
    }

    int GetSpaceIndex()
    {
        var p = transform.parent as RectTransform;
        if (!p) return 0;
        var sm = SpaceManager.Instance;
        if (sm == null) return 0;

        // ถ้า SpaceManager มีเมธอด IndexOfSlot(RectTransform) ให้ใช้
        // มิฉะนั้นจะวนหาจากลิสต์ public ของมัน
        try { return sm.IndexOfSlot(p); }
        catch { /* เผื่อไม่มีเมธอดนั้น */ }

        if (sm.slotTransforms != null)
        {
            for (int i = 0; i < sm.slotTransforms.Count; i++)
                if (sm.slotTransforms[i] == p) return i;
        }
        return 0;
    }

    public static void Trigger(Animator a, string trig)
    {
        if (!a || string.IsNullOrEmpty(trig)) return;
        a.ResetTrigger(trig);
        a.SetTrigger(trig);
    }
}
