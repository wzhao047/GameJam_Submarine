using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SubmarineController : MonoBehaviour
{
    private readonly float[] gearSpeeds = { 0f, 1f, 3f, 5f };

    private readonly string[] gearNames =
    {
        "STOP", "LOW", "CRUISE", "HIGH"
    };

    [Header("推进")]
    public float acceleration = 1f;

    [Header("升降")]
    public float maxVerticalSpeed = 2f;
    public float verticalAcceleration = 0.8f;

    [Header("配平")]
    public float naturalSinkSpeed = 0.3f;
    public float trimStep = 0.1f;
    public float maxTrim = 1f;

    private int gear = 0;
    private float currentSpeed = 0f;
    private float distanceTravelled = 0f;

    private float depth = 100f;
    private float verticalSpeed = 0f;
    private float trim = 0f;

    private SubmarinePower power;

    // 提供给配电系统读取。
    public int CurrentGear => gear;

    private void Start()
    {
        power = GetComponent<SubmarinePower>();
    }

    private void Update()
    {
        float diveInput = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.rKey.wasPressedThisFrame)
                ChangeGear(1);

            if (keyboard.fKey.wasPressedThisFrame)
                ChangeGear(-1);

            if (keyboard.wKey.isPressed)
                diveInput -= 1f;

            if (keyboard.sKey.isPressed)
                diveInput += 1f;

            if (keyboard.qKey.wasPressedThisFrame)
                ChangeTrim(-trimStep);

            if (keyboard.eKey.wasPressedThisFrame)
                ChangeTrim(trimStep);
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.R))
            ChangeGear(1);

        if (Input.GetKeyDown(KeyCode.F))
            ChangeGear(-1);

        if (Input.GetKey(KeyCode.W))
            diveInput -= 1f;

        if (Input.GetKey(KeyCode.S))
            diveInput += 1f;

        if (Input.GetKeyDown(KeyCode.Q))
            ChangeTrim(-trimStep);

        if (Input.GetKeyDown(KeyCode.E))
            ChangeTrim(trimStep);
#endif

        // 电池耗尽后，推进目标速度变为零。
        bool propulsionAvailable = power != null && power.HasPower;
        float targetSpeed = propulsionAvailable ? gearSpeeds[gear] : 0f;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            acceleration * Time.deltaTime
        );

        distanceTravelled += currentSpeed * Time.deltaTime;

        float targetVerticalSpeed =
            diveInput * maxVerticalSpeed
            + naturalSinkSpeed
            + trim;

        verticalSpeed = Mathf.MoveTowards(
            verticalSpeed,
            targetVerticalSpeed,
            verticalAcceleration * Time.deltaTime
        );

        depth += verticalSpeed * Time.deltaTime;

        if (depth < 0f)
        {
            depth = 0f;
            verticalSpeed = Mathf.Max(0f, verticalSpeed);
        }
    }

    private void ChangeGear(int change)
    {
        gear = Mathf.Clamp(gear + change, 0, 3);
    }

    private void ChangeTrim(float change)
    {
        trim = Mathf.Clamp(trim + change, -maxTrim, maxTrim);
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(20, 20, 380, 330), "SUBMARINE");

        GUI.Label(new Rect(35, 50, 350, 25),
            $"Gear: {gear} - {gearNames[gear]}");

        GUI.Label(new Rect(35, 80, 350, 25),
            $"Speed: {currentSpeed:F1} m/s");

        GUI.Label(new Rect(35, 110, 350, 25),
            $"Distance: {distanceTravelled:F1} m");

        GUI.Label(new Rect(35, 140, 350, 25),
            $"Depth: {depth:F2} m");

        GUI.Label(new Rect(35, 170, 350, 25),
            $"Vertical: {verticalSpeed:F2} m/s (+ Down)");

        GUI.Label(new Rect(35, 200, 350, 25),
            $"Trim: {trim:F1} m/s (- Up / + Down)");

        GUI.Label(new Rect(35, 230, 350, 25),
            Mathf.Abs(verticalSpeed) < 0.05f
                ? "Depth trend: STABLE"
                : "Depth trend: CHANGING");

        GUI.Label(new Rect(35, 260, 350, 25),
            "R / F: Gear Up / Down");

        GUI.Label(new Rect(35, 290, 350, 25),
            "W / S: Rise / Dive");

        GUI.Label(new Rect(35, 320, 350, 25),
            "Q / E: Trim Up / Down");
    }
}