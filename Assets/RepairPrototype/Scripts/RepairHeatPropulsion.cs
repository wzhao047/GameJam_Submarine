using UnityEngine;

// 在驾驶模拟前更新推进能力。
[DefaultExecutionOrder(-50)]
public class RepairHeatPropulsion : MonoBehaviour
{
    [Header("驾驶和温度系统")]
    public SubmarineController controller;
    public RepairCoolingPrototype cooling;

    [Header("最高温度时保留的推进能力")]
    [Range(0.1f, 1f)]
    public float minimumEfficiency = 0.4f;

    [Header("推进能力每秒变化速度")]
    [Min(0.01f)]
    public float responseSpeed = 0.25f;

    [Header("当前推进效率，仅用于观察")]
    [SerializeField] private float efficiency = 1f;

    private float normalLowSpeed;
    private float normalCruiseSpeed;
    private float normalHighSpeed;

    private bool initialized;

    private void Start()
    {
        if (controller == null || cooling == null)
        {
            Debug.LogError("请绑定驾驶和温度系统。", this);
            enabled = false;
            return;
        }

        // 保存场景中原本的速度设置。
        normalLowSpeed = controller.lowSpeed;
        normalCruiseSpeed = controller.cruiseSpeed;
        normalHighSpeed = controller.highSpeed;

        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !controller.isActiveAndEnabled)
            return;

        float warning = cooling.warningTemperature;

        float maximum = Mathf.Max(
            warning + 1f,
            cooling.maximumTemperature
        );

        float heat = Mathf.InverseLerp(
            warning,
            maximum,
            cooling.Temperature
        );

        float targetEfficiency = Mathf.Lerp(
            1f,
            Mathf.Clamp(minimumEfficiency, 0.1f, 1f),
            heat
        );

        efficiency = Mathf.MoveTowards(
            efficiency,
            targetEfficiency,
            Mathf.Max(0.01f, responseSpeed) * Time.deltaTime
        );

        controller.lowSpeed = normalLowSpeed * efficiency;
        controller.cruiseSpeed = normalCruiseSpeed * efficiency;
        controller.highSpeed = normalHighSpeed * efficiency;
    }

    private void OnDisable()
    {
        if (!initialized || controller == null)
            return;

        controller.lowSpeed = normalLowSpeed;
        controller.cruiseSpeed = normalCruiseSpeed;
        controller.highSpeed = normalHighSpeed;

        efficiency = 1f;
    }
}