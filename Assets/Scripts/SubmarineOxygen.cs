using UnityEngine;

public class SubmarineOxygen : MonoBehaviour
{
    [Header("氧气可维持的时间，单位：秒")]
    public float oxygenDuration = 600f;

    private float oxygenRemaining;
    private bool failed;

    private SubmarineController controller;
    private SubmarinePower power;

    private void Awake()
    {
        oxygenRemaining = Mathf.Max(0f, oxygenDuration);

        controller = GetComponent<SubmarineController>();
        power = GetComponent<SubmarinePower>();
    }

    private void Update()
    {
        if (failed) return;

        // 不论停车还是航行，氧气都会持续消耗。
        oxygenRemaining = Mathf.Max(
            0f,
            oxygenRemaining - Time.deltaTime
        );

        if (oxygenRemaining <= 0f)
        {
            FailVoyage();
        }
    }

    private void FailVoyage()
    {
        failed = true;

        // 暂停驾驶和耗电模拟，保留最终数据显示。
        if (controller != null)
            controller.enabled = false;

        if (power != null)
            power.enabled = false;
    }

    private string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(seconds);
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private void OnGUI()
    {
        float percentage = oxygenDuration > 0f
            ? oxygenRemaining / oxygenDuration * 100f
            : 0f;

        // 临时氧气仪表，放在驾驶测试面板下方。
        GUI.Box(new Rect(20, 365, 380, 100), "OXYGEN");

        GUI.Label(new Rect(35, 395, 350, 25),
            $"Oxygen: {percentage:F0}%");

        GUI.Label(new Rect(35, 425, 350, 25),
            $"Time remaining: {FormatTime(oxygenRemaining)}");

        Color previousColor = GUI.color;

        if (failed)
        {
            float left = (Screen.width - 440f) * 0.5f;
            float top = (Screen.height - 150f) * 0.5f;

            GUI.color = Color.red;

            GUI.Box(new Rect(left, top, 440, 150),
                "VOYAGE FAILED");

            GUI.Label(new Rect(left + 25, top + 45, 390, 30),
                "Cause: Oxygen depleted.");

            GUI.Label(new Rect(left + 25, top + 85, 390, 30),
                "Exit Play Mode to restart.");
        }
        else if (percentage <= 20f)
        {
            GUI.color = Color.yellow;

            GUI.Label(new Rect(20, 475, 400, 30),
                "WARNING: LOW OXYGEN");
        }

        GUI.color = previousColor;
    }
}
