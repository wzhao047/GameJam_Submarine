using UnityEngine;
using UnityEngine.EventSystems;

public class RepairPaperDrag : MonoBehaviour,
    IBeginDragHandler, IDragHandler
{
    [Header("需要移动的纸张")]
    public RectTransform paper;

    private RectTransform parentRect;
    private Vector2 grabOffset;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (paper == null) return;

        parentRect = paper.parent as RectTransform;
        if (parentRect == null) return;

        // 记录鼠标抓住的位置，避免开始拖动时纸张跳动。
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerPosition))
        {
            grabOffset = (Vector2)paper.localPosition - pointerPosition;
        }

        // 把纸张显示在同一画布其他元素的前面。
        paper.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (paper == null || parentRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerPosition))
        {
            return;
        }

        Vector2 position = pointerPosition + grabOffset;

        // 保留部分标题区在屏幕内，避免整张纸被拖丢。
        Rect bounds = parentRect.rect;
        Rect sheet = paper.rect;

        position.x = Mathf.Clamp(
            position.x,
            bounds.xMin - sheet.xMax + 60f,
            bounds.xMax - sheet.xMin - 60f
        );

        position.y = Mathf.Clamp(
            position.y,
            bounds.yMin - sheet.yMax + 60f,
            bounds.yMax - sheet.yMax
        );

        paper.localPosition = new Vector3(
            position.x,
            position.y,
            paper.localPosition.z
        );
    }
}
