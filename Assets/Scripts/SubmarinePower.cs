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

    [Header("设备开关")]
    public bool sonarSwitch = true;
    public bool pumpSwitch = false;

    private float battery;
    private SubmarineController controller;

    public bool HasPower => battery > 0f;

    public bool SonarWorking => HasPower && sonarSwitch;
    public bool PumpWorking => HasPower && pumpSwitch;

    public float BatteryRemaining => battery;

    public float BatteryPercentage =>
        batteryCapacity > 0f
            ? battery / batteryCapacity * 100f
            : 0f;

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
        if (!HasPower)
            return 0f;

        float drain =
            Mathf.Max(0f, baseDrain)
            + Mathf.Max(0f, GetPropulsionDrain());

        if (sonarSwitch)
            drain += Mathf.Max(0f, sonarDrain);

        if (pumpSwitch)
            drain += Mathf.Max(0f, pumpDrain);

        return drain;
    }
}