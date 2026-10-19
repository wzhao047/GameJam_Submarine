using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RepairFileHolderInteraction : MonoBehaviour
{
    [Header("玩家相机")]
    public Camera playerCamera;

    [Header("屏幕上的维修单")]
    public GameObject paper;

    [Header("最远点击距离，单位：米")]
    public float interactionDistance = 10f;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        // 开始时将纸张收起。
        if (paper != null)
            paper.SetActive(false);
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked) return;
        bool clicked = false;
        Vector2 pointerPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;

        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame && paper != null && paper.activeSelf)
        {
            paper.SetActive(false);
            return;
        }

        clicked = mouse.leftButton.wasPressedThisFrame;
        pointerPosition = mouse.position.ReadValue();

#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1) && paper != null && paper.activeSelf)
        {
            paper.SetActive(false);
            return;
        }

        clicked = Input.GetMouseButtonDown(0);
        pointerPosition = Input.mousePosition;
#endif

        if (!clicked || playerCamera == null || paper == null)
            return;

        // 点击纸张等 UI 时，不穿透到墙上的收纳夹。
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

        // 只响应这个收纳夹及其子物体。
        bool hitHolder =
            hit.transform == transform ||
            hit.transform.IsChildOf(transform);

        if (!hitHolder) return;

        bool show = !paper.activeSelf;
        paper.SetActive(show);

        if (show)
            paper.transform.SetAsLastSibling();
    }
}