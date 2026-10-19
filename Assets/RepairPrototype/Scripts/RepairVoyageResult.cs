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

    [Header("维修系统")]
    public RepairVoyageRecord record;
    public RepairFaultDirector director;

    [Header("声呐上的终点标记")]
    public SubmarineGoalMarker goalMarker;

    [Header("到达条件")]
    [Min(1f)]
    public float arrivalRadius = 20f;

    [Min(0.1f)]
    public float arrivalDepthTolerance = 10f;

    [Header("结算界面")]
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("Ending credits (optional scene override)")]
    public RepairEndingCredits endingCredits;

    private SubmarineHull hull;
    private bool ended;
    private float startTime;

    private void Start()
    {
        if (controller == null ||
            oxygen == null ||
            power == null ||
            record == null ||
            director == null ||
            goalMarker == null ||
            resultPanel == null ||
            resultText == null)
        {
            SetupError();
            return;
        }

        if (record.flood == null ||
            record.temperature == null ||
            record.leak == null ||
            record.cooling == null ||
            record.fuse == null)
        {
            SetupError();
            return;
        }

        if (goalMarker.controller != controller)
        {
            Debug.LogError(
                "终点标记和结算必须引用同一个 SubmarineController。",
                this
            );

            enabled = false;
            return;
        }

        hull = controller.GetComponent<SubmarineHull>();

        if (hull == null)
        {
            SetupError();
            return;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        startTime = Time.time;
        resultPanel.SetActive(false);
    }

    private void SetupError()
    {
        Debug.LogError(
            "请检查 RepairVoyageResult 的航行、维修、终点和界面引用。",
            this
        );

        enabled = false;
    }

    private void LateUpdate()
    {
        if (ended)
            return;

        // 同一帧到达与失败同时发生时，优先判定失败。
        if (hull.HasFailed)
        {
            EndVoyage(false, "HULL DESTROYED - THREE COLLISIONS");
        }
        else if (oxygen.HasFailed)
        {
            EndVoyage(false, "OXYGEN DEPLETED");
        }
        else if (record.flood.WaterPercentage >= 100f)
        {
            EndVoyage(false, "FLOODING LIMIT REACHED");
        }
        else if (HasArrived())
        {
            EndVoyage(true, "DESTINATION REACHED");
        }
    }

    private bool HasArrived()
    {
        Vector3 goal = goalMarker.GoalPosition;
        Vector2 position = controller.NavigationPosition;

        Vector2 horizontalOffset = new Vector2(
            goal.x - position.x,
            goal.z - position.y
        );

        float radius = Mathf.Max(1f, arrivalRadius);

        bool horizontallyClose =
            horizontalOffset.sqrMagnitude <= radius * radius;

        bool depthCorrect =
            Mathf.Abs(goal.y - controller.Depth)
            <= Mathf.Max(0.1f, arrivalDepthTolerance);

        return horizontallyClose && depthCorrect;
    }

    private void EndVoyage(bool success, string cause)
    {
        if (ended)
            return;

        ended = true;

        float elapsed = Time.time - startTime;
        string report = record.BuildReport();

        resultText.text =
            $"Voyage time: {elapsed:F0} s\n" +
            $"Travelled: {controller.DistanceTravelled:F0} m\n" +
            $"Forward progress: {controller.NavigationPosition.y:F0} m\n" +
            $"Oxygen remaining: {oxygen.OxygenPercentage:F0}%\n" +
            $"Hull remaining: {hull.HullPercentage:F0}%\n" +
            $"Collisions: {hull.CollisionCount}/3\n\n" +
            report;

        StopScript(director);
        StopScript(controller);
        StopScript(power);
        StopScript(oxygen);
        StopScript(hull);

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

        // Keep the old scene UI hidden; its references remain valid for existing scenes.
        resultPanel.SetActive(false);
        if (endingCredits == null)
        {
            var template = Resources.Load<RepairEndingCredits>("EndingCreditsDefaults");
            endingCredits = template != null
                ? Instantiate(template)
                : new GameObject("Ending credits", typeof(RectTransform)).AddComponent<RepairEndingCredits>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(endingCredits.gameObject, gameObject.scene);
        }
        endingCredits.gameObject.SetActive(true);
        endingCredits.Show(success, cause, resultText.text, RestartFromEnding);
    }

    private void RestartFromEnding()
    {
        var restart = GetComponent<RepairRestart>();
        if (restart == null) restart = gameObject.AddComponent<RepairRestart>();
        restart.RestartVoyage();
    }

    private void StopScript(MonoBehaviour script)
    {
        if (script != null)
            script.enabled = false;
    }
}