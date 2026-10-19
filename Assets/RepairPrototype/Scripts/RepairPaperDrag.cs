using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class RepairPaperDrag : MonoBehaviour,
    IBeginDragHandler, IDragHandler
{
    [Header("需要移动的纸张")]
    public RectTransform paper;

    private RectTransform parentRect;
    private Vector2 grabOffset;

    private void Awake()
    {
        if (paper == null) paper = transform as RectTransform;
        if (paper == null) return;

        GameObject hint = new GameObject("CloseHint", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        hint.transform.SetParent(paper, false);
        RectTransform rect = (RectTransform)hint.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 8f);
        rect.sizeDelta = new Vector2(0f, 36f);

        Text label = hint.GetComponent<Text>();
        label.text = "Right-click to close repair sheet";
        label.font = Resources.Load<Font>("MenuFont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 24;
        label.lineSpacing = 1.15f;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.95f, 0.93f, 0.82f);
        label.raycastTarget = false;
        hint.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
    }
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (GameMenuController.IsInputBlocked || eventData.button != PointerEventData.InputButton.Left) return;
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
        if (GameMenuController.IsInputBlocked || eventData.button != PointerEventData.InputButton.Left) return;
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
            bounds.yMax - sheet.yMax - 44f
        );

        paper.localPosition = new Vector3(
            position.x,
            position.y,
            paper.localPosition.z
        );
    }
}
