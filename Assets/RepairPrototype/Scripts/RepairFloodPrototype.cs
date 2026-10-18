using UnityEngine;

public class RepairFloodPrototype : MonoBehaviour
{
    [Header("漏水维修逻辑")]
    public LeakRepairInteraction repair;

    [Header("积水参数")]
    public float maximumWater = 100f;
    public float leakPerSecond = 2f;
    public float drainPerSecond = 3f;

    private float water;
    private bool testPumpOn;

    public float Water => water;

    public float WaterPercentage =>
        water / Mathf.Max(1f, maximumWater) * 100f;

    private void Update()
    {
        if (repair == null) return;

        float change = 0f;

        if (repair.IsLeaking)
            change += Mathf.Max(0f, leakPerSecond);

        if (testPumpOn)
            change -= Mathf.Max(0f, drainPerSecond);

        water = Mathf.Clamp(
            water + change * Time.deltaTime,
            0f,
            Mathf.Max(1f, maximumWater)
        );
    }

    private void OnGUI()
    {
        float width = Mathf.Min(330f, Screen.width - 20f);
        float left = Screen.width - width - 10f;

        GUILayout.BeginArea(
            new Rect(left, 280f, width, 210f),
            GUI.skin.box
        );

        GUILayout.Label("BILGE WATER");

        if (repair == null)
        {
            GUILayout.Label("Assign Repair in Inspector.");
        }
        else
        {
            GUILayout.Label($"Water level: {WaterPercentage:F0}%");

            GUILayout.Label(
                repair.IsLeaking ? "Leak: ACTIVE" : "Leak: STOPPED"
            );

            GUILayout.Label(
                WaterPercentage >= 80f
                    ? "WARNING: HIGH WATER"
                    : "Water below warning level"
            );

            if (GUILayout.Button(
                testPumpOn ? "Test Pump: ON" : "Test Pump: OFF"))
            {
                testPumpOn = !testPumpOn;
            }

            GUILayout.Label("Test only: no battery connection.");
        }

        GUILayout.EndArea();
    }
}