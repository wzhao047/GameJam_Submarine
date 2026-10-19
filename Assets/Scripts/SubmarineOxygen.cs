using UnityEngine;

public class SubmarineOxygen : MonoBehaviour
{
    [Header("氧气可维持时间，单位为秒")]
    public float oxygenDuration = 600f;

    private float oxygenRemaining;
    private bool failed;

    private SubmarineController controller;
    private SubmarinePower power;

    public float OxygenRemaining => oxygenRemaining;

    public float OxygenPercentage =>
        oxygenDuration > 0f
            ? oxygenRemaining / oxygenDuration * 100f
            : 0f;

    public bool HasFailed => failed;
    public event System.Action<float> OxygenCollected;

    private void Awake()
    {
        oxygenRemaining = Mathf.Max(0f, oxygenDuration);

        controller = GetComponent<SubmarineController>();
        power = GetComponent<SubmarinePower>();
    }

    // Returns the amount actually restored; a failed voyage cannot be revived.
    public float AddOxygen(float seconds)
    {
        if (failed || !isActiveAndEnabled || seconds <= 0f) return 0f;
        float previous = oxygenRemaining;
        oxygenRemaining += seconds;
        float restored = oxygenRemaining - previous;
        OxygenCollected?.Invoke(restored);
        return restored;
    }
    public void ResetForMode(float seconds)
    {
        oxygenDuration = Mathf.Max(1f, seconds);
        oxygenRemaining = oxygenDuration;
        failed = false;
    }
    private void Update()
    {
        if (failed || GameMenuController.IsInputBlocked || Time.timeScale <= 0f)
            return;

        // 停车、切换位置、维修时都继续消耗。
        oxygenRemaining = Mathf.Max(
            0f,
            oxygenRemaining - Time.deltaTime
        );

        if (oxygenRemaining <= 0f)
        {
            failed = true;

            if (controller != null)
                controller.enabled = false;

            if (power != null)
                power.enabled = false;
        }
    }
}