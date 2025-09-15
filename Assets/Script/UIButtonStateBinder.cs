using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIButtonStateBinder : MonoBehaviour
{
    public enum Role { Confirm, Clear, CancelAlwaysDisabled }

    [Header("Assign")]
    public Role role = Role.Confirm;
    public Button button;                 // ปล่อยว่างได้ จะหาอัตโนมัติ
    public Animator animatorOnThis;       // ถ้ามี จะส่งพารามิเตอร์ "Interactable"
    public CanvasGroup fadeGroup;         // ถ้ามี จะปรับ alpha ตอน disable

    [Header("Visual (เผื่อไม่มี Animator)")]
    [Range(0f,1f)] public float disabledAlpha = 0.5f;

    void Awake()
    {
        if (!button) button = GetComponent<Button>();
        if (!animatorOnThis) animatorOnThis = GetComponent<Animator>();
        if (!fadeGroup) fadeGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable() { ApplyStateImmediate(); }
    void Update()   { ApplyStateImmediate(); }

    void ApplyStateImmediate()
    {
        bool can = role switch
        {
            Role.Confirm => BoardHasAnyTile(),
            Role.Clear   => SpaceHasAnyTile(),
            Role.CancelAlwaysDisabled => false,
            _ => false
        };

        if (button) button.interactable = can;

        // ส่งให้ Animator คุมภาพ (ไป state Disabled/Idle ตามพารามิเตอร์นี้)
        if (animatorOnThis) animatorOnThis.SetBool("Interactable", can);

        // เผื่อยังไม่ได้ทำ state Disabled ใน Animator: จางด้วย CanvasGroup
        if (fadeGroup) fadeGroup.alpha = can ? 1f : disabledAlpha;
    }

    bool SpaceHasAnyTile()
    {
        var sm = SpaceManager.Instance;
        return sm != null && sm.GetPreparedTiles().Count > 0;
    }

    bool BoardHasAnyTile()
    {
        var bm = BoardManager.Instance;
        if (bm == null || bm.grid == null) return false;

        int R = bm.grid.GetLength(0), C = bm.grid.GetLength(1);
        for (int r = 0; r < R; r++)
            for (int c = 0; c < C; c++)
                if (bm.grid[r, c] != null && bm.grid[r, c].HasLetterTile())
                    return true;
        return false;
    }
}
