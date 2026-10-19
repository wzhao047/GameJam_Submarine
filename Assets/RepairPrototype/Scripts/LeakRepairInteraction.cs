using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LeakRepairInteraction : MonoBehaviour
{
    [Header("Debug UI")]
    public bool showDebugUI = false;

    [Header("������")]
    public Camera playerCamera;

    [Header("ά�޶���")]
    public Transform pipeJoint;
    public Transform valve;
    public GameObject sealingClamp;

    [Header("���ù�����")]
    public RepairSharedToolbox sharedToolbox;

    public float interactionDistance = 10f;

    [Header("Valve visual rotation")]
    [Min(0.01f)] public float valveRotationDuration = 0.35f;

    private bool faultActive;
    private bool jointInspected;
    private bool valveClosed;
    private Transform valveRotationTarget;
    private Vector3 valveEulerAngles;
    private float valveCurrentX;
    private float valveStartX;
    private float valveRotationElapsed;
    private bool valveRotating;
    private bool clampInstalled;
    private bool repaired;

    private string message = "Pipe system normal.";

    // ���ϰ������Ѿ���װ�ܷ�У�����δ���顱�Ľ׶Ρ�
    public bool HasFault => faultActive;

    public bool CanTriggerFault =>
        isActiveAndEnabled && !faultActive;

    // ʵ���Ƿ����ڽ�ˮ��������Ƿ����ά�޷ֿ��жϡ�
    public bool IsLeaking =>
        faultActive && !valveClosed && !clampInstalled;

    public bool JointInspected => jointInspected;
    public bool ValveClosed => valveClosed;
    public bool ClampInstalled => clampInstalled;
    public bool Repaired => repaired;

    public bool ToolTaken =>
        sharedToolbox != null && sharedToolbox.HasTools;

    [Header("Repair action sounds")]
    public AudioClip valveTurnClip;
    public AudioClip clampInstallClip;
    [Range(0f, 1f)] public float actionVolume = 0.55f;
    private AudioSource actionSource;

    private void Start()
    {
        actionSource = gameObject.AddComponent<AudioSource>();
        actionSource.playOnAwake = false;
        actionSource.loop = false;
        actionSource.spatialBlend = 0f;
        if (playerCamera == null)
            playerCamera = Camera.main;

        if (sealingClamp != null)
            sealingClamp.SetActive(clampInstalled);
    }

    // ����ͳһ���ϵ������������������
    public bool TriggerFault()
    {
        if (!CanTriggerFault) return false;

        faultActive = true;
        jointInspected = false;
        clampInstalled = false;
        repaired = false;

        if (sealingClamp != null)
            sealingClamp.SetActive(false);

        // ��������״̬������ǿ������ҿ�����
        message = valveClosed
            ? "Pipe fault detected. V1 is already closed."
            : "Leak warning. Inspect the pipe joint.";

        return true;
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked) return;
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

    private void PlayActionSound(AudioClip clip)
    {
        if (clip == null || actionSource == null) return;
        actionSource.Stop();
        actionSource.PlayOneShot(clip, actionVolume);
    }
    private bool Matches(Transform hit, Transform target)
    {
        return target != null &&
            (hit == target || hit.IsChildOf(target));
    }

    private void ToggleValve()
    {
        if (valve != null)
        {
            if (valveRotationTarget != valve)
            {
                valveRotationTarget = valve;
                valveEulerAngles = valve.localEulerAngles;
                valveCurrentX = valveEulerAngles.x;
            }
            // Retain every click, including clicks during an unfinished turn.
            // Unwrapped Euler angles keep X moving forward across 360 degrees.
            valveStartX = valveCurrentX;
            valveEulerAngles.x += 50f;
            valveRotationElapsed = 0f;
            valveRotating = true;
        }
        valveClosed = !valveClosed;
        PlayActionSound(valveTurnClip);
        if (faultActive && clampInstalled && !valveClosed)
        {
            repaired = true;
            faultActive = false;
            message = "No leakage detected. Repair complete.";
            return;
        }

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
                ? "V1 OPEN. Pipe repair pending."
                : "V1 OPEN. Pipe system normal.";
        }
    }

    private void LateUpdate()
    {
        if (!valveRotating) return;
        if (valve == null || valve != valveRotationTarget)
        { valveRotating = false; return; }

        valveRotationElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(valveRotationElapsed / Mathf.Max(0.01f, valveRotationDuration));
        valveCurrentX = Mathf.Lerp(valveStartX, valveEulerAngles.x, Mathf.SmoothStep(0f, 1f, progress));
        valve.localRotation = Quaternion.Euler(valveCurrentX, valveEulerAngles.y, valveEulerAngles.z);
        if (progress >= 1f) valveRotating = false;
    }

    private void OnDisable()
    {
        if (actionSource != null) actionSource.Stop();
        // Finish the visual state if the voyage ends or this component is disabled.
        if (valveRotating && valve != null && valve == valveRotationTarget)
        {
            valveCurrentX = valveEulerAngles.x;
            valve.localRotation = Quaternion.Euler(valveEulerAngles);
        }
        valveRotating = false;
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
            PlayActionSound(clampInstallClip);

            message = "Clamp installed. Reopen V1 to complete the repair.";
            return;
        }

        if (valveClosed)
        {
            message = "Reopen V1 to complete the repair.";
            return;
        }

        repaired = true;
        faultActive = false;
        message = "No leakage detected. Repair complete.";
    }

    private void OnDestroy()
    {
        if (actionSource != null) Destroy(actionSource);
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

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
                ? "REOPEN V1"
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