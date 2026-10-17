using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SubmarineController : MonoBehaviour
{
    [Header("四档推进速度：停车 / 低速 / 巡航 / 高速")]
    public float lowSpeed = 1f;
    public float cruiseSpeed = 3f;
    public float highSpeed = 5f;
    public float acceleration = 1f;

    [Header("升降")]
    public float maxVerticalSpeed = 2f;
    public float verticalAcceleration = 0.8f;
    public float naturalSinkSpeed = 0.3f;

    [Header("配平：负数上浮，正数下沉")]
    public float trimStep = 0.1f;
    public float maxTrim = 1f;

    [Header("转向")]
    public float maxTurnSpeed = 12f;
    public float turnAcceleration = 6f;

    [Tooltip("限制偏航角度，保证潜艇始终朝航道前方行驶")]
    public float maxHeadingAngle = 65f;

    [Header("起始深度")]
    public float startingDepth = 100f;

    private int gear;
    private int currentStation = 1;

    private float currentSpeed;
    private float distanceTravelled;
    private float depth;
    private float verticalSpeed;
    private float trim;

    private float heading;
    private float turnSpeed;

    // x：左右位置；y：沿航道前进的位置。
    // 这些只是后台数据，不会移动船舱模型。
    private Vector2 navigationPosition;

    private SubmarinePower power;

    private readonly string[] gearNames =
    {
        "STOP", "LOW", "CRUISE", "HIGH"
    };

    public int CurrentGear => gear;
    public float DistanceTravelled => distanceTravelled;
    public float CurrentSpeed => currentSpeed;
    public float Depth => depth;
    public float VerticalSpeed => verticalSpeed;
    public float Heading => heading;
    public Vector2 NavigationPosition => navigationPosition;

    private void Awake()
    {
        power = GetComponent<SubmarinePower>();
        depth = Mathf.Max(0f, startingDepth);
    }

    private void Update()
    {
        float diveInput = 0f;
        float turnInput = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame)
                currentStation = 1;

            if (keyboard.digit2Key.wasPressedThisFrame)
                currentStation = 2;

            if (keyboard.digit3Key.wasPressedThisFrame)
                currentStation = 3;

            if (currentStation == 1)
            {
                if (keyboard.rKey.wasPressedThisFrame)
                    ChangeGear(1);

                if (keyboard.fKey.wasPressedThisFrame)
                    ChangeGear(-1);

                if (keyboard.wKey.isPressed)
                    diveInput -= 1f;

                if (keyboard.sKey.isPressed)
                    diveInput += 1f;

                if (keyboard.aKey.isPressed)
                    turnInput -= 1f;

                if (keyboard.dKey.isPressed)
                    turnInput += 1f;

                if (keyboard.qKey.wasPressedThisFrame)
                    ChangeTrim(-trimStep);

                if (keyboard.eKey.wasPressedThisFrame)
                    ChangeTrim(trimStep);
            }
        }

#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1))
            currentStation = 1;

        if (Input.GetKeyDown(KeyCode.Alpha2))
            currentStation = 2;

        if (Input.GetKeyDown(KeyCode.Alpha3))
            currentStation = 3;

        if (currentStation == 1)
        {
            if (Input.GetKeyDown(KeyCode.R))
                ChangeGear(1);

            if (Input.GetKeyDown(KeyCode.F))
                ChangeGear(-1);

            if (Input.GetKey(KeyCode.W))
                diveInput -= 1f;

            if (Input.GetKey(KeyCode.S))
                diveInput += 1f;

            if (Input.GetKey(KeyCode.A))
                turnInput -= 1f;

            if (Input.GetKey(KeyCode.D))
                turnInput += 1f;

            if (Input.GetKeyDown(KeyCode.Q))
                ChangeTrim(-trimStep);

            if (Input.GetKeyDown(KeyCode.E))
                ChangeTrim(trimStep);
        }
#endif

        SimulateNavigation(diveInput, turnInput, Time.deltaTime);
    }

    private void SimulateNavigation(
        float diveInput,
        float turnInput,
        float deltaTime)
    {
        bool propulsionAvailable = power != null && power.HasPower;

        float targetSpeed = propulsionAvailable
            ? GetGearSpeed()
            : 0f;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            acceleration * deltaTime
        );

        // 有前进速度时才有转向效果，停车后不能原地旋转。
        float steeringStrength = Mathf.Clamp01(
            currentSpeed / Mathf.Max(lowSpeed, 0.1f)
        );

        float targetTurnSpeed =
            turnInput * maxTurnSpeed * steeringStrength;

        turnSpeed = Mathf.MoveTowards(
            turnSpeed,
            targetTurnSpeed,
            turnAcceleration * deltaTime
        );

        heading = Mathf.Clamp(
            heading + turnSpeed * deltaTime,
            -maxHeadingAngle,
            maxHeadingAngle
        );

        // 到达转向边界时，清除继续向外转的惯性。
        if ((heading >= maxHeadingAngle && turnSpeed > 0f) ||
            (heading <= -maxHeadingAngle && turnSpeed < 0f))
        {
            turnSpeed = 0f;
        }

        float headingRadians = heading * Mathf.Deg2Rad;

        Vector2 direction = new Vector2(
            Mathf.Sin(headingRadians),
            Mathf.Cos(headingRadians)
        );

        float travelledThisFrame = currentSpeed * deltaTime;

        navigationPosition += direction * travelledThisFrame;
        distanceTravelled += travelledThisFrame;

        float targetVerticalSpeed =
            diveInput * maxVerticalSpeed
            + naturalSinkSpeed
            + trim;

        verticalSpeed = Mathf.MoveTowards(
            verticalSpeed,
            targetVerticalSpeed,
            verticalAcceleration * deltaTime
        );

        depth += verticalSpeed * deltaTime;

        if (depth < 0f)
        {
            depth = 0f;
            verticalSpeed = Mathf.Max(0f, verticalSpeed);
        }
    }

    private float GetGearSpeed()
    {
        switch (gear)
        {
            case 1: return lowSpeed;
            case 2: return cruiseSpeed;
            case 3: return highSpeed;
            default: return 0f;
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
        GUI.Box(new Rect(20, 20, 380, 330), "NAVIGATION");

        GUI.Label(new Rect(35, 50, 350, 25),
            $"Gear: {gear} - {gearNames[gear]}");

        GUI.Label(new Rect(35, 80, 350, 25),
            $"Speed: {currentSpeed:F1} m/s");

        GUI.Label(new Rect(35, 110, 350, 25),
            $"Travelled: {distanceTravelled:F1} m");

        GUI.Label(new Rect(35, 140, 350, 25),
            $"Forward: {navigationPosition.y:F1} m");

        GUI.Label(new Rect(35, 170, 350, 25),
            $"Lateral: {navigationPosition.x:F1} m (+ Right)");

        GUI.Label(new Rect(35, 200, 350, 25),
            $"Heading: {heading:F1} deg (+ Right)");

        GUI.Label(new Rect(35, 230, 350, 25),
            $"Depth: {depth:F1} m | Vertical: {verticalSpeed:F2}");

        GUI.Label(new Rect(35, 260, 350, 25),
            $"Trim: {trim:F1} | Station: {currentStation}");

        GUI.Label(new Rect(35, 290, 350, 25),
            "R/F Gear | W/S Rise/Dive | A/D Turn");

        GUI.Label(new Rect(35, 320, 350, 25),
            "Q/E Trim | 1/2/3 Station");
    }
}