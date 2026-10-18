using UnityEngine;
using TMPro;

public class SubmarineOxygenDisplay : MonoBehaviour
{
    [Header("已有氧气系统")]
    public SubmarineOxygen oxygen;

    [Header("屏幕文字")]
    public TMP_Text oxygenText;

    [Header("颜色")]
    public Color normalColor =
        new Color(0.5f, 1f, 0.35f);

    public Color warningColor =
        new Color(1f, 0.55f, 0.15f);

    [Header("低氧警告阈值")]
    [Range(0f, 100f)]
    public float warningPercentage = 20f;

    [Header("校准测试：只改变显示")]
    public bool calibrationMode = false;

    [Range(0f, 100f)]
    public float calibrationPercentage = 100f;

    private int lastPercentage = -1;

    private void Start()
    {
        if (oxygen == null || oxygenText == null)
        {
            Debug.LogError(
                "请连接 Oxygen 和 Oxygen Text。",
                this
            );

            enabled = false;
            return;
        }

        RefreshDisplay();
    }

    private void Update()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        float percentage = calibrationMode
            ? calibrationPercentage
            : oxygen.OxygenPercentage;

        percentage = Mathf.Clamp(percentage, 0f, 100f);

        int displayedPercentage =
            Mathf.CeilToInt(percentage);

        if (displayedPercentage != lastPercentage)
        {
            oxygenText.text =
                $"O2 {displayedPercentage}%";

            lastPercentage = displayedPercentage;
        }

        oxygenText.color =
            percentage <= warningPercentage
                ? warningColor
                : normalColor;
    }
}