using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(500)]
public class GameMenuController : MonoBehaviour
{
    public GameObject startPanel;
    public GameObject pausePanel;
    private GameObject modePanel;
    private GameObject resultMenu;
    private VoyageMode pendingMode;
    private Text modeDescription;
    private Text modeConfirmation;
    private bool ended;

    private void Awake()
    {
        if (VoyageModeSession.Current == null) gameObject.AddComponent<VoyageModeSession>();
    }

    [Header("Menu text (edit GameMenuDefaults prefab outside Play Mode)")]
    [TextArea(1, 3)] public string gameTitle = "RESIDUAL\nPRESSURE";
    public string startLabel = "START DIVE";
    public string resumeLabel = "RESUME VOYAGE";
    public string restartLabel = "RESTART";
    public string quitLabel = "QUIT GAME";
    [Header("Menu appearance")]
    public Font font;
    [Range(24, 140)] public int titleSize = 68;
    [Range(18, 60)] public int buttonSize = 34;
    public Color textColor = new Color(0.65f, 0.64f, 0.57f);
    public Color selectedColor = new Color(1f, 0.93f, 0.72f);
    public Color accentColor = new Color(0.91f, 0.74f, 0.25f);
    public Vector2 menuAnchorMin = new Vector2(0.065f, 0.12f);
    public Vector2 menuAnchorMax = new Vector2(0.37f, 0.88f);

    [Header("Start menu camera (world coordinates)")]
    public bool useCustomMenuCamera;
    public Vector3 menuCameraPosition;
    public Vector3 menuCameraEulerAngles;
    [Range(20f, 100f)] public float menuCameraFieldOfView = 60f;
    public bool animateMenuCamera = true;
    [Range(0f, 2f)] public float cameraSwayDegrees = 0.3f;
    [Range(0f, 0.1f)] public float cameraSwayDistance = 0.012f;
    [Range(0.05f, 1f)] public float cameraSwaySpeed = 0.12f;
    private Camera menuCamera;
    private Vector3 gameplayCameraPosition;
    private Quaternion gameplayCameraRotation;
    private float gameplayCameraFov;
    private bool cameraCaptured;
    private float cameraClock;
    private readonly System.Collections.Generic.Dictionary<GameObject, bool> startMenuHiddenUI =
        new System.Collections.Generic.Dictionary<GameObject, bool>();

    private void HideGameplayUI()
    {
        foreach (var view in Object.FindObjectsByType<RepairStationView>(FindObjectsInactive.Include))
        {
            if (view.leftButton != null) HideForStartMenu(view.leftButton.gameObject);
            if (view.rightButton != null) HideForStartMenu(view.rightButton.gameObject);
        }
        foreach (var tutorial in Object.FindObjectsByType<TutorialOverlay>(FindObjectsInactive.Include))
        {
            tutorial.HideTutorial();
            HideForStartMenu(tutorial.openTutorialUI);
        }
    }

    private void HideForStartMenu(GameObject target)
    {
        if (target == null || startMenuHiddenUI.ContainsKey(target)) return;
        startMenuHiddenUI.Add(target, target.activeSelf);
        target.SetActive(false);
    }

    private void RestoreGameplayUI()
    {
        foreach (var entry in startMenuHiddenUI)
            if (entry.Key != null) entry.Key.SetActive(entry.Value);
        startMenuHiddenUI.Clear();
    }

#if UNITY_EDITOR
    [ContextMenu("Capture Scene View as Menu Camera")]
    private void CaptureSceneViewCamera()
    {
        var view = UnityEditor.SceneView.lastActiveSceneView;
        if (view == null || view.camera == null) return;
        UnityEditor.Undo.RecordObject(this, "Set menu camera viewpoint");
        menuCameraPosition = view.camera.transform.position;
        menuCameraEulerAngles = view.camera.transform.eulerAngles;
        menuCameraFieldOfView = view.camera.fieldOfView;
        useCustomMenuCamera = true;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void LateUpdate()
    {
        if (started) return;
        if (!cameraCaptured)
        {
            menuCamera = Camera.main;
            if (menuCamera == null) return;
            // Capture after the station scripts have applied their initial driving pose.
            gameplayCameraPosition = menuCamera.transform.position;
            gameplayCameraRotation = menuCamera.transform.rotation;
            gameplayCameraFov = menuCamera.fieldOfView;
            cameraCaptured = true;
        }
        cameraClock += Time.unscaledDeltaTime;
        Vector3 position = useCustomMenuCamera ? menuCameraPosition : gameplayCameraPosition;
        Quaternion rotation = useCustomMenuCamera
            ? Quaternion.Euler(menuCameraEulerAngles) : gameplayCameraRotation;
        if (animateMenuCamera)
        {
            // Speed is cycles per second, rather than radians per second.
            float phase = cameraClock * cameraSwaySpeed * Mathf.PI * 2f;
            position += rotation * new Vector3(Mathf.Sin(phase) * cameraSwayDistance,
                Mathf.Sin(phase * 0.7f) * cameraSwayDistance * 0.5f, 0f);
            rotation *= Quaternion.Euler(Mathf.Sin(phase * 0.7f) * cameraSwayDegrees,
                Mathf.Sin(phase) * cameraSwayDegrees, 0f);
        }
        menuCamera.transform.SetPositionAndRotation(position, rotation);
        menuCamera.fieldOfView = useCustomMenuCamera ? menuCameraFieldOfView : gameplayCameraFov;
    }

    private void RestoreGameplayCamera()
    {
        if (!cameraCaptured || menuCamera == null) return;
        menuCamera.transform.SetPositionAndRotation(gameplayCameraPosition, gameplayCameraRotation);
        menuCamera.fieldOfView = gameplayCameraFov;
        cameraCaptured = false;
    }

    public static bool IsInputBlocked { get; private set; }
    public static bool IsStartMenuVisible { get; private set; }

    private bool started;
    private bool paused;
    private Font menuFont;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterMenus()
    {
        IsInputBlocked = false;
        IsStartMenuVisible = false;
        SceneManager.sceneLoaded -= EnsureMenu;
        SceneManager.sceneLoaded += EnsureMenu;
    }

    // Enter Play Mode can keep scenes loaded, so sceneLoaded alone is insufficient.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureMenuOnPlay()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            EnsureMenu(SceneManager.GetSceneAt(i), LoadSceneMode.Single);
    }

    private static void EnsureMenu(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive || scene.name != "SampleScene") return;
        if (Object.FindAnyObjectByType<GameMenuController>() != null) return;
        var template = Resources.Load<GameObject>("GameMenuDefaults");
        var root = template != null ? Object.Instantiate(template) : new GameObject("GameMenuController");
        root.name = "GameMenuController";
        SceneManager.MoveGameObjectToScene(root, scene);
        if (root.GetComponent<GameMenuController>() == null) root.AddComponent<GameMenuController>();
    }

    private void CreateMissingPanels()
    {
        if (startPanel != null && pausePanel != null) return;
        var root = new GameObject("GameMenuCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        if (EventSystem.current == null)
        {
            var events = new GameObject("MenuEventSystem", typeof(EventSystem));
            events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            events.AddComponent<StandaloneInputModule>();
#endif
        }
        if (startPanel == null) startPanel = BuildPanel(root.transform, false);
        if (pausePanel == null) pausePanel = BuildPanel(root.transform, true);
    }

    private GameObject BuildPanel(Transform parent, bool isPause)
    {
        var panel = new GameObject(isPause ? "PausePanel" : "StartPanel",
            typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.012f, 0.02f, 0.018f, isPause ? 0.3f : 0.16f);
        for (int i = 0; i < 48; i++)
        {
            var strip = new GameObject("Shade", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(panel.transform, false);
            var shade = strip.GetComponent<RectTransform>();
            shade.anchorMin = new Vector2(i / 48f, 0f);
            shade.anchorMax = new Vector2((i + 1f) / 48f, 1f);
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            strip.GetComponent<Image>().color = new Color(0.005f, 0.012f, 0.01f,
                0.86f * (1f - Mathf.SmoothStep(0f, 1f, i / (48f * 0.64f))));
            strip.GetComponent<Image>().raycastTarget = false;
        }
        var content = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup));
        content.transform.SetParent(panel.transform, false);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = menuAnchorMin;
        contentRect.anchorMax = menuAnchorMax;
        contentRect.offsetMin = contentRect.offsetMax = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        AddLabel(content.transform, gameTitle, titleSize, titleSize * 2.6f);
        var rule = new GameObject("TitleRule", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rule.transform.SetParent(content.transform, false);
        rule.GetComponent<Image>().color = new Color(0.66f, 0.57f, 0.31f, 0.85f);
        rule.GetComponent<Image>().raycastTarget = false;
        rule.GetComponent<LayoutElement>().preferredHeight = 2;
        var gap = new GameObject("TitleSpacing", typeof(RectTransform), typeof(LayoutElement));
        gap.transform.SetParent(content.transform, false);
        gap.GetComponent<LayoutElement>().preferredHeight = 54;
        AddButton(content.transform, isPause ? resumeLabel : startLabel,
            isPause ? (UnityEngine.Events.UnityAction)ResumeGame : StartGame);
        if (isPause) AddButton(content.transform, restartLabel, RestartGame);
        AddButton(content.transform, "MODE", ShowModeSelection);
        AddButton(content.transform, quitLabel, QuitGame);
        return panel;
    }

    private void AddLabel(Transform parent, string text, int size, float height)
    {
        var label = new GameObject(text, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        label.transform.SetParent(parent, false);
        var uiText = label.GetComponent<Text>();
        if (menuFont == null)
            menuFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 32);
        uiText.font = font != null ? font : menuFont;
        uiText.text = text;
        uiText.fontSize = size;
        uiText.alignment = TextAnchor.MiddleLeft;
        uiText.fontStyle = size >= 30 ? FontStyle.Bold : FontStyle.Normal;
        uiText.color = selectedColor;
        uiText.raycastTarget = false;
        label.GetComponent<LayoutElement>().preferredHeight = height;
    }

    private void AddButton(Transform parent, string text, UnityEngine.Events.UnityAction action)
    {
        var root = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 70;
        root.GetComponent<Image>().color = Color.clear;
        var button = root.GetComponent<Button>();
        button.onClick.AddListener(action);
        button.transition = Selectable.Transition.None;
        var navigation = button.navigation;
        navigation.mode = Navigation.Mode.Vertical;
        button.navigation = navigation;
        AddLabel(root.transform, text, buttonSize, 70);
        var label = (RectTransform)root.transform.GetChild(0);
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.offsetMin = label.offsetMax = Vector2.zero;
        label.offsetMin = new Vector2(48, 0);
        AddLabel(root.transform, "▶", 28, 70);
        var arrow = (RectTransform)root.transform.GetChild(1);
        arrow.anchorMin = Vector2.zero;
        arrow.anchorMax = new Vector2(0, 1);
        arrow.sizeDelta = new Vector2(38, 0);
        arrow.anchoredPosition = new Vector2(19, 0);
        var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(root.transform, false);
        var line = underline.GetComponent<RectTransform>();
        line.anchorMin = Vector2.zero;
        line.anchorMax = new Vector2(0.82f, 0);
        line.offsetMin = new Vector2(48, 0);
        line.offsetMax = new Vector2(0, 2);
        underline.GetComponent<Image>().color = accentColor;
        underline.GetComponent<Image>().raycastTarget = false;
        var visual = root.AddComponent<SubmarineMenuOptionVisual>();
        visual.label = label.GetComponent<Text>();
        visual.arrow = arrow.gameObject;
        visual.underline = underline;
        visual.normalColor = textColor;
        visual.focusColor = selectedColor;
        visual.accentColor = accentColor;
        visual.Refresh();
    }

    private static void FocusPanel(GameObject panel)
    {
        if (EventSystem.current == null) return;
        var button = panel.GetComponentInChildren<Button>();
        EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null);
    }

    private void Start()
    {
        CreateMissingPanels();
        IsStartMenuVisible = true;
        HideGameplayUI();
        startPanel.SetActive(true);
        pausePanel.SetActive(false);
        SetPaused(true);
        FocusPanel(startPanel);
        if (VoyageModeSession.AutoStartAfterReload)
        {
            VoyageModeSession.AutoStartAfterReload = false;
            StartGame();
        }
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        bool escapePressed =
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif
        if (escapePressed && modePanel != null && modePanel.activeSelf) { CancelModeSelection(); return; }
        if (!started || ended) return;
        if (escapePressed)
        {
            if (paused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void StartGame()
    {
        RestoreGameplayCamera();
        IsStartMenuVisible = false;
        started = true;
        RestoreGameplayUI();
        startPanel.SetActive(false);
        ResumeGame();
        foreach (var tutorial in Object.FindObjectsByType<TutorialOverlay>())
            tutorial.ShowOnFirstVoyage();
    }

    public void PauseGame()
    {
        if (!started) return;

        pausePanel.SetActive(true);
        SetPaused(true);
        FocusPanel(pausePanel);
    }

    public void ResumeGame()
    {
        if (!started || ended || (modePanel != null && modePanel.activeSelf)) return;

        pausePanel.SetActive(false);
        SetPaused(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    public void RestartGame()
    {
        Scene scene = gameObject.scene;

        if (!Application.CanStreamedLevelBeLoaded(scene.path))
        {
            Debug.LogError("Add the current scene to the build scene list before restarting.");
            return;
        }

        IsInputBlocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(scene.path);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Transform MenuCanvas => startPanel.transform.parent;

    private GameObject CreateMenuOverlay(string name, out Transform content)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(MenuCanvas, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.012f, 0.02f, 0.018f, 0.98f);
        var column = new GameObject("Options", typeof(RectTransform), typeof(VerticalLayoutGroup));
        column.transform.SetParent(panel.transform, false);
        var layoutRect = column.GetComponent<RectTransform>();
        layoutRect.anchorMin = new Vector2(0.2f, 0.08f);
        layoutRect.anchorMax = new Vector2(0.8f, 0.92f);
        layoutRect.offsetMin = layoutRect.offsetMax = Vector2.zero;
        var layout = column.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        content = column.transform;
        return panel;
    }

    public void ShowModeSelection()
    {
        pendingMode = VoyageModeSession.Selected;
        if (modePanel == null)
        {
            modePanel = CreateMenuOverlay("ModeSelection", out Transform content);
            AddLabel(content, "SELECT MODE", 40, 65);
            AddLabel(content, "", 24, 135);
            modeDescription = content.GetChild(content.childCount - 1).GetComponent<Text>();
            for (int i = 0; i < 3; i++)
            {
                VoyageMode choice = (VoyageMode)i;
                AddButton(content, VoyageModeSession.Name(choice), () => { pendingMode = choice; RefreshModeSelection(); });
            }
            AddButton(content, "", ConfirmModeSelection);
            modeConfirmation = content.GetChild(content.childCount - 1).GetComponentInChildren<Text>();
            AddButton(content, "CANCEL", CancelModeSelection);
        }
        modePanel.SetActive(true);
        startPanel.SetActive(false);
        pausePanel.SetActive(false);
        if (resultMenu != null) resultMenu.SetActive(false);
        modePanel.transform.SetAsLastSibling();
        RefreshModeSelection();
        var options = modePanel.GetComponentsInChildren<Button>();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(options[(int)pendingMode].gameObject);
    }

    private void RefreshModeSelection()
    {
        var options = modePanel.GetComponentsInChildren<Button>();
        for (int i = 0; i < 3; i++)
            options[i].GetComponent<SubmarineMenuOptionVisual>().SetModeChoice(i == (int)pendingMode);
        string detail = pendingMode == VoyageMode.Easy
            ? "More oxygen, fewer obstacles, and automatic hull recovery."
            : pendingMode == VoyageMode.Hard
            ? "Less oxygen, more obstacles, and more frequent repairs."
            : "Survive as long as you can as the danger grows.";
        modeDescription.text = "SELECTED: " + VoyageModeSession.Name(pendingMode) + "\n" + detail
            + (started ? "\nChanging mode restarts the voyage." : "");
        modeConfirmation.text = started ? "CONFIRM & RESTART" : "CONFIRM";
    }

    private void ConfirmModeSelection()
    {
        VoyageModeSession.Select(pendingMode);
        VoyageModeSession.AutoStartAfterReload = started;
        RestartGame();
    }

    private void CancelModeSelection()
    {
        modePanel.SetActive(false);
        if (ended) resultMenu.SetActive(true);
        else if (started) pausePanel.SetActive(true);
        else startPanel.SetActive(true);
        FocusPanel(ended ? resultMenu : started ? pausePanel : startPanel);
    }

    public void ShowResultMenu(string summary)
    {
        if (resultMenu != null) Destroy(resultMenu);
        ended = true;
        startPanel.SetActive(false);
        pausePanel.SetActive(false);
        if (modePanel != null) modePanel.SetActive(false);
        resultMenu = CreateMenuOverlay("VoyageResults", out Transform content);
        AddLabel(content, summary, 28, 400);
        AddButton(content, "RETRY", () => { VoyageModeSession.AutoStartAfterReload = true; RestartGame(); });
        AddButton(content, "SELECT MODE", ShowModeSelection);
        AddButton(content, quitLabel, QuitGame);
        SetPaused(true);
        FocusPanel(resultMenu);
    }

    private void SetPaused(bool value)
    {
        paused = value;
        IsInputBlocked = value;

        Time.timeScale = value ? 0f : 1f;
        AudioListener.pause = value;
    }

    private void OnDestroy()
    {
        if (menuFont != null) Destroy(menuFont);
        RestoreGameplayUI();
        RestoreGameplayCamera();
        IsStartMenuVisible = false;
        IsInputBlocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}

public sealed class SubmarineMenuOptionVisual : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public Text label;
    public GameObject arrow;
    public GameObject underline;
    public Color normalColor;
    public Color focusColor;
    public Color accentColor;
    private bool selected;
    private bool modeOption;
    private bool chosen;

    public void SetModeChoice(bool value)
    {
        modeOption = true;
        chosen = value;
        Refresh();
    }

    private void OnEnable() { Refresh(); }
    private void OnDisable() { selected = false; }
    public void OnPointerEnter(PointerEventData data)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
    }
    public void OnPointerExit(PointerEventData data) { }
    public void OnSelect(BaseEventData data) { selected = true; Refresh(); }
    public void OnDeselect(BaseEventData data) { selected = false; Refresh(); }
    public void Refresh()
    {
        if (label != null) label.color = (selected || chosen)
            ? focusColor : normalColor;
        if (arrow != null)
        {
            arrow.SetActive(modeOption ? chosen : selected);
            arrow.GetComponent<Text>().color = accentColor;
        }
        if (underline != null) underline.SetActive(selected);
    }
}
