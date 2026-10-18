using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LeakRepairInteraction : MonoBehaviour
{
    [Header("玩家相机")]
    public Camera playerCamera;

    [Header("维修对象")]
    public Transform pipeJoint;
    public Transform valve;
    public GameObject sealingClamp;

    [Header("共用工具箱")]
    public RepairSharedToolbox sharedToolbox;

    public float interactionDistance = 10f;

    private bool faultActive;
    private bool jointInspected;
    private bool valveClosed;
    private bool clampInstalled;
    private bool repaired;

    private string message = "Pipe system normal.";

    // 故障包含“已经安装密封夹，但尚未复查”的阶段。
    public bool HasFault => faultActive;

    public bool CanTriggerFault =>
        isActiveAndEnabled && !faultActive;

    // 实际是否正在进水，与故障是否完成维修分开判断。
    public bool IsLeaking =>
        faultActive && !valveClosed && !clampInstalled;

    public bool JointInspected => jointInspected;
    public bool ValveClosed => valveClosed;
    public bool ClampInstalled => clampInstalled;
    public bool Repaired => repaired;

    public bool ToolTaken =>
        sharedToolbox != null && sharedToolbox.HasTools;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (sealingClamp != null)
            sealingClamp.SetActive(clampInstalled);
    }

    // 后续统一故障调度器调用这个方法。
    public bool TriggerFault()
    {
        if (!CanTriggerFault) return false;

        faultActive = true;
        jointInspected = false;
        clampInstalled = false;
        repaired = false;

        if (sealingClamp != null)
            sealingClamp.SetActive(false);

        // 保留阀门状态，不能强行替玩家开阀。
        message = valveClosed
            ? "Pipe fault detected. V1 is already closed."
            : "Leak warning. Inspect the pipe joint.";

        return true;
    }

    private void Update()
    {
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

        if (Matches(hit.transform, pipeJoint))
            InteractWithJoint();
        else if (Matches(hit.transform, valve))
            ToggleValve();
    }

    private bool Matches(Transform hit, Transform target)
    {
        return target != null &&
            (hit == target || hit.IsChildOf(target));
    }

    private void ToggleValve()
    {
        valveClosed = !valveClosed;

        if (valveClosed)
        {
            message = "V1 CLOSED. Flow isolated.";
        }
        else if (IsLeaking)
        {
            message = "V1 OPEN. Water is leaking.";
        }
        else
        {
            message = faultActive
                ? "V1 OPEN. Recheck the repaired joint."
                : "V1 OPEN. Pipe system normal.";
        }
    }

    private void InteractWithJoint()
    {
        if (!faultActive)
        {
            message = repaired
                ? "Repair verified. No leakage."
                : "Joint normal. No repair required.";

            return;
        }

        if (!jointInspected)
        {
            jointInspected = true;
            message = "Damaged joint found. Take tools and close V1.";
            return;
        }

        if (!clampInstalled)
        {
            if (!valveClosed)
            {
                message = "Close V1 before installing the clamp.";
                return;
            }

            if (sealingClamp == null || sharedToolbox == null)
            {
                message = "Setup error: check clamp and toolbox references.";
                return;
            }

            if (!sharedToolbox.TryUseTools())
            {
                message = "Take tools from the toolbox first.";
                return;
            }

            clampInstalled = true;
            sealingClamp.SetActive(true);

            message = "Clamp installed. Reopen V1, then recheck.";
            return;
        }

        if (valveClosed)
        {
            message = "Reopen V1 before checking the seal.";
            return;
        }

        repaired = true;
        faultActive = false;
        message = "No leakage detected. Repair complete.";
    }

    private void OnGUI()
    {
        float width = Mathf.Min(580f, Screen.width - 20f);
        float top = Mathf.Max(10f, Screen.height - 165f);

        GUILayout.BeginArea(
            new Rect(10f, top, width, 155f),
            GUI.skin.box
        );

        GUILayout.Label("PIPE REPAIR");

        string status = !faultActive
            ? "NORMAL"
            : clampInstalled
                ? "AWAITING VERIFICATION"
                : "FAULT";

        GUILayout.Label($"Status: {status}");

        GUILayout.Label(
            $"Tools: {(ToolTaken ? "READY" : "NOT TAKEN")}"
            + $" | V1: {(valveClosed ? "CLOSED" : "OPEN")}"
        );

        GUILayout.Label(message);
        GUILayout.EndArea();
    }
}