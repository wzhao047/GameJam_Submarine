using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CoolingRepairInteraction : MonoBehaviour
{
    [Header("Debug UI")]
    public bool showDebugUI = false;

    [Header("玩家相机")]
    public Camera playerCamera;

    [Header("维修对象")]
    public Transform coolingJoint;
    public Transform powerSwitch;

    [Header("温度系统和共用工具箱")]
    public RepairCoolingPrototype cooling;
    public RepairSharedToolbox sharedToolbox;

    public float interactionDistance = 10f;

    private bool inspected;
    private bool fixedJoint;
    private bool verified;

    private string message = "Cooling system normal.";

    public bool Repaired => verified;

    // 已修好但尚未通电复查，也算本次故障尚未处理完。
    public bool HasFault =>
        cooling != null &&
        (cooling.HasFault || (fixedJoint && !verified));

    public bool CanTriggerFault =>
        isActiveAndEnabled &&
        cooling != null &&
        cooling.isActiveAndEnabled &&
        !HasFault;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    // 供统一故障调度器调用。
    public bool TriggerFault()
    {
        if (!CanTriggerFault) return false;

        inspected = false;
        fixedJoint = false;
        verified = false;

        cooling.SetCoolingFault(true);

        // 保留温度和共用开关状态，不替玩家操作。
        message = cooling.IsPowered
            ? "Cooling warning. Inspect the equipment."
            : "Cooling fault detected. Circuit is already OFF.";

        return true;
    }

    private void Update()
    {
        if (cooling == null) return;

        bool clicked = false;
        Vector2 position = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        clicked = mouse.leftButton.wasPressedThisFrame;
        position = mouse.position.ReadValue();

#elif ENABLE_LEGACY_INPUT_MANAGER
        clicked = Input.GetMouseButtonDown(0);
        position = Input.mousePosition;
#endif

        if (!clicked || playerCamera == null) return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = playerCamera.ScreenPointToRay(position);

        if (!Physics.Raycast(
            ray, out RaycastHit hit, interactionDistance))
            return;

        if (Matches(hit.transform, powerSwitch))
            TogglePower();
        else if (Matches(hit.transform, coolingJoint))
            InteractWithJoint();
    }

    private bool Matches(Transform hit, Transform target)
    {
        return target != null &&
            (hit == target || hit.IsChildOf(target));
    }

    private void TogglePower()
    {
        cooling.SetPower(!cooling.IsPowered);

        message = cooling.IsPowered
            ? "Shared circuit ON."
            : "Shared circuit OFF.";
    }

    private void InteractWithJoint()
    {
        if (!HasFault)
        {
            message = verified
                ? "Cooling repair verified."
                : "Joint normal. No repair required.";

            return;
        }

        if (!inspected)
        {
            inspected = true;
            message = "Loose joint found. Switch OFF power before repair.";
            return;
        }

        if (!fixedJoint)
        {
            if (cooling.IsPowered)
            {
                message = "Switch OFF the shared circuit first.";
                return;
            }

            if (sharedToolbox == null)
            {
                message = "Setup error: assign Shared Toolbox.";
                return;
            }

            if (!sharedToolbox.TryUseTools())
            {
                message = "Take tools from the shared toolbox.";
                return;
            }

            fixedJoint = true;
            cooling.SetCoolingFault(false);

            message = "Joint fixed. Restore power, then recheck.";
            return;
        }

        if (!cooling.IsPowered)
        {
            message = "Restore power before verification.";
            return;
        }

        verified = true;
        message = "Cooling restored. Observe the temperature.";
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        float width = Mathf.Min(440f, Screen.width - 20f);

        GUILayout.BeginArea(
            new Rect(10f, 245f, width, 145f),
            GUI.skin.box
        );

        GUILayout.Label("COOLING REPAIR");

        if (cooling == null)
        {
            GUILayout.Label("Assign Cooling in Inspector.");
        }
        else
        {
            string status = !HasFault
                ? "NORMAL"
                : fixedJoint
                    ? "AWAITING VERIFICATION"
                    : "FAULT";

            GUILayout.Label($"Status: {status}");
            GUILayout.Label(message);
        }

        GUILayout.EndArea();
    }
}