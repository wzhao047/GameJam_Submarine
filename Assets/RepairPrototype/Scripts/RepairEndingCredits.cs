using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Builds the ending roll at runtime; edit the EndingCreditsDefaults prefab for presentation.</summary>
public sealed class RepairEndingCredits : MonoBehaviour
{
    [Header("Content")]
    [TextArea(1, 3)] public string gameTitle = "RESIDUAL\nPRESSURE";
    public string voyageHeading = "VOYAGE LOG";
    public string teamHeading = "THE CREW";
    [TextArea(3, 12)] public string teamCredits = "GAME DESIGNERS\nMarius FU\nWeiting Z\n\nPROGRAMMERS\nMarius FU\nWeiting Z\n\nUI DESIGNERS\nMarius FU\nWeiting Z\nWu Yang\n\n3D & ENVIRONMENT ARTISTS\nWeiting Z\nZhengbo Wang\n\nSOUND DESIGN\nMarius FU\n\nSpecial thanks to everyone who contributed to development, playtesting, and support.";
    public string thanksMessage = "THANK YOU FOR PLAYING!";
    public string thanksSubtitle = "RESIDUAL PRESSURE";
    public string restartLabel = "RESTART VOYAGE";
    public string playingHint = "DOUBLE-CLICK ANYWHERE TO SKIP";
    public string finishedHint = "SCROLL TO REVIEW YOUR VOYAGE";

    [Header("Ending messages")]
    [TextArea(1, 3)] public string destinationReachedMessage = "DESTINATION REACHED";
    [TextArea(1, 3)] public string hullDestroyedMessage = "HULL DESTROYED - THREE COLLISIONS";
    [TextArea(1, 3)] public string oxygenDepletedMessage = "OXYGEN DEPLETED";
    [TextArea(1, 3)] public string floodingLimitReachedMessage = "FLOODING LIMIT REACHED";
    [Header("Timing (unscaled seconds / canvas units per second)")]
    [Min(1f)] public float scrollSpeed = 42f;
    [Min(0f)] public float openingHold = 2.5f;
    [Min(0f)] public float endingHold = 1.5f;
    [Range(0.15f, 0.6f)] public float doubleClickWindow = 0.35f;

    [Header("Appearance")]
    public Font font;
    public Color backgroundColor = new Color(0.025f, 0.047f, 0.063f, 1f);
    public Color titleColor = new Color(1f, 0.93f, 0.72f, 1f);
    public Color bodyColor = new Color(0.78f, 0.81f, 0.79f, 1f);
    public Color accentColor = new Color(0.78f, 0.65f, 0.36f, 1f);
    public Color failureColor = new Color(0.86f, 0.47f, 0.38f, 1f);
    [Header("Optional ending artwork (uses a drawn emblem when empty)")]
    public Sprite successIcon;
    public Sprite hullIcon;
    public Sprite oxygenIcon;
    public Sprite floodingIcon;

    public static bool IsVisible { get; private set; }
    public bool IsComplete { get; private set; }
    public ScrollRect Roll { get; private set; }
    public Button RestartButton { get; private set; }

    private RectTransform content;
    private RectTransform viewport;
    private Text hint;
    private Font displayFont;
    private float elapsed;
    private float bottomElapsed;
    private float lastClickTime = float.NegativeInfinity;
    private bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetVisibility() { IsVisible = false; }

    public void Show(bool success, string cause, string report, UnityAction restart)
    {
        if (ready) return;
        IsVisible = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        displayFont = font != null ? font : Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 32);
        if (displayFont == null) displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureEventSystem();
        BuildCanvas();
        BuildRoll(success, cause, report);
        BuildFooter(restart);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Roll.verticalNormalizedPosition = 1f;
        Roll.enabled = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        ready = true;
    }

    private void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        var backdrop = NewRect("Deep sea", transform);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = backgroundColor;

        var scrollRoot = NewRect("Ending roll", transform);
        scrollRoot.anchorMin = new Vector2(0.12f, 0f);
        scrollRoot.anchorMax = new Vector2(0.88f, 1f);
        scrollRoot.offsetMin = new Vector2(0f, 170f);
        scrollRoot.offsetMax = new Vector2(0f, -60f);
        Roll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        Roll.horizontal = false;
        Roll.vertical = true;
        Roll.movementType = ScrollRect.MovementType.Clamped;
        Roll.inertia = false;
        Roll.scrollSensitivity = 65f;
        viewport = NewRect("Viewport", scrollRoot);
        Stretch(viewport);
        var hitArea = viewport.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        Roll.viewport = viewport;
        content = NewRect("Credits content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 45, 120);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Roll.content = content;
    }

    private void BuildRoll(bool success, string cause, string report)
    {
        Label("Game title", gameTitle, 76, titleColor, FontStyle.Bold);
        Gap(24f);
        var emblemRow = NewRect("Ending emblem", content);
        emblemRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 156f;
        var emblem = NewRect("Emblem", emblemRow);
        emblem.anchorMin = emblem.anchorMax = new Vector2(0.5f, 0.5f);
        emblem.sizeDelta = new Vector2(148f, 148f);
        Color endingColor = success ? accentColor : failureColor;
        int ending = success ? 0 : cause.Contains("HULL") ? 1 : cause.Contains("OXYGEN") ? 2 : 3;
        Sprite artwork = ending == 0 ? successIcon : ending == 1 ? hullIcon : ending == 2 ? oxygenIcon : floodingIcon;
        if (artwork != null)
        {
            var image = emblem.gameObject.AddComponent<Image>();
            image.sprite = artwork;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        else
        {
            var graphic = emblem.gameObject.AddComponent<RepairEndingEmblem>();
            graphic.Ending = ending;
            graphic.color = endingColor;
            graphic.raycastTarget = false;
        }
        Label("Outcome", success ? "VOYAGE COMPLETE" : "VOYAGE FAILED", 38, endingColor, FontStyle.Bold);
        // Resolve artwork from the original cause, independently of editable display text.
        string endingMessage = ending switch
        {
            0 => destinationReachedMessage,
            1 => hullDestroyedMessage,
            2 => oxygenDepletedMessage,
            _ => floodingLimitReachedMessage
        };
        Label("Cause", endingMessage, 24, bodyColor);
        Gap(90f);
        Rule();
        Label("Log heading", voyageHeading, 24, accentColor, FontStyle.Bold);
        Gap(22f);
        Label("Voyage statistics", report, 30, bodyColor);
        Gap(120f);
        Rule();
        Label("Crew heading", teamHeading, 30, accentColor, FontStyle.Bold);
        Gap(30f);
        Label("Team credits", teamCredits, 30, bodyColor);
        Gap(140f);
        Label("Thank you", thanksMessage, 52, titleColor, FontStyle.Bold);
        Label("Thank you subtitle", thanksSubtitle, 22, accentColor);
        Gap(30f);
    }

    private void BuildFooter(UnityAction restart)
    {
        var footer = NewRect("Footer", transform);
        footer.anchorMin = Vector2.zero;
        footer.anchorMax = new Vector2(1f, 0f);
        footer.pivot = new Vector2(0.5f, 0f);
        footer.sizeDelta = new Vector2(0f, 155f);
        hint = Label("Playback hint", playingHint, 20, bodyColor, FontStyle.Normal, footer);
        var hintRect = (RectTransform)hint.transform;
        hintRect.anchorMin = new Vector2(0.05f, 1f);
        hintRect.anchorMax = new Vector2(0.95f, 1f);
        hintRect.pivot = new Vector2(0.5f, 1f);
        hintRect.sizeDelta = new Vector2(0f, 42f);
        var buttonRect = NewRect("Restart voyage", footer);
        buttonRect.anchorMin = new Vector2(0.25f, 0f);
        buttonRect.anchorMax = new Vector2(0.75f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(0f, 22f);
        buttonRect.sizeDelta = new Vector2(0f, 68f);
        var background = buttonRect.gameObject.AddComponent<Image>();
        background.color = new Color(0.16f, 0.18f, 0.16f, 1f);
        var outline = buttonRect.gameObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = Vector2.one;
        RestartButton = buttonRect.gameObject.AddComponent<Button>();
        RestartButton.targetGraphic = background;
        var colors = RestartButton.colors;
        colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.65f, 0.7f, 0.65f, 1f);
        RestartButton.colors = colors;
        RestartButton.onClick.AddListener(restart);
        Text label = Label("Restart label", restartLabel, 26, titleColor, FontStyle.Bold, buttonRect);
        Stretch((RectTransform)label.transform);
        RestartButton.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!ready || IsComplete) return;
        elapsed += Time.unscaledDeltaTime;
        // Poll the whole screen: the second click can be over any credit or empty space.
        bool pressed;
#if ENABLE_INPUT_SYSTEM
        pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
#else
        pressed = Input.GetMouseButtonDown(0);
#endif
        if (pressed && elapsed > 0.15f)
        {
            if (Time.unscaledTime - lastClickTime <= doubleClickWindow)
            {
                Complete();
                return;
            }
            lastClickTime = Time.unscaledTime;
        }
        if (elapsed < openingHold) return;
        float maxOffset = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        float offset = Mathf.Min(maxOffset, content.anchoredPosition.y + Mathf.Max(1f, scrollSpeed) * Time.unscaledDeltaTime);
        content.anchoredPosition = new Vector2(0f, offset);
        if (offset >= maxOffset)
        {
            bottomElapsed += Time.unscaledDeltaTime;
            if (bottomElapsed >= endingHold) Complete();
        }
    }

    public void Complete()
    {
        if (!ready || IsComplete) return;
        IsComplete = true;
        Roll.enabled = true;
        Roll.StopMovement();
        Canvas.ForceUpdateCanvases();
        Roll.verticalNormalizedPosition = 0f;
        hint.text = finishedHint;
        RestartButton.gameObject.SetActive(true);
        // Leave focus empty so the second click of a skip cannot activate Restart.
    }

    private Text Label(string objectName, string value, int size, Color color,
        FontStyle style = FontStyle.Normal, Transform parent = null)
    {
        var rect = NewRect(objectName, parent != null ? parent : content);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = displayFont;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.lineSpacing = 1.3f;
        label.supportRichText = false;
        label.raycastTarget = false;
        return label;
    }

    private void Gap(float height)
    {
        NewRect("Breathing space", content).gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private void Rule()
    {
        var row = NewRect("Section rule", content);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
        var line = NewRect("Line", row);
        line.anchorMin = new Vector2(0.32f, 0.5f);
        line.anchorMax = new Vector2(0.68f, 0.5f);
        line.sizeDelta = new Vector2(0f, 1f);
        var image = line.gameObject.AddComponent<Image>();
        image.color = accentColor;
        image.raycastTarget = false;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var events = new GameObject("Ending EventSystem", typeof(EventSystem));
        events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
        events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        events.AddComponent<StandaloneInputModule>();
#endif
    }

    private void OnDestroy() { if (ready) IsVisible = false; }
}
