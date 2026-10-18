using UnityEngine;

public class SubmarineGearIndicator : MonoBehaviour
{
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("驾驶系统")]
    public SubmarineController controller;

    [Header("支点自身的旋转轴")]
    public RotationAxis axis = RotationAxis.Z;

    [Header("相对于初始姿态的角度")]
    public float stopAngle = 0f;
    public float lowAngle = 30f;
    public float cruiseAngle = 60f;
    public float highAngle = 90f;

    [Header("每秒转动角度")]
    [Min(1f)]
    public float rotationSpeed = 120f;

    private Quaternion initialRotation;
    private float currentAngle;

    private void Awake()
    {
        initialRotation = transform.localRotation;
    }

    private void Start()
    {
        if (controller == null)
        {
            Debug.LogError(
                "请连接档位指示器的 Controller。",
                this
            );

            enabled = false;
            return;
        }

        currentAngle = GetTargetAngle();
        ApplyRotation();
    }

    private void Update()
    {
        currentAngle = Mathf.MoveTowards(
            currentAngle,
            GetTargetAngle(),
            Mathf.Max(1f, rotationSpeed) * Time.deltaTime
        );

        ApplyRotation();
    }

    private float GetTargetAngle()
    {
        switch (controller.CurrentGear)
        {
            case 1:
                return lowAngle;

            case 2:
                return cruiseAngle;

            case 3:
                return highAngle;

            default:
                return stopAngle;
        }
    }

    private Vector3 GetAxis()
    {
        switch (axis)
        {
            case RotationAxis.X:
                return Vector3.right;

            case RotationAxis.Y:
                return Vector3.up;

            default:
                return Vector3.forward;
        }
    }

    private void ApplyRotation()
    {
        transform.localRotation =
            initialRotation *
            Quaternion.AngleAxis(currentAngle, GetAxis());
    }

    private void OnDisable()
    {
        transform.localRotation = initialRotation;
    }
}