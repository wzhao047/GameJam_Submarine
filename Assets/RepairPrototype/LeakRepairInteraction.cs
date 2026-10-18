using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class LeakRepairInteraction : MonoBehaviour
{
    [Header("玩家相机")]
    public Camera playerCamera;

    [Header("可点击的维修物体")]
    public Transform pipeJoint;
    public Transform valve;

    [Header("交互距离，单位：米")]
    public float interactionDistance = 10f;

    private bool jointInspected;
    private bool valveClosed;

    private string message =
        "Inspect the pipe joint to locate the leak.";

    public bool JointInspected => jointInspected;
    public bool ValveClosed => valveClosed;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    private void Update()
    {
        bool clicked = false;
        Vector2 pointerPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        clicked = mouse.leftButton.wasPressedThisFrame;
        pointerPosition = mouse.position.ReadValue();

#elif ENABLE_LEGACY_INPUT_MANAGER
        clicked = Input.GetMouseButtonDown(0);
        pointerPosition = Input.mousePosition;
#endif

        if (!clicked || playerCamera == null)
            return;

        // 点击维修单时，不操作后面的三维物体。
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = playerCamera.ScreenPointToRay(pointerPosition);

        if (!Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactionDistance))
        {
            return;
        }

        if (Matches(hit.transform, pipeJoint))
        {
            InspectJoint();
        }
        else if (Matches(hit.transform, valve))
        {
            ToggleValve();
        }
    }

    private bool Matches(Transform hit, Transform target)
    {
        return target != null &&
            (hit == target || hit.IsChildOf(target));
    }

    private void InspectJoint()
    {
        jointInspected = true;

        message = valveClosed
            ? "Flow isolated. The joint still needs sealing."
            : "Water is leaking from the joint. Close valve V1.";
    }

    private void ToggleValve()
    {
        valveClosed = !valveClosed;

        message = valveClosed
            ? "Valve V1 CLOSED. Flow isolated; repair still required."
            : "Valve V1 OPEN. The unsealed joint is leaking.";
    }

    private void OnGUI()
    {
        float width = Mathf.Min(520f, Screen.width - 20f);
        float top = Mathf.Max(10f, Screen.height - 135f);

        GUILayout.BeginArea(
            new Rect(10f, top, width, 125f),
            GUI.skin.box
        );

        GUILayout.Label("PIPE LEAK - REPAIR TEST");

        GUILayout.Label(
            $"Joint inspected: {(jointInspected ? "YES" : "NO")}"
            + $" | V1: {(valveClosed ? "CLOSED" : "OPEN")}"
        );

        GUILayout.Label(message);

        GUILayout.EndArea();
    }
}