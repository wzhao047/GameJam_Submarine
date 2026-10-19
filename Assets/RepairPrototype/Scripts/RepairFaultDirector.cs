using System.Collections.Generic;
using UnityEngine;

public class RepairFaultDirector : MonoBehaviour
{
    [Header("三种维修系统")]
    public LeakRepairInteraction leak;
    public CoolingRepairInteraction cooling;
    public FusePuzzleInteraction fuse;

    [Header("首次故障等待时间，单位：秒")]
    public Vector2 firstFaultDelay = new Vector2(30f, 60f);

    [Header("之后每次事件的随机间隔")]
    public Vector2 eventInterval = new Vector2(45f, 90f);

    [Header("Fault variety")]
    [Tooltip("Protection time after a system is repaired before it can fail again.")]
    [Min(0f)] public float repairedSystemCooldown = 120f;
    public bool avoidConsecutiveFaults = true;
    private int lastFault = -1;
    private readonly int[] triggerCounts = new int[3];
    private readonly float[] cooldownRemaining = new float[3];
    private readonly bool[] wasFaulted = new bool[3];
    private float remainingTime;

    private readonly List<int> candidates = new List<int>(3);

    private void Start()
    {
        if (leak == null || cooling == null || fuse == null)
        {
            Debug.LogError(
                "RepairFaultDirector：请绑定三种维修系统。",
                this
            );

            enabled = false;
            return;
        }

        wasFaulted[0] = leak.HasFault;
        wasFaulted[1] = cooling.HasFault;
        wasFaulted[2] = fuse.HasFault;
        remainingTime = RandomDelay(firstFaultDelay);
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked || Time.timeScale <= 0f) return;
        TrackRecovery(0, leak.HasFault);
        TrackRecovery(1, cooling.HasFault);
        TrackRecovery(2, fuse.HasFault);
        remainingTime -= Time.deltaTime;
        if (remainingTime > 0f)
            return;

        TryTriggerFault();

        // 不论上一项故障是否修好，都安排下一次事件。
        remainingTime = RandomDelay(eventInterval);
    }

    private void TryTriggerFault()
    {
        var mode = VoyageModeSession.Current;
        int active = (leak.HasFault ? 1 : 0) + (cooling.HasFault ? 1 : 0) + (fuse.HasFault ? 1 : 0);
        if (mode != null && (!mode.FaultSafeToStart || active >= mode.MaximumFaults || mode.Elapsed < mode.Profile.faultGrace)) return;
        // Fuse and cooling failures both impair propulsion; do not stack them.
        candidates.Clear();

        if (leak.CanTriggerFault && cooldownRemaining[0] <= 0f)
            candidates.Add(0);

        if (!fuse.HasFault && cooling.CanTriggerFault && cooldownRemaining[1] <= 0f)
            candidates.Add(1);

        if (!cooling.HasFault && fuse.CanTriggerFault && cooldownRemaining[2] <= 0f)
            candidates.Add(2);

        // 全部设备都在故障或等待复查时，本次不再叠加。
        if (candidates.Count == 0)
            return;

        // Avoid an immediate repeat when another eligible system is available.
        if (avoidConsecutiveFaults && candidates.Count > 1)
            candidates.Remove(lastFault);
        int minimumCount = int.MaxValue;
        foreach (int candidate in candidates)
            minimumCount = Mathf.Min(minimumCount, triggerCounts[candidate]);
        for (int i = candidates.Count - 1; i >= 0; i--)
            if (triggerCounts[candidates[i]] > minimumCount) candidates.RemoveAt(i);

        int choice = candidates[
            Random.Range(0, candidates.Count)
        ];

        bool triggered = false;
        string faultName = "";

        switch (choice)
        {
            case 0:
                triggered = leak.TriggerFault();
                faultName = "Pipe leak";
                break;

            case 1:
                triggered = cooling.TriggerFault();
                faultName = "Cooling fault";
                break;

            case 2:
                triggered = fuse.TriggerFault();
                faultName = "Fuse fault";
                break;
        }

        if (triggered)
        {
            lastFault = choice;
            triggerCounts[choice]++;
            wasFaulted[choice] = true;
            Debug.Log(
                $"[Repair event] {faultName}",
                this
            );
        }
    }

    private void TrackRecovery(int index, bool faulted)
    {
        cooldownRemaining[index] = Mathf.Max(0f, cooldownRemaining[index] - Time.deltaTime);
        if (wasFaulted[index] && !faulted)
            cooldownRemaining[index] = Mathf.Max(0f, repairedSystemCooldown);
        wasFaulted[index] = faulted;
    }
    private float RandomDelay(Vector2 range)
    {
        float minimum = Mathf.Max(
            1f,
            Mathf.Min(range.x, range.y)
        );

        float maximum = Mathf.Max(
            minimum,
            Mathf.Max(range.x, range.y)
        );

        return Random.Range(minimum, maximum);
    }
}