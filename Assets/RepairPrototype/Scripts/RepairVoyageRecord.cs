using UnityEngine;

public class RepairVoyageRecord : MonoBehaviour
{
    [Header("三种维修系统")]
    public LeakRepairInteraction leak;
    public CoolingRepairInteraction cooling;
    public FusePuzzleInteraction fuse;

    [Header("积水和温度")]
    public RepairFloodPrototype flood;
    public RepairCoolingPrototype temperature;

    [Header("本次运行记录")]
    [SerializeField] private int leakEvents;
    [SerializeField] private int coolingEvents;
    [SerializeField] private int fuseEvents;

    [SerializeField] private int leakRepairs;
    [SerializeField] private int coolingRepairs;
    [SerializeField] private int fuseRepairs;

    [SerializeField] private float peakWaterPercentage;
    [SerializeField] private float peakTemperature;

    [SerializeField] private bool leakPending;
    [SerializeField] private bool coolingPending;
    [SerializeField] private bool fusePending;

    private bool previousLeak;
    private bool previousCooling;
    private bool previousFuse;
    private bool ready;

    private void Start()
    {
        ready = leak != null && cooling != null &&
                fuse != null && flood != null &&
                temperature != null;

        if (!ready)
        {
            Debug.LogError("请补齐维修记录的五个引用。", this);
            enabled = false;
            return;
        }

        peakTemperature = temperature.Temperature;
        peakWaterPercentage = flood.WaterPercentage;
    }

    private void LateUpdate()
    {
        CaptureRecord();
    }

    private void CaptureRecord()
    {
        if (!ready) return;

        RecordState(
            leak.HasFault, leak.Repaired,
            ref previousLeak, ref leakEvents, ref leakRepairs
        );

        RecordState(
            cooling.HasFault, cooling.Repaired,
            ref previousCooling, ref coolingEvents, ref coolingRepairs
        );

        RecordState(
            fuse.HasFault, fuse.Repaired,
            ref previousFuse, ref fuseEvents, ref fuseRepairs
        );

        peakWaterPercentage = Mathf.Max(
            peakWaterPercentage, flood.WaterPercentage
        );

        peakTemperature = Mathf.Max(
            peakTemperature, temperature.Temperature
        );

        leakPending = leak.HasFault;
        coolingPending = cooling.HasFault;
        fusePending = fuse.HasFault;
    }

    private void RecordState(
        bool fault, bool repaired,
        ref bool previousFault,
        ref int eventCount, ref int repairCount)
    {
        if (fault && !previousFault)
            eventCount++;

        if (!fault && previousFault && repaired)
            repairCount++;

        previousFault = fault;
    }

    public string BuildReport()
    {
        CaptureRecord();

        return
            $"Leak: {leakEvents} faults / {leakRepairs} repaired\n" +
            $"Cooling: {coolingEvents} faults / {coolingRepairs} repaired\n" +
            $"Fuse: {fuseEvents} faults / {fuseRepairs} repaired\n\n" +
            $"Peak water: {peakWaterPercentage:F0}%\n" +
            $"Peak temperature: {peakTemperature:F1} C\n\n" +
            $"Unresolved faults: " +
            $"{(leakPending ? 1 : 0) + (coolingPending ? 1 : 0) + (fusePending ? 1 : 0)}";
    }

    [ContextMenu("Print Current Record")]
    private void PrintCurrentRecord()
    {
        Debug.Log(BuildReport(), this);
    }
}