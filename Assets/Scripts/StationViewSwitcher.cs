using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StationViewSwitcher : MonoBehaviour
{
    public Transform drivingView;
    public Transform powerView;
    public Transform repairView;

    private void Start()
    {
        SwitchView(drivingView);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame)
            SwitchView(drivingView);

        if (keyboard.digit2Key.wasPressedThisFrame)
            SwitchView(powerView);

        if (keyboard.digit3Key.wasPressedThisFrame)
            SwitchView(repairView);

#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SwitchView(drivingView);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            SwitchView(powerView);

        if (Input.GetKeyDown(KeyCode.Alpha3))
            SwitchView(repairView);
#endif
    }

    private void SwitchView(Transform target)
    {
        if (target == null) return;

        transform.SetPositionAndRotation(
            target.position,
            target.rotation
        );
    }
}