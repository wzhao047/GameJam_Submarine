using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SubmarineGoalMarker : MonoBehaviour
{
    [Header("已有系统")]
    public SubmarineController controller;
    public SubmarineSonar sonar;

    [Header("终点的虚拟坐标")]
    public float goalLateral = 0f;
    public float goalForward = 1500f;
    public float goalDepth = 100f;

    [Header("自定义图片：留空使用默认图案")]
    public Texture2D directionImage;
    public Texture2D targetImage;

    [Header("标记外观")]
    public Color markerColor =
        new Color(1f, 0.75f, 0.25f);

    public float directionSize = 24f;
    public float targetSize = 24f;
    public float imageRotationOffset = 0f;

    [Header("缓慢明暗变化")]
    public bool pulse = true;
    public float pulsePeriod = 2.5f;

    [Range(0.1f, 1f)]
    public float minimumBrightness = 0.6f;

    [Header("升降提示")]
    public bool showDepthCue = true;

    [Tooltip("拖入 RepairSystem 上的结算组件，保持深度规则一致")]
    public RepairVoyageResult voyageResult;

    [Tooltip("未连接结算组件时使用的深度容差")]
    [Min(0.1f)]
    public float depthCueTolerance = 10f;

    public float depthCueFontSize = 18f;

    [Tooltip("可以留空，使用 TMP 默认字体")]
    public TMP_FontAsset depthCueFont;

    private RectTransform screenRect;
    private RectTransform markerRect;
    private RawImage markerImage;

    private TextMeshProUGUI depthCue;
    private GameObject depthCueObject;

    private Texture2D defaultDirection;
    private Texture2D defaultTarget;

    public Vector3 GoalPosition => new Vector3(
        goalLateral,
        goalDepth,
        goalForward
    );

    private void Awake()
    {
        screenRect = GetComponent<RectTransform>();

        defaultDirection = CreateTexture(true);
        defaultTarget = CreateTexture(false);
    }

    private void Start()
    {
        if (screenRect == null ||
            controller == null ||
            sonar == null)
        {
            Debug.LogError(
                "请把脚本挂在 SonarScreen 上，并连接 Controller 和 Sonar。",
                this
            );

            enabled = false;
            return;
        }

        CreateMarker();
        CreateDepthCue();
    }

    private void CreateMarker()
    {
        GameObject marker = new GameObject(
            "GoalMarker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage)
        );

        marker.transform.SetParent(transform, false);

        markerRect = marker.GetComponent<RectTransform>();

        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);

        markerRect.localScale = Vector3.one;
        markerRect.localRotation = Quaternion.identity;
        markerRect.localPosition = Vector3.zero;

        markerImage = marker.GetComponent<RawImage>();
        markerImage.raycastTarget = false;
    }

    private void CreateDepthCue()
    {
        depthCueObject = new GameObject(
            "GoalDepthCue",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );

        depthCueObject.transform.SetParent(transform, false);

        RectTransform rect =
            depthCueObject.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.localPosition = Vector3.zero;

        depthCue =
            depthCueObject.GetComponent<TextMeshProUGUI>();

        TMP_FontAsset font = depthCueFont != null
            ? depthCueFont
            : TMP_Settings.defaultFontAsset;

        if (font != null)
            depthCue.font = font;

        depthCue.alignment = TextAlignmentOptions.Center;
        depthCue.enableAutoSizing = false;
        depthCue.raycastTarget = false;
        depthCue.text = "";
    }

    private void LateUpdate()
    {
        if (markerImage == null)
            return;

        bool visible =
            sonar != null &&
            (VoyageModeSession.Current == null || VoyageModeSession.Current.Mode != VoyageMode.Endless) &&
            sonar.sonarScreen != null &&
            sonar.sonarScreen.isActiveAndEnabled;

        markerImage.enabled = visible;

        if (depthCue != null)
            depthCue.enabled = visible && showDepthCue;

        if (!visible)
            return;

        UpdateGoalMarker();
        UpdateDepthCue();
    }

    private void UpdateGoalMarker()
    {
        Vector2 offset = new Vector2(
            goalLateral,
            goalForward
        ) - controller.NavigationPosition;

        float radians =
            controller.Heading * Mathf.Deg2Rad;

        Vector2 relative = new Vector2(
            offset.x * Mathf.Cos(radians)
                - offset.y * Mathf.Sin(radians),

            offset.x * Mathf.Sin(radians)
                + offset.y * Mathf.Cos(radians)
        );

        float range = Mathf.Max(1f, sonar.detectionRange);
        bool outsideRange = relative.magnitude > range;

        float screenSize = Mathf.Min(
            screenRect.rect.width,
            screenRect.rect.height
        );

        float radarRadius = screenSize * 0.485f;

        float size = outsideRange
            ? directionSize
            : targetSize;

        size = Mathf.Clamp(
            size,
            4f,
            screenSize * 0.2f
        );

        float maximumRadius = Mathf.Max(
            0f,
            radarRadius - size * 0.5f - 2f
        );

        if (outsideRange)
        {
            markerRect.anchoredPosition =
                relative.normalized * maximumRadius;

            float angle = Mathf.Atan2(
                relative.x,
                relative.y
            ) * Mathf.Rad2Deg;

            markerRect.localRotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    -angle + imageRotationOffset
                );

            markerImage.texture = directionImage != null
                ? directionImage
                : defaultDirection;
        }
        else
        {
            markerRect.anchoredPosition =
                Vector2.ClampMagnitude(
                    relative / range * radarRadius,
                    maximumRadius
                );

            markerRect.localRotation =
                Quaternion.identity;

            markerImage.texture = targetImage != null
                ? targetImage
                : defaultTarget;
        }

        markerRect.sizeDelta =
            new Vector2(size, size);

        float brightness = 1f;

        if (pulse)
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(
                Time.time * Mathf.PI * 2f
                / Mathf.Max(0.2f, pulsePeriod)
            );

            brightness = Mathf.Lerp(
                minimumBrightness,
                1f,
                wave
            );
        }

        Color color = markerColor;
        color.a *= brightness;

        markerImage.color = color;
    }

    private void UpdateDepthCue()
    {
        if (depthCue == null || !showDepthCue)
            return;

        RectTransform rect = depthCue.rectTransform;

        rect.anchoredPosition = new Vector2(
            0f,
            -screenRect.rect.height * 0.33f
        );

        rect.sizeDelta = new Vector2(
            screenRect.rect.width * 0.6f,
            screenRect.rect.height * 0.12f
        );

        depthCue.fontSize = depthCueFontSize;
        depthCue.color = markerColor;

        float tolerance = voyageResult != null
            ? voyageResult.arrivalDepthTolerance
            : depthCueTolerance;

        tolerance = Mathf.Max(0.1f, tolerance);

        float difference = goalDepth - controller.Depth;

        string message;

        if (difference > tolerance)
        {
            message = "DIVE";
        }
        else if (difference < -tolerance)
        {
            message = "RISE";
        }
        else
        {
            message = "";
        }

        if (depthCue.text != message)
            depthCue.text = message;
    }

    private Texture2D CreateTexture(bool directional)
    {
        const int resolution = 64;

        Texture2D texture = new Texture2D(
            resolution,
            resolution,
            TextureFormat.RGBA32,
            false
        );

        texture.name = directional
            ? "Retro Bearing Mark"
            : "Retro Target Ring";

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels =
            new Color32[resolution * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float px =
                    (x + 0.5f) / resolution * 2f - 1f;

                float py =
                    (y + 0.5f) / resolution * 2f - 1f;

                bool filled;

                if (directional)
                {
                    float dotY = py - 0.55f;

                    bool dot =
                        px * px + dotY * dotY
                        < 0.16f * 0.16f;

                    bool firstTick =
                        Mathf.Abs(px) < 0.32f &&
                        Mathf.Abs(py - 0.02f) < 0.055f;

                    bool secondTick =
                        Mathf.Abs(px) < 0.48f &&
                        Mathf.Abs(py + 0.42f) < 0.055f;

                    filled =
                        dot || firstTick || secondTick;
                }
                else
                {
                    float distance = Mathf.Sqrt(
                        px * px + py * py
                    );

                    bool ring =
                        distance > 0.48f &&
                        distance < 0.68f;

                    bool centerDot = distance < 0.12f;

                    filled = ring || centerDot;
                }

                pixels[y * resolution + x] = filled
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return texture;
    }

    private void OnDisable()
    {
        if (markerImage != null)
            markerImage.enabled = false;

        if (depthCue != null)
            depthCue.enabled = false;
    }

    private void OnDestroy()
    {
        if (markerRect != null)
            Destroy(markerRect.gameObject);

        if (depthCueObject != null)
            Destroy(depthCueObject);

        if (defaultDirection != null)
            Destroy(defaultDirection);

        if (defaultTarget != null)
            Destroy(defaultTarget);
    }
}
