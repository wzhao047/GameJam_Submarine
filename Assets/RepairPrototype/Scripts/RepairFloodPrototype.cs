using UnityEngine;

public class RepairFloodPrototype : MonoBehaviour
{
    [Header("Debug UI")]
    public bool showDebugUI = false;

    [Header("漏水维修逻辑")]
    public LeakRepairInteraction repair;

    [Header("累计进水参数")]
    public float maximumWater = 100f;
    public float leakPerSecond = 2f;

    private float water;

    public float Water => water;

    public float WaterPercentage =>
        water / Mathf.Max(1f, maximumWater) * 100f;

    private void Update()
    {
        if (repair == null || !repair.IsLeaking)
            return;

        water = Mathf.Clamp(
            water + Mathf.Max(0f, leakPerSecond) * Time.deltaTime,
            0f,
            Mathf.Max(1f, maximumWater)
        );
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        float width = Mathf.Min(330f, Screen.width - 20f);
        float left = Screen.width - width - 10f;

        GUILayout.BeginArea(
            new Rect(left, 280f, width, 135f),
            GUI.skin.box
        );

        GUILayout.Label("ACCUMULATED WATER");

        if (repair == null)
        {
            GUILayout.Label("Assign Repair in Inspector.");
        }
        else
        {
            GUILayout.Label($"Water: {WaterPercentage:F0}%");

            GUILayout.Label(
                repair.IsLeaking ? "Leak: ACTIVE" : "Leak: STOPPED"
            );

            if (WaterPercentage >= 80f)
                GUILayout.Label("WARNING: HIGH WATER");
        }

        GUILayout.EndArea();
    }
}