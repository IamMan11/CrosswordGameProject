using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

[DisallowMultipleComponent]
public class PingPongTwoPlayersNoFlicker : MonoBehaviour
{
    public RawImage bottomImage, topImage;      // RawImage 2 ตัวซ้อนกัน
    public VideoPlayer forwardPlayer, reversePlayer;
    public RenderTexture forwardRT, reverseRT;
    public bool startWithForward = true;
    bool _gotNextFirstFrame = false;
    [Range(0f, 0.2f)] public float switchEarlySeconds = 0.02f;

    VideoPlayer current, next; RenderTexture currentRT, nextRT;
    Coroutine watchCo; bool switching;

    void Awake()
    {
        Application.runInBackground = true;

        Setup(forwardPlayer);
        Setup(reversePlayer);

        // ผูก RT ให้ RawImage ตั้งแต่แรก
        bottomImage.texture = forwardRT;
        topImage.texture    = reverseRT;
        topImage.enabled    = false;
    }

    void Setup(VideoPlayer vp)
    {
        if (!vp) return;
        vp.playOnAwake = false;
        vp.isLooping = false;
        vp.waitForFirstFrame = true;
        vp.skipOnDrop = false;
        vp.audioOutputMode = VideoAudioOutputMode.None;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        vp.playbackSpeed = 1f;
        vp.targetTexture = null;
    }

    IEnumerator Start()
    {
        yield return Prepare(forwardPlayer);
        yield return Prepare(reversePlayer);
        yield return PrerenderFirst(forwardPlayer, forwardRT);
        yield return PrerenderFirst(reversePlayer, reverseRT);

        if (startWithForward)
            Begin(forwardPlayer, forwardRT, reversePlayer, reverseRT);
        else
            Begin(reversePlayer, reverseRT, forwardPlayer, forwardRT);

        StartCoroutine(KickIfStalled()); // กันค้างเงียบ ๆ
    }

    IEnumerator Prepare(VideoPlayer vp)
    {
        if (vp.isPrepared) yield break;
        vp.Prepare();
        while (!vp.isPrepared) yield return null;
    }

    IEnumerator PrerenderFirst(VideoPlayer vp, RenderTexture rt)
    {
        vp.targetTexture = rt;
        vp.frame = 0;
        vp.Play();
        yield return new WaitForEndOfFrame(); // ให้มีเฟรมแรกจริง ๆ ลง RT
        vp.Pause();
        vp.targetTexture = null;
    }

    void Begin(VideoPlayer a, RenderTexture aRT, VideoPlayer b, RenderTexture bRT)
    {
        current = a; currentRT = aRT;
        next = b;    nextRT = bRT;

        current.targetTexture = currentRT;
        bottomImage.texture   = currentRT;
        bottomImage.enabled   = true;

        current.frame = 0;
        current.loopPointReached += OnLoop;
        current.Play();

        if (watchCo != null) StopCoroutine(watchCo);
        watchCo = StartCoroutine(WatchAndQueueSwitch());
    }

    IEnumerator WatchAndQueueSwitch()
    {
        while (current.frameCount <= 0) yield return null;
        var fps = (double)current.frameRate;
        var earlyFrames = Mathf.CeilToInt((float)(switchEarlySeconds * fps));
        for (;;)
        {
            long remain = (long)current.frameCount - 1 - current.frame;
            if (!switching && remain <= earlyFrames) StartPreSwitch();
            yield return null;
        }
    }

    void OnLoop(VideoPlayer _) => StartPreSwitch();

    void StartPreSwitch()
    {
        if (switching) return;
        switching = true;

        _gotNextFirstFrame = false;  

        next.sendFrameReadyEvents = true;
        next.frameReady += OnNextFirstFrameReady;
        next.frame = 0;
        next.targetTexture = nextRT;
        next.Play();
    }

    void OnNextFirstFrameReady(VideoPlayer src, long frame)
    {
        if (_gotNextFirstFrame) return;       // รับแค่ครั้งแรกก็พอ
        _gotNextFirstFrame = true;
        src.sendFrameReadyEvents = false;
        src.frameReady -= OnNextFirstFrameReady;
        StartCoroutine(FinishSwitchNextFrame());
    }

    IEnumerator FinishSwitchNextFrame()
    {
        yield return null;                 // รอ 1 tick ให้ RT มีภาพแล้ว
        topImage.texture = nextRT;
        topImage.enabled = true;           // โชว์ตัวใหม่ทับก่อน

        current.Pause();
        current.targetTexture = null;
        bottomImage.enabled = false;

        // swap image
        var tmpI = bottomImage; bottomImage = topImage; topImage = tmpI;
        topImage.enabled = false;

        // swap player/RT
        var tmpP = current; current = next; next = tmpP;
        var tmpR = currentRT; currentRT = nextRT; nextRT = tmpR;

        current.loopPointReached += OnLoop;

        if (watchCo != null) StopCoroutine(watchCo);
        watchCo = StartCoroutine(WatchAndQueueSwitch());
        switching = false;
    }

    // รีสตาร์ทเล็ก ๆ หากเฟรมไม่เดิน (บางเครื่อง WMF ชอบนิ่งที่เฟรม 0)
    IEnumerator KickIfStalled()
    {
        for (;;)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            if (current == null) continue;
            long f = current.frame;
            yield return new WaitForSecondsRealtime(0.05f);
            if (current.frame == f) current.Play();
        }
    }
}
