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

    private void Awake()
    {
        oxygenRemaining = Mathf.Max(0f, oxygenDuration);

        controller = GetComponent<SubmarineController>();
        power = GetComponent<SubmarinePower>();
    }

    private void Update()
    {
        if (failed)
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