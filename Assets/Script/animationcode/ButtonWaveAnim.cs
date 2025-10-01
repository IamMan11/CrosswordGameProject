using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonWaveAnim : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] RectTransform target;   // ถ้าไม่ตั้ง จะใช้ RectTransform ของตัวเอง
    [Header("Scale")]
    [SerializeField] float normalScale = 1f;
    [SerializeField] float hoverScale  = 1.06f;
    [SerializeField] float scaleDamp   = 0.12f;   // เวลาไหลเข้า-ออกของสเกล

    [Header("Idle Sway (ตอนลอย)")]
    [SerializeField] float swayAngle  = 5f;       // องศาซ้าย-ขวาสูงสุด
    [SerializeField] float swayPeriod = 2.8f;     // วินาทีต่อหนึ่งรอบ (ช้าลง=เลขมากขึ้น)
    [SerializeField] float rotateDamp = 0.18f;    // ความนุ่มเวลาปรับองศา

    [Header("Pressed")]
    [SerializeField] float pressedAngle = -7f;    // กดแล้วเอียงซ้ายเล็กน้อย
    [SerializeField] float pressRotateDamp = 0.07f;

    enum State { Idle, Hover, Pressed }
    State state = State.Idle;

    float rotVel;     // สำหรับ SmoothDampAngle
    float scaleVel;   // สำหรับ SmoothDamp scale
    bool isHovered;
    bool isPressed;
    float phase;      // ให้แต่ละปุ่มส่ายไม่พร้อมกัน

    void Reset() { target = GetComponent<RectTransform>(); }
    void Awake() {
        if (!target) target = GetComponent<RectTransform>();
        phase = Random.value * Mathf.PI * 2f;
    }
    void OnEnable() {
        SetScaleImmediate(normalScale);
        SetRotationImmediate(0f);
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;

        // เลือกเป้าหมายองศาตามสถานะ
        float targetZ;
        switch (state) {
            case State.Idle:
                float w = 2f * Mathf.PI / Mathf.Max(0.01f, swayPeriod);
                targetZ = Mathf.Sin((Time.unscaledTime + phase) * w) * swayAngle;
                break;
            case State.Hover:
                targetZ = 0f; // กลับมาตรง
                break;
            case State.Pressed:
                targetZ = pressedAngle; // เอียงซ้ายตอนกด
                break;
            default:
                targetZ = 0f; break;
        }

        float damp = (state == State.Pressed) ? pressRotateDamp : rotateDamp;

        // หมุนแกน Z อย่างนุ่มนวล
        float currentZ = GetCurrentZ();
        float z = Mathf.SmoothDampAngle(currentZ, targetZ, ref rotVel, damp, Mathf.Infinity, dt);
        target.localRotation = Quaternion.Euler(0, 0, z);

        // ขยายตอน Hover/Pressed, ปกติกลับเป็น normal
        float targetS = (state == State.Idle) ? normalScale : hoverScale;
        float s = Mathf.SmoothDamp(target.localScale.x, targetS, ref scaleVel, scaleDamp, Mathf.Infinity, dt);
        target.localScale = new Vector3(s, s, 1f);
    }

    public void OnPointerEnter(PointerEventData e) { isHovered = true;  if (!isPressed) state = State.Hover; }
    public void OnPointerExit (PointerEventData e) { isHovered = false; if (!isPressed) state = State.Idle;  }
    public void OnPointerDown (PointerEventData e) { isPressed = true;  state = State.Pressed; }
    public void OnPointerUp   (PointerEventData e) { isPressed = false; state = isHovered ? State.Hover : State.Idle; }

    float GetCurrentZ() {
        float z = target.localEulerAngles.z;
        if (z > 180f) z -= 360f; // ให้เป็นช่วง [-180,180]
        return z;
    }
    void SetScaleImmediate(float s)    => target.localScale = new Vector3(s, s, 1f);
    void SetRotationImmediate(float z) => target.localRotation = Quaternion.Euler(0, 0, z);
}
