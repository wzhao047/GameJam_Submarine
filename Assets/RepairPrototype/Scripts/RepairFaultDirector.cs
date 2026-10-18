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

        remainingTime = RandomDelay(firstFaultDelay);
    }

    private void Update()
    {
        remainingTime -= Time.deltaTime;

        if (remainingTime > 0f)
            return;

        TryTriggerFault();

        // 不论上一项故障是否修好，都安排下一次事件。
        remainingTime = RandomDelay(eventInterval);
    }

    private void TryTriggerFault()
    {
        candidates.Clear();

        if (leak.CanTriggerFault)
            candidates.Add(0);

        if (cooling.CanTriggerFault)
            candidates.Add(1);

        if (fuse.CanTriggerFault)
            candidates.Add(2);

        // 全部设备都在故障或等待复查时，本次不再叠加。
        if (candidates.Count == 0)
            return;

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
            Debug.Log(
                $"[Repair event] {faultName}",
                this
            );
        }
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