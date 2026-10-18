using UnityEngine;
using UnityEngine.EventSystems;

public class RepairChecklistItem : MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler
{
    [Header("划线物体")]
    public GameObject strikeLine;

    private RepairPaperDrag paperDrag;
    private bool crossedOut;
    private bool dragged;

    private void Awake()
    {
        paperDrag = GetComponentInParent<RepairPaperDrag>();
        RefreshLine();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragged = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        // 拖动纸张不算勾选。
        if (dragged)
            return;

        crossedOut = !crossedOut;
        RefreshLine();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        dragged = true;

        // 在条目上拖动时，仍然移动整张纸。
        if (paperDrag != null)
            paperDrag.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (paperDrag != null)
            paperDrag.OnDrag(eventData);
    }

    private void RefreshLine()
    {
        if (strikeLine != null)
            strikeLine.SetActive(crossedOut);
    }
}