using UnityEngine;

public class SubmarinePower : MonoBehaviour
{
    [Header("电池")]
    public float batteryCapacity = 100f;

    [Header("每秒耗电")]
    public float baseDrain = 0.01f;
    public float sonarDrain = 0.03f;

    [Header("各档推进每秒耗电")]
    public float lowGearDrain = 0.02f;
    public float cruiseGearDrain = 0.06f;
    public float highGearDrain = 0.14f;

    [Header("声呐供电")]
    public bool sonarSwitch = true;

    private float battery;
    private SubmarineController controller;

    public bool HasPower => battery > 0f;
    public bool SonarWorking => HasPower && sonarSwitch;

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

        float drain =
            Mathf.Max(0f, baseDrain) +
            Mathf.Max(0f, GetPropulsionDrain());

        if (sonarSwitch)
            drain += Mathf.Max(0f, sonarDrain);

        return drain;
    }
}