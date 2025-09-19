using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class PingPongVideoBackground : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer player;

    [Header("Behavior")]
    [Tooltip("เมื่อวิดีโอถึงท้ายคลิป ให้รอคลิกเพื่อเริ่มย้อนกลับ")]
    public bool requireClickAtEnd = true;

    [Tooltip("พยายามใช้การย้อนแบบ native (playbackSpeed = -1) ก่อน ถ้าใช้ไม่ได้จะ fallback เป็นการไล่เฟรม")]
    public bool preferNativeReverse = true;

    [Tooltip("FPS เป้าหมายเวลาไล่เฟรมย้อน (fallback)")]
    [Range(5, 60)] public int seekFps = 30;

    [Tooltip("คลิกเมาส์ซ้าย/แตะหน้าจอเพื่อเริ่มย้อนเมื่อถึงท้ายคลิป")]
    public KeyCode triggerKey = KeyCode.Mouse0;

    bool waitingForClick;

    void Reset()
    {
        player = GetComponent<VideoPlayer>();
    }

    IEnumerator Start()
    {
        if (!player) player = GetComponent<VideoPlayer>();
        player.isLooping = false;                     // เราคุมลูปเอง
        player.skipOnDrop = true;                     // ช่วยความลื่น
        player.waitForFirstFrame = true;
        player.loopPointReached += OnVideoEnded;

        // เตรียมคลิปก่อนเล่น
        if (!player.isPrepared)
        {
            player.Prepare();
            while (!player.isPrepared) yield return null;
        }

        PlayForwardFromStart();
    }

    void OnDestroy()
    {
        if (player != null) player.loopPointReached -= OnVideoEnded;
    }

    void OnVideoEnded(VideoPlayer vp)
    {
        // ถึงท้ายคลิปแล้ว หยุดไว้เพื่อรอคลิก (ตามสเปคที่ขอ)
        if (requireClickAtEnd)
        {
            waitingForClick = true;
            vp.Pause();
        }
        else
        {
            // ถ้าไม่ต้องการคลิก ก็ย้อนทันที
            StartCoroutine(PlayBackwardThenForward());
        }
    }

    void Update()
    {
        if (!waitingForClick) return;

        // คลิกเมาส์ซ้าย หรือสัมผัสจอ (เริ่มย้อน)
        bool clicked = Input.GetKeyDown(triggerKey) || Input.GetMouseButtonDown(0)
#if UNITY_IOS || UNITY_ANDROID
                       || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began
#endif
            ;

        if (clicked)
        {
            waitingForClick = false;
            StartCoroutine(PlayBackwardThenForward());
        }
    }

    void PlayForwardFromStart()
    {
        player.playbackSpeed = 1f;
        player.time = 0.0;
        player.Play();
    }

    IEnumerator PlayBackwardThenForward()
    {
        // 1) พยายามย้อนแบบ native ก่อน (หากแพลตฟอร์ม/โค้คช่วยได้)
        if (preferNativeReverse && player.canSetPlaybackSpeed)
        {
            // บางแพลตฟอร์มจะไม่รองรับ speed ติดลบ — ถ้าไม่ขยับ เราจะ fallback ให้อัตโนมัติ
            double t0 = player.time;
            player.playbackSpeed = -1f;
            player.Play();

            // รอให้เวลาเดินถอยจริงๆ หาก 0.3 วิแล้วยังไม่ถอย ให้ fallback
            float probe = 0f;
            while (player.time < t0 && player.time > 0.05f)
            {
                yield return null;
                probe += Time.unscaledDeltaTime;
                if (probe > 0.3f && Mathf.Approximately((float)player.time, (float)t0))
                {
                    // ไม่ยอมถอย -> ใช้ fallback
                    player.Pause();
                    yield return StartCoroutine(PlayBackwardBySeeking());
                    PlayForwardFromStart();
                    yield break;
                }
            }

            // ถึงหัวคลิปแล้ว -> เดินหน้าใหม่
            player.Pause();
            player.time = 0.0;
            player.playbackSpeed = 1f;
            player.Play();
            yield break;
        }

        // 2) Fallback: ไล่เฟรมย้อนด้วยการ seek
        yield return StartCoroutine(PlayBackwardBySeeking());
        PlayForwardFromStart();
    }

    IEnumerator PlayBackwardBySeeking()
    {
        player.Pause();

        // กันกรณียังไม่ prepare
        if (!player.isPrepared)
        {
            player.Prepare();
            while (!player.isPrepared) yield return null;
        }

        // กำหนดก้าวเฟรมตาม FPS เป้าหมาย
        double clipFps = player.frameRate > 1 ? player.frameRate : 30.0;
        int step = Mathf.Max(1, Mathf.RoundToInt((float)(clipFps / Mathf.Max(1, seekFps))));

        var wait = new WaitForEndOfFrame();
        long f = player.frame >= 0 ? player.frame : (long)(player.frameCount - 1);

        while (f > 0)
        {
            f = Mathf.Max(0, (int)f - step);
            player.frame = f;
            yield return wait; // รอเฟรมเรนเดอร์
        }

        player.frame = 0;
        yield return null; // ให้เฟรมแรกแสดงผล
    }
}
