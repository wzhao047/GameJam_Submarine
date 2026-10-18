using UnityEngine;
using TMPro;

[DefaultExecutionOrder(1000)]
public class RepairVoyageResult : MonoBehaviour
{
    [Header("航行系统")]
    public SubmarineController controller;
    public SubmarineOxygen oxygen;
    public SubmarinePower power;
    public Camera playerCamera;

    [Header("维修记录与故障调度")]
    public RepairVoyageRecord record;
    public RepairFaultDirector director;

    [Header("终点：沿航道向前的距离，单位米")]
    [Min(1f)]
    public float destinationForwardDistance = 1500f;

    [Header("结算界面")]
    public GameObject resultPanel;
    public TMP_Text resultText;

    private bool ended;
    private float startTime;

    private void Start()
    {
        if (controller == null || oxygen == null || power == null ||
            record == null || director == null ||
            resultPanel == null || resultText == null ||
            record.flood == null || record.temperature == null ||
            record.leak == null || record.cooling == null ||
            record.fuse == null)
        {
            Debug.LogError("请补齐结算系统及维修记录的引用。", this);
            enabled = false;
            return;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        startTime = Time.time;
        resultPanel.SetActive(false);
    }

    private void LateUpdate()
    {
        if (ended) return;

        // 同一帧同时满足失败和抵达条件时，优先判定失败。
        if (oxygen.HasFailed)
        {
            EndVoyage(false, "OXYGEN DEPLETED");
        }
        else if (record.flood.WaterPercentage >= 100f)
        {
            EndVoyage(false, "FLOODING LIMIT REACHED");
        }
        else if (
            controller.NavigationPosition.y >=
            Mathf.Max(1f, destinationForwardDistance))
        {
            EndVoyage(true, "DESTINATION REACHED");
        }
    }

    private void EndVoyage(bool success, string cause)
    {
        if (ended) return;
        ended = true;

        float elapsed = Time.time - startTime;
        string report = record.BuildReport();

        string title = success
            ? "VOYAGE COMPLETE"
            : "VOYAGE FAILED";

        resultText.text =
            title + "\n\n" +
            $"Result: {cause}\n" +
            $"Voyage time: {elapsed:F0} s\n" +
            $"Travelled: {controller.DistanceTravelled:F0} m\n" +
            $"Forward progress: {controller.NavigationPosition.y:F0} m\n" +
            $"Oxygen remaining: {oxygen.OxygenPercentage:F0}%\n\n" +
            report +
            "\n\nClick Restart to begin a new voyage.";

        // 停止航行、供电、氧气及故障调度。
        StopScript(director);
        StopScript(controller);
        StopScript(power);
        StopScript(oxygen);

        StopScript(controller.GetComponent<SubmarineHull>());

        // 停止维修与环境变化。
        StopScript(record.leak);
        StopScript(record.cooling);
        StopScript(record.fuse);
        StopScript(record.flood);
        StopScript(record.temperature);

        StopScript(record.leak.sharedToolbox);
        StopScript(record.cooling.sharedToolbox);

        StopScript(
            record.leak.GetComponent<LeakWaterFeedback>()
        );

        StopScript(record);

        // 停止视角切换。
        if (playerCamera != null)
        {
            StopScript(
                playerCamera.GetComponent<RepairStationView>()
            );

            StopScript(
                playerCamera.GetComponent<StationViewSwitcher>()
            );
        }

        RepairFaultAlarm alarm =
            FindAnyObjectByType<RepairFaultAlarm>();

        if (alarm != null)
        {
            StopScript(alarm);

            AudioSource source = alarm.GetComponent<AudioSource>();

            if (source != null)
                source.Stop();
        }

        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
    }

    private void StopScript(MonoBehaviour script)
    {
        if (script != null)
            script.enabled = false;
    }
}