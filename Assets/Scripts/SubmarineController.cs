using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SubmarineController : MonoBehaviour
{
    [Header("Debug UI")]
    public bool showDebugUI = false;

    [Header("当前视角控制器")]
    public RepairStationView stationView;

    [Header("四档推进速度")]
    public float lowSpeed = 1f;
    public float cruiseSpeed = 3f;
    public float highSpeed = 5f;
    public float acceleration = 1f;

    [Header("升降")]
    public float maxVerticalSpeed = 2f;
    public float verticalAcceleration = 0.8f;
    public float naturalSinkSpeed = 0.3f;

    [Header("配平")]
    public float trimStep = 0.1f;
    public float maxTrim = 1f;

    [Header("转向")]
    public float maxTurnSpeed = 12f;
    public float turnAcceleration = 6f;
    public float maxHeadingAngle = 65f;

    [Header("初始深度")]
    public float startingDepth = 100f;

    private int gear;
    private float currentSpeed;
    private float distanceTravelled;
    private float depth;
    private float verticalSpeed;
    private float trim;
    private float heading;
    private float turnSpeed;

    private Vector2 navigationPosition;
    private SubmarinePower power;
    private SubmarineHull hull;

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

    public bool CanDrive =>
        isActiveAndEnabled &&
        stationView != null &&
        stationView.IsDriving;

    private void Awake()
    {
        power = GetComponent<SubmarinePower>();
        hull = GetComponent<SubmarineHull>();
        depth = Mathf.Max(0f, startingDepth);
    }

    private void Start()
    {
        if (stationView == null)
        {
            Debug.LogError(
                "请在 SubmarineController 中绑定 Station View。",
                this
            );
        }
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked) return;
        float diveInput = 0f;
        float turnInput = 0f;

        // 只有驾驶台允许读取驾驶输入。
        if (CanDrive)
        {
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

                if (keyboard.aKey.isPressed)
                    turnInput -= 1f;

                if (keyboard.dKey.isPressed)
                    turnInput += 1f;


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

            if (Input.GetKey(KeyCode.A))
                turnInput -= 1f;

            if (Input.GetKey(KeyCode.D))
                turnInput += 1f;


#endif
        }

        // 离开驾驶台后，航行和惯性仍继续模拟。
        SimulateNavigation(diveInput, turnInput, Time.deltaTime);
    }

    private void SimulateNavigation(
        float diveInput,
        float turnInput,
        float deltaTime)
    {
        bool propulsionAvailable = power != null && power.HasPower;

        float targetSpeed = propulsionAvailable ? GetGearSpeed() : (gear > 0 ? lowSpeed : 0f);
        if (hull != null && hull.IsScrapingReef) targetSpeed *= 0.4f;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            acceleration * deltaTime
        );

        float steeringStrength = Mathf.Clamp01(
            currentSpeed / Mathf.Max(lowSpeed, 0.1f)
        );

        turnSpeed = Mathf.MoveTowards(
            turnSpeed,
            turnInput * maxTurnSpeed * steeringStrength,
            turnAcceleration * deltaTime
        );

        heading = Mathf.Repeat(heading + turnSpeed * deltaTime + 180f, 360f) - 180f;

        float radians = heading * Mathf.Deg2Rad;

        Vector2 direction = new Vector2(
            Mathf.Sin(radians),
            Mathf.Cos(radians)
        );

        float movement = currentSpeed * deltaTime;

        navigationPosition += direction * movement;
        distanceTravelled += movement;

        float targetVerticalSpeed =
            diveInput * maxVerticalSpeed + naturalSinkSpeed + trim;

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
        if (!showDebugUI) return;

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
            $"Lateral: {navigationPosition.x:F1} m");

        GUI.Label(new Rect(35, 200, 350, 25),
            $"Heading: {heading:F1} deg");

        GUI.Label(new Rect(35, 230, 350, 25),
            $"Depth: {depth:F1} m | Vertical: {verticalSpeed:F2}");

        string station = stationView != null
            ? stationView.StationName
            : "NOT ASSIGNED";

        GUI.Label(new Rect(35, 260, 350, 25),
            $"Trim: {trim:F1} | Station: {station}");

        GUI.Label(new Rect(35, 290, 350, 25),
            CanDrive ? "DRIVING CONTROLS AVAILABLE" : "DRIVING CONTROLS LOCKED");

        GUI.Label(new Rect(35, 320, 350, 25),
            "R/F Gear | W/S Rise/Dive | A/D Turn | Q/E Trim");
    }
}