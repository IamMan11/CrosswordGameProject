using UnityEngine;

/// ตัวช่วยสำหรับรัน/หยุด Coroutine ที่ไม่ผูกกับ GameObject UI ใดๆ
/// ทำให้เราสามารถเริ่มคอร์รูลีนได้ แม้ Cardpanel จะ inactive อยู่
public sealed class CoroutineRunner : MonoBehaviour
{
    static CoroutineRunner _inst;
    public static CoroutineRunner Instance {
        get {
            if (_inst == null) {
                var go = new GameObject("[CoroutineRunner]");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<CoroutineRunner>();
            }
            return _inst;
        }
    }
}
