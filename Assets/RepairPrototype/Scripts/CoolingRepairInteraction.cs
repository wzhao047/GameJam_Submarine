using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CoolingRepairInteraction : MonoBehaviour
{
    [Header("Debug UI")]
    public bool showDebugUI = false;

    [Header("������")]
    public Camera playerCamera;

    [Header("ά�޶���")]
    public Transform coolingJoint;
    public Transform powerSwitch;

    [Header("�¶�ϵͳ�͹��ù�����")]
    public RepairCoolingPrototype cooling;
    public RepairSharedToolbox sharedToolbox;

    public float interactionDistance = 10f;

    [Header("SwitchKid power-state visuals")]
    public Transform switchKid;
    public Material poweredOnMaterial;
    public Material poweredOffMaterial;
    private Renderer[] switchRenderers;
    private Material[][] originalSwitchMaterials;
    private Vector3 switchEulerAngles;
    private bool switchVisualsInitialized;
    private bool displayedPower;

    private bool inspected;
    private bool fixedJoint;
    private bool verified;

    private string message = "Cooling system normal.";

    public bool Repaired => verified;

    // ���޺õ���δͨ�縴�飬Ҳ�㱾�ι�����δ�����ꡣ
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

        RefreshSwitchVisuals();
    }

    // ��ͳһ���ϵ��������á�
    public bool TriggerFault()
    {
        if (!CanTriggerFault) return false;

        inspected = false;
        fixedJoint = false;
        verified = false;

        cooling.SetCoolingFault(true);

        // �����¶Ⱥ͹��ÿ���״̬��������Ҳ�����
        message = cooling.IsPowered
            ? "Cooling warning. Inspect the equipment."
            : "Cooling fault detected. Circuit is already OFF.";

        return true;
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked) return;
        if (cooling == null) return;
        CompleteRepairIfReady();

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

    private bool CompleteRepairIfReady()
    {
        if (cooling == null || !fixedJoint || verified || !cooling.IsPowered)
            return false;
        verified = true;
        message = "Cooling restored. Repair complete.";
        return true;
    }
    private void TogglePower()
    {
        cooling.SetPower(!cooling.IsPowered);
        if (CompleteRepairIfReady())
        {
            RefreshSwitchVisuals();
            return;
        }
        RefreshSwitchVisuals();

        message = cooling.IsPowered
            ? "Shared circuit ON."
            : "Shared circuit OFF.";
    }

    private void LateUpdate()
    {
        // Also follow power changes made outside this click handler.
        RefreshSwitchVisuals();
    }

    private void RefreshSwitchVisuals()
    {
        if (cooling == null) return;
        CompleteRepairIfReady();
        if (!switchVisualsInitialized)
        {
            if (switchKid == null && powerSwitch != null)
                foreach (var candidate in powerSwitch.GetComponentsInChildren<Transform>(true))
                    if (string.Equals(candidate.name, "SwitchKid", System.StringComparison.OrdinalIgnoreCase))
                    { switchKid = candidate; break; }
            if (switchKid == null) return;
            if (poweredOnMaterial == null) poweredOnMaterial = Resources.Load<Material>("SwitchPowerOn");
            if (poweredOffMaterial == null) poweredOffMaterial = Resources.Load<Material>("SwitchPowerOff");
            switchEulerAngles = switchKid.localEulerAngles;
            switchRenderers = switchKid.GetComponentsInChildren<MeshRenderer>(true);
            originalSwitchMaterials = new Material[switchRenderers.Length][];
            for (int i = 0; i < switchRenderers.Length; i++)
                originalSwitchMaterials[i] = switchRenderers[i].sharedMaterials;
            switchVisualsInitialized = true;
            ApplySwitchVisuals();
        }
        else if (cooling.IsPowered != displayedPower) ApplySwitchVisuals();
    }

    private void ApplySwitchVisuals()
    {
        displayedPower = cooling.IsPowered;
        switchKid.localRotation = Quaternion.Euler(displayedPower ? -12f : 12f,
            switchEulerAngles.y, switchEulerAngles.z);
        Material material = displayedPower ? poweredOnMaterial : poweredOffMaterial;
        if (material == null) return;
        foreach (var renderer in switchRenderers)
        {
            if (renderer == null) continue;
            var slots = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }

    private void OnDestroy()
    {
        if (!switchVisualsInitialized) return;
        for (int i = 0; i < switchRenderers.Length; i++)
            if (switchRenderers[i] != null) switchRenderers[i].sharedMaterials = originalSwitchMaterials[i];
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

            message = "Joint fixed. Restore power to complete the repair.";
            return;
        }

        if (!cooling.IsPowered)
        {
            message = "Restore power to complete the repair.";
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
                    ? "RESTORE POWER"
                    : "FAULT";

            GUILayout.Label($"Status: {status}");
            GUILayout.Label(message);
        }

        GUILayout.EndArea();
    }
}