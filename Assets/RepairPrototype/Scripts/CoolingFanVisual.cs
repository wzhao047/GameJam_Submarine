using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoolingFanVisual : MonoBehaviour
{
    public RepairCoolingPrototype cooling;
    [Tooltip("Local rotation axis. The Art_006 fan uses X.")]
    public Vector3 localAxis = Vector3.right;
    [Tooltip("Degrees per second. Use a negative value to reverse direction.")]
    public float runningSpeed = 360f;
    [Min(0.01f)] public float acceleration = 240f;
    [Min(0.01f)] public float deceleration = 180f;

    private float currentSpeed;

    private void Start()
    {
        if (cooling == null)
        {
            Debug.LogError("CoolingFanVisual: assign CoolingRepairArea's RepairCoolingPrototype.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked || Time.deltaTime <= 0f) return;
        bool running = cooling != null && cooling.isActiveAndEnabled &&
            cooling.IsPowered && !cooling.HasFault;
        float target = running ? runningSpeed : 0f;
        float previousSpeed = currentSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, target,
            Mathf.Max(0.01f, running ? acceleration : deceleration) * Time.deltaTime);
        if (localAxis.sqrMagnitude > 0.000001f)
            transform.Rotate(localAxis.normalized,
                (previousSpeed + currentSpeed) * 0.5f * Time.deltaTime, Space.Self);
    }

    private void OnDisable() => currentSpeed = 0f;
}
