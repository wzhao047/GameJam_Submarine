using UnityEngine;

public class RepairCoolingPrototype : MonoBehaviour
{
    [Header("温度，单位：摄氏度")]
    public float startingTemperature = 40f;
    public float normalTemperature = 40f;
    public float warningTemperature = 80f;
    public float maximumTemperature = 120f;

    [Header("每秒温度变化")]
    public float faultHeatingRate = 1f;
    public float recoveryCoolingRate = 2f;
    public float passiveCoolingRate = 0.3f;

    private float temperature;
    private bool coolingFault;
    private bool powered = true;

    public float Temperature => temperature;
    public bool HasFault => coolingFault;
    public bool IsPowered => powered;
    public bool IsOverheated => temperature >= warningTemperature;

    private void Awake()
    {
        temperature = startingTemperature;
    }

    private void Update()
    {
        bool heating = powered && coolingFault;

        float target = heating
            ? Mathf.Max(normalTemperature, maximumTemperature)
            : normalTemperature;

        float rate = !powered
            ? passiveCoolingRate
            : coolingFault
                ? faultHeatingRate
                : recoveryCoolingRate;

        temperature = Mathf.MoveTowards(
            temperature,
            target,
            Mathf.Max(0f, rate) * Time.deltaTime
        );
    }

    public void SetCoolingFault(bool active)
    {
        coolingFault = active;
    }

    public void SetPower(bool active)
    {
        powered = active;
    }

    private void OnGUI()
    {
        float width = Mathf.Min(330f, Screen.width - 20f);
        float left = Screen.width - width - 10f;

        GUILayout.BeginArea(
            new Rect(left, 500f, width, 145f),
            GUI.skin.box
        );

        GUILayout.Label("COOLING - ISOLATED TEST");
        GUILayout.Label($"Temperature: {temperature:F1} C");
        GUILayout.Label(powered ? "Power: ON" : "Power: OFF");
        GUILayout.Label(coolingFault ? "Condition: FAULT" : "Condition: FIXED");

        if (IsOverheated)
            GUILayout.Label("WARNING: OVERHEATING");

        GUILayout.EndArea();
    }
}