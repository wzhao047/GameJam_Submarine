using UnityEngine;
using TMPro;

public class SubmarineReadout : MonoBehaviour
{
    [Header("操纵台上的文字")]
    public TMP_Text navigationReadout;

    private SubmarineController controller;
    private SubmarineSonar sonar;
    private SubmarineOxygen oxygen;
    private SubmarinePower power;

    private float refreshTimer;
    private const float RefreshInterval = 0.1f;

    private void Awake()
    {
        controller = GetComponent<SubmarineController>();
        sonar = GetComponent<SubmarineSonar>();
        oxygen = GetComponent<SubmarineOxygen>();
        power = GetComponent<SubmarinePower>();
    }

    private void Start()
    {
        if (controller == null ||
            sonar == null ||
            oxygen == null ||
            power == null ||
            navigationReadout == null)
        {
            Debug.LogError(
                "请确认 Submarine 上的驾驶、声呐、氧气、供电组件，以及文字引用。",
                this
            );

            enabled = false;
            return;
        }

        RefreshReadout();
    }

    private void Update()
    {
        refreshTimer += Time.deltaTime;

        if (refreshTimer < RefreshInterval)
            return;

        refreshTimer = 0f;
        RefreshReadout();
    }

    private void RefreshReadout()
    {
        if (oxygen.HasFailed)
        {
            navigationReadout.text =
                "<color=#FFAA55>VOYAGE FAILED</color>\n\n" +
                "OXYGEN DEPLETED\n\n" +
                $"TRAVELLED {controller.DistanceTravelled:F0} m";

            return;
        }

        string contactText;

        if (!sonar.IsWorking)
        {
            contactText = "SONAR OFFLINE\nRANGE --";
        }
        else if (!sonar.HasContact)
        {
            contactText = "SCANNING...\nRANGE --";
        }
        else
        {
            float difference = sonar.ContactDepthDifference;

            string depthText;

            if (Mathf.Abs(difference) < 1f)
                depthText = "SAME LEVEL";
            else if (difference > 0f)
                depthText = $"{difference:F1} m BELOW";
            else
                depthText = $"{-difference:F1} m ABOVE";

            contactText =
                $"RANGE {sonar.ContactRange:F1} m\n" +
                depthText;
        }

        int totalSeconds = Mathf.CeilToInt(
            oxygen.OxygenRemaining
        );

        string oxygenText =
            $"O2 {oxygen.OxygenPercentage:F0}%  " +
            $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";

        if (oxygen.OxygenPercentage <= 20f)
            oxygenText = Warning(oxygenText + " LOW");

        string batteryText =
            $"BAT {power.BatteryPercentage:F0}%";

        if (!power.HasPower)
            batteryText = Warning("BAT EMPTY");
        else if (power.BatteryPercentage <= 20f)
            batteryText = Warning(batteryText + " LOW");

        string sonarState = !power.HasPower
            ? "NO POWER"
            : power.SonarWorking ? "ON" : "OFF";

        string pumpState = !power.HasPower
            ? "NO POWER"
            : power.PumpWorking ? "ON" : "OFF";

        navigationReadout.text =
            "NAVIGATION\n" +
            $"SPEED {controller.CurrentSpeed:F1} m/s  " +
            $"DEPTH {controller.Depth:F1} m\n" +
            $"VERT {controller.VerticalSpeed:F1}  " +
            $"HDG {controller.Heading:F1}\n\n" +
            $"{contactText}\n\n" +
            $"{oxygenText}\n" +
            $"{batteryText}\n" +
            $"SONAR {sonarState}  PUMP {pumpState}";
    }

    private string Warning(string text)
    {
        return $"<color=#FFAA55>{text}</color>";
    }
}