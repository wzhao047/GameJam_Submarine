using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SubmarineControlVisuals : MonoBehaviour
{
    [Header("拖入需要旋转的模型或支点")]
    [SerializeField] private Transform trigger;
    [SerializeField] private Transform wheel;

    [Header("每次过渡所需时间（秒）")]
    [SerializeField, Min(0.01f)] private float duration = 1f;

    private Vector3 triggerEuler;
    private Vector3 wheelEuler;

    private readonly AngleTransition triggerAngle = new AngleTransition();
    private readonly AngleTransition wheelAngle = new AngleTransition();

    private void Start()
    {
        if (trigger == null || wheel == null)
        {
            Debug.LogError("请在 Inspector 中指定 Trigger 和 Wheel。", this);
            enabled = false;
            return;
        }

        triggerEuler = trigger.localEulerAngles;
        wheelEuler = wheel.localEulerAngles;

        triggerAngle.Reset(30f);
        wheelAngle.Reset(180f);
        ApplyRotation();
    }

    private void Update()
    {
        ReadKeys(out bool w, out bool s, out bool a, out bool d);

        // 相反方向同时按下时，回到默认位置。
        float triggerTarget = w == s ? 30f : (w ? 0f : 50f);
        float wheelTarget = a == d ? 180f : (a ? -50f : 420f);

        triggerAngle.Step(triggerTarget, duration, Time.deltaTime);
        wheelAngle.Step(wheelTarget, duration, Time.deltaTime);

        ApplyRotation();
    }

    private void ApplyRotation()
    {
        // Trigger 只绕本地 Y 轴旋转，X 和 Z 始终为 0。
        trigger.localRotation =
            Quaternion.Euler(0f, triggerAngle.Current, 0f);

        wheelEuler.z = wheelAngle.Current;
        wheel.localRotation = Quaternion.Euler(wheelEuler);
    }

    private static void ReadKeys(
        out bool w, out bool s, out bool a, out bool d)
    {
        w = s = a = d = false;

        if (!Application.isFocused)
            return;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        w = keyboard.wKey.isPressed;
        s = keyboard.sKey.isPressed;
        a = keyboard.aKey.isPressed;
        d = keyboard.dKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
        w = Input.GetKey(KeyCode.W);
        s = Input.GetKey(KeyCode.S);
        a = Input.GetKey(KeyCode.A);
        d = Input.GetKey(KeyCode.D);
#endif
    }

    private sealed class AngleTransition
    {
        public float Current { get; private set; }

        private float start;
        private float target;
        private float elapsed;

        public void Reset(float angle)
        {
            Current = start = target = angle;
            elapsed = 0f;
        }

        public void Step(float nextTarget, float seconds, float deltaTime)
        {
            if (nextTarget != target)
            {
                start = Current;
                target = nextTarget;
                elapsed = 0f;
            }

            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(seconds, 0.01f));

            // 插值数值角度，保留方向盘超过 180° 的旋转行程。
            Current = Mathf.Lerp(start, target, t);
        }
    }
}