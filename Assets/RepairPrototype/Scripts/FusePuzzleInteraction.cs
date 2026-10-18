using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class FusePuzzleInteraction : MonoBehaviour
{
    [Header("玩家相机")]
    public Camera playerCamera;

    [Header("插槽：A、B、C")]
    public Transform[] slots = new Transform[3];

    [Header("保险丝：红、黄、蓝")]
    public Transform[] fuses = new Transform[3];

    [Header("实体测试按钮")]
    public Transform testButton;

    [Header("共用电源状态")]
    public RepairCoolingPrototype cooling;

    [Header("交互设置")]
    public float interactionDistance = 10f;
    public float frontOffset = 0.08f;

    private Vector3[] homePositions;
    private Vector3[] originalScales;

    private int[] occupants = { -1, -1, -1 };

    // 红=0、黄=1、蓝=2。
    // 正确排列：A 蓝、B 黄、C 红。
    private readonly int[] solution = { 2, 1, 0 };

    private readonly string[] fuseNames =
    {
        "RED", "YELLOW", "BLUE"
    };

    private int selected = -1;
    private bool ready;
    private bool faultActive;
    private bool repaired;

    private string message = "Fuse circuit normal.";

    public bool Repaired => repaired;
    public bool HasFault => faultActive;

    public bool CanTriggerFault =>
        ready && isActiveAndEnabled && !faultActive;

    // 后续设备可以读取这个状态，目前尚未连接实际设备。
    public bool CircuitWorking =>
        ready && !faultActive &&
        cooling != null && cooling.IsPowered;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        ready =
            slots != null && slots.Length == 3 &&
            fuses != null && fuses.Length == 3 &&
            cooling != null && testButton != null;

        if (ready)
        {
            for (int i = 0; i < 3; i++)
            {
                if (slots[i] == null || fuses[i] == null)
                    ready = false;
            }
        }

        if (!ready)
        {
            message = "Setup error: check Inspector references.";
            return;
        }

        homePositions = new Vector3[3];
        originalScales = new Vector3[3];

        // 记录场景中下方备件的位置。
        for (int i = 0; i < 3; i++)
        {
            homePositions[i] = fuses[i].localPosition;
            originalScales[i] = fuses[i].localScale;
        }

        // 正常开局：自动装入正确位置。
        for (int i = 0; i < 3; i++)
            SeatFuse(solution[i], i);
    }

    // 供统一故障调度器调用。
    public bool TriggerFault()
    {
        if (!CanTriggerFault) return false;

        ClearSelection();

        faultActive = true;
        repaired = false;

        for (int i = 0; i < 3; i++)
        {
            occupants[i] = -1;
            fuses[i].localPosition = homePositions[i];
        }

        message = "Fuse fault. Switch OFF power and follow the job card.";
        return true;
    }

    private void Update()
    {
        if (!ready || playerCamera == null) return;

        if (selected >= 0 && cooling.IsPowered)
            ClearSelection();

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

        if (!clicked) return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
            return;

        Ray ray = playerCamera.ScreenPointToRay(position);

        if (!Physics.Raycast(
            ray, out RaycastHit hit, interactionDistance))
            return;

        if (Matches(hit.transform, testButton))
        {
            TestCircuit();
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            if (Matches(hit.transform, fuses[i]))
            {
                if (CanHandleFuses())
                    SelectFuse(i);

                return;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (Matches(hit.transform, slots[i]))
            {
                if (CanHandleFuses())
                    PlaceFuse(i);

                return;
            }
        }
    }

    private bool Matches(Transform hit, Transform target)
    {
        return hit == target || hit.IsChildOf(target);
    }

    private bool CanHandleFuses()
    {
        if (!faultActive)
        {
            message = "Circuit normal. No repair required.";
            return false;
        }

        if (cooling.IsPowered)
        {
            message = "Switch OFF the shared circuit first.";
            return false;
        }

        return true;
    }

    private void SelectFuse(int index)
    {
        if (selected == index)
        {
            ClearSelection();
            message = "Selection cancelled.";
            return;
        }

        ClearSelection();

        for (int i = 0; i < 3; i++)
        {
            if (occupants[i] == index)
            {
                occupants[i] = -1;
                fuses[index].localPosition = homePositions[index];
                break;
            }
        }

        selected = index;
        fuses[index].localScale = originalScales[index] * 1.15f;

        message = $"{fuseNames[index]} selected. Click an empty slot.";
    }

    private void PlaceFuse(int slotIndex)
    {
        if (selected < 0)
        {
            message = "Select a fuse first.";
            return;
        }

        if (occupants[slotIndex] >= 0)
        {
            message = "Slot occupied. Remove its fuse first.";
            return;
        }

        SeatFuse(selected, slotIndex);
        ClearSelection();

        message = "Fuse placed. Follow the job card.";
    }

    private void SeatFuse(int fuseIndex, int slotIndex)
    {
        fuses[fuseIndex].position =
            slots[slotIndex].position
            + slots[slotIndex].right * frontOffset;

        occupants[slotIndex] = fuseIndex;
    }

    private void TestCircuit()
    {
        if (!cooling.IsPowered)
        {
            message = "Restore shared circuit power before testing.";
            return;
        }

        if (!faultActive)
        {
            message = "Circuit test PASSED. Circuit normal.";
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            if (occupants[i] < 0)
            {
                message = "Test failed: one or more slots are empty.";
                return;
            }
        }

        for (int i = 0; i < 3; i++)
        {
            if (occupants[i] != solution[i])
            {
                message = "Test failed: arrangement does not match the job card.";
                return;
            }
        }

        faultActive = false;
        repaired = true;
        ClearSelection();

        message = "Circuit test PASSED. Repair complete.";
    }

    private void ClearSelection()
    {
        if (selected >= 0)
            fuses[selected].localScale = originalScales[selected];

        selected = -1;
    }

    private void OnGUI()
    {
        float left = Mathf.Max(10f, Screen.width * 0.5f - 220f);

        GUI.Box(
            new Rect(left, 10f, 440f, 90f),
            "FUSE REPAIR\n"
            + (faultActive ? "STATUS: FAULT\n" : "STATUS: NORMAL\n")
            + message
        );
    }
}