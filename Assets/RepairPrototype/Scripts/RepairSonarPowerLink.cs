using UnityEngine;
using UnityEngine.UI;

public class RepairSonarPowerLink : MonoBehaviour
{
    [Header("保险丝维修系统")]
    public FusePuzzleInteraction fuse;

    [Header("原来的声呐供电系统")]
    public SubmarinePower power;

    [Header("声呐显示组件")]
    public RawImage sonarScreen;

    private bool originalEnabled;
    private bool initialized;

    private void Start()
    {
        if (fuse == null || power == null || sonarScreen == null)
        {
            Debug.LogError(
                "请绑定保险丝系统、原供电系统和声呐显示组件。",
                this
            );

            enabled = false;
            return;
        }

        originalEnabled = sonarScreen.enabled;
        initialized = true;
    }

    private void LateUpdate()
    {
        // 原声呐开关、电池、维修回路和保险丝都正常时显示。
        bool working =
            power.SonarWorking &&
            fuse.CircuitWorking;

        sonarScreen.enabled = originalEnabled && working;
    }

    private void OnDisable()
    {
        if (initialized && sonarScreen != null)
            sonarScreen.enabled = originalEnabled;
    }
}