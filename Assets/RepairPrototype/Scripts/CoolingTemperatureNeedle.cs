using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoolingTemperatureNeedle : MonoBehaviour
{
    public RepairCoolingPrototype cooling;
    [Header("Temperature calibration")]
    public float minimumTemperature = 0f;
    public float maximumTemperature = 120f;
    public float minimumAngle = 65f;
    public float maximumAngle = -192f;

    private Vector3 initialEuler;

    private void Awake() => initialEuler = transform.localEulerAngles;

    private void Start()
    {
        if (cooling == null)
        {
            Debug.LogError("CoolingTemperatureNeedle: assign CoolingRepairArea's RepairCoolingPrototype.", this);
            enabled = false;
            return;
        }
        RefreshNeedle();
    }

    private void LateUpdate()
    {
        if (cooling != null) RefreshNeedle();
    }

    private void RefreshNeedle()
    {
        float fraction = Mathf.InverseLerp(minimumTemperature, maximumTemperature, cooling.Temperature);
        // Use the calibrated 257-degree sweep, rather than the shortest angular path.
        float angle = Mathf.Lerp(minimumAngle, maximumAngle, fraction);
        transform.localRotation = Quaternion.Euler(angle, initialEuler.y, initialEuler.z);
    }
}
