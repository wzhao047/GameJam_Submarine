using UnityEngine;

public class SubmarineTemperatureGauge : MonoBehaviour
{
    [Header("已有温度系统")]
    public RepairCoolingPrototype cooling;

    [Header("表盘温度范围")]
    public float minimumTemperature = 0f;
    public float maximumTemperature = 100f;

    [Header("指针对应的X角度")]
    public float minimumAngle = -25f;
    public float maximumAngle = -120f;

    [Header("指针响应速度：度/秒")]
    [Min(1f)]
    public float rotationSpeed = 60f;

    [Header("校准测试：只改变显示，不改变实际温度")]
    public bool calibrationMode = false;

    [Range(0f, 120f)]
    public float calibrationTemperature = 0f;

    private Quaternion originalRotation;
    private Vector3 fixedEuler;
    private float currentAngle;
    private bool initialized;

    private void Awake()
    {
        originalRotation = transform.localRotation;
        fixedEuler = transform.localEulerAngles;
    }

    private void Start()
    {
        if (cooling == null)
        {
            Debug.LogError(
                "请把 CoolingRepairArea 拖入温度表的 Cooling。",
                this
            );

            enabled = false;
            return;
        }

        currentAngle = GetTargetAngle();
        ApplyRotation();
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        currentAngle = Mathf.MoveTowards(
            currentAngle,
            GetTargetAngle(),
            Mathf.Max(1f, rotationSpeed) * Time.deltaTime
        );

        ApplyRotation();
    }

    private float GetTargetAngle()
    {
        float temperature = calibrationMode
            ? calibrationTemperature
            : cooling.Temperature;

        float upperLimit = Mathf.Max(
            minimumTemperature + 0.1f,
            maximumTemperature
        );

        float proportion = Mathf.InverseLerp(
            minimumTemperature,
            upperLimit,
            temperature
        );

        return Mathf.Lerp(
            minimumAngle,
            maximumAngle,
            proportion
        );
    }

    private void ApplyRotation()
    {
        // 只改变X，保留模型原来的Y和Z。
        transform.localRotation = Quaternion.Euler(
            currentAngle,
            fixedEuler.y,
            fixedEuler.z
        );
    }

    private void OnDisable()
    {
        if (initialized)
            transform.localRotation = originalRotation;
    }
}