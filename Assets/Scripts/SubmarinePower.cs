using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SubmarinePower : MonoBehaviour
{
    [Header("电池")]
    public float batteryCapacity = 100f;

    [Header("每秒耗电")]
    public float baseDrain = 0.01f;
    public float sonarDrain = 0.03f;
    public float pumpDrain = 0.08f;

    [Header("各档推进每秒耗电")]
    public float lowGearDrain = 0.02f;
    public float cruiseGearDrain = 0.06f;
    public float highGearDrain = 0.14f;

    [Header("开关初始状态")]
    public bool sonarSwitch = true;
    public bool pumpSwitch = false;

    private float battery;
    private SubmarineController controller;

    public bool HasPower => battery > 0f;
    public bool SonarWorking => HasPower && sonarSwitch;
    public bool PumpWorking => HasPower && pumpSwitch;

    private void Awake()
    {
        battery = Mathf.Max(0f, batteryCapacity);
        controller = GetComponent<SubmarineController>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.zKey.wasPressedThisFrame)
                sonarSwitch = !sonarSwitch;

            if (keyboard.xKey.wasPressedThisFrame)
                pumpSwitch = !pumpSwitch;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Z))
            sonarSwitch = !sonarSwitch;

        if (Input.GetKeyDown(KeyCode.X))
            pumpSwitch = !pumpSwitch;
#endif

        battery = Mathf.Max(
            0f,
            battery - GetTotalDrain() * Time.deltaTime
        );
    }

    private float GetPropulsionDrain()
    {
        if (!HasPower || controller == null)
            return 0f;

        switch (controller.CurrentGear)
        {
            case 1: return lowGearDrain;
            case 2: return cruiseGearDrain;
            case 3: return highGearDrain;
            default: return 0f;
        }
    }

    private float GetTotalDrain()
    {
        if (!HasPower) return 0f;

        float drain = baseDrain + GetPropulsionDrain();

        if (sonarSwitch) drain += sonarDrain;
        if (pumpSwitch) drain += pumpDrain;

        return drain;
    }

    private string DeviceState(bool switchOn)
    {
        if (!switchOn) return "OFF";
        return HasPower ? "ON" : "NO POWER";
    }

    private void OnGUI()
    {
        float left = Mathf.Max(420f, Screen.width - 350f);

        GUI.Box(new Rect(left, 20, 330, 240), "POWER PANEL");

        GUI.Label(new Rect(left + 15, 50, 300, 25),
            $"Battery: {battery:F1} / {batteryCapacity:F1}");

        GUI.Label(new Rect(left + 15, 80, 300, 25),
            $"Total drain: {GetTotalDrain():F2} / s");

        GUI.Label(new Rect(left + 15, 110, 300, 25),
            $"Propulsion drain: {GetPropulsionDrain():F2} / s");

        GUI.Label(new Rect(left + 15, 140, 300, 25),
            $"Z - Sonar: {DeviceState(sonarSwitch)}");

        GUI.Label(new Rect(left + 15, 170, 300, 25),
            $"X - Pump: {DeviceState(pumpSwitch)}");

        string status = !HasPower
            ? "POWER LOST - PROPULSION DISABLED"
            : battery <= batteryCapacity * 0.2f
                ? "LOW BATTERY"
                : "POWER NORMAL";

        GUI.Label(new Rect(left + 15, 210, 300, 25), status);
    }
}