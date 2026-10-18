using UnityEngine;

public class SubmarineSpeedGauge : MonoBehaviour
{
    [Header("驾驶系统")]
    public SubmarineController controller;

    [Header("表盘满刻度速度：米/秒")]
    [Min(0.1f)]
    public float maximumSpeed = 5f;

    [Header("指针对应的Z角度")]
    public float zeroAngle = 125f;
    public float fullAngle = -55f;

    [Header("指针响应速度：度/秒")]
    [Min(1f)]
    public float rotationSpeed = 90f;

    [Header("校准测试：只改变显示")]
    public bool calibrationMode = false;

    [Min(0f)]
    public float calibrationSpeed = 0f;

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
        if (controller == null)
        {
            Debug.LogError(
                "请连接速度表的 Controller。",
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
        float speed = calibrationMode
            ? calibrationSpeed
            : controller.CurrentSpeed;

        float proportion = Mathf.Clamp01(
            speed / Mathf.Max(0.1f, maximumSpeed)
        );

        return Mathf.Lerp(
            zeroAngle,
            fullAngle,
            proportion
        );
    }

    private void ApplyRotation()
    {
        transform.localRotation = Quaternion.Euler(
            fixedEuler.x,
            fixedEuler.y,
            currentAngle
        );
    }

    private void OnDisable()
    {
        if (initialized)
            transform.localRotation = originalRotation;
    }
}