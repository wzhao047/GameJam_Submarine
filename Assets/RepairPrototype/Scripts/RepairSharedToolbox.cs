using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RepairSharedToolbox : MonoBehaviour
{
    [Header("Debug UI")]
    public bool showDebugUI = false;

    [Header("玩家相机")]
    public Camera playerCamera;

    [Header("点击距离，单位：米")]
    public float interactionDistance = 10f;

    public bool HasTools { get; private set; }

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
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

        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance))
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                HasTools = true;
            }
        }
    }

    // 只有实际维修成功时，才消耗本次工具状态。
    public bool TryUseTools()
    {
        if (!HasTools) return false;

        HasTools = false;
        return true;
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;

        GUI.Box(
            new Rect(10f, 180f, 330f, 55f),
            HasTools
                ? "TOOLS: READY"
                : "TOOLS: NOT TAKEN - CLICK TOOLBOX"
        );
    }
}