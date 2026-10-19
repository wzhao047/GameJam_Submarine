using UnityEngine;
using UnityEngine.UI;

public class SubmarineSonar : MonoBehaviour
{
    [Header("声呐屏幕")]
    public RawImage sonarScreen;

    [Header("海洋系统")]
    public RepairOceanDirector ocean;
    public FusePuzzleInteraction fuse;

    [Header("扫描设置")]
    public float detectionRange = 200f;
    public float scanPeriod = 3f;
    public float echoDuration = 2.5f;

    [Header("Contact colors")]
    public Color fishColor = new Color(0.65f, 1f, 0.55f);
    public Color rockColor = new Color(0.15f, 0.7f, 0.3f);
    public Color oxygenCanisterColor = new Color(0.1f, 0.8f, 1f);

    [Header("大型礁体外观")]
    public Color reefFillColor =
        new Color(0.35f, 0.025f, 0.015f);

    public Color reefOutlineColor =
        new Color(1f, 0.12f, 0.035f);

    [Range(1f, 8f)]
    public float reefOutlineWidth = 3f;

    [Range(0f, 1f)]
    public float reefFillOpacity = 0.6f;

    [Header("鱼群回波")]
    public bool animateFishEchoes = true;

    [Range(0f, 3f)]
    public float fishTrailSeconds = 1.5f;

    [Range(2, 12)]
    public int fishTrailSamples = 8;

    [Header("礁石深度风险显示")]
    public bool fadeSafeObstacles = true;

    [Tooltip("潜艇与障碍深度错开后，额外保留的安全距离")]
    public float depthSafetyMargin = 3f;

    [Tooltip("由危险亮度过渡到安全亮度所需的深度距离")]
    public float depthFadeBand = 8f;

    [Range(0f, 1f)]
    public float safeObstacleBrightness = 0.12f;

    [Tooltip("亮度变化速度，越大变化越快")]
    public float depthFadeSpeed = 2f;

    [Header("旧版障碍参数：保留兼容")]
    public float obstacleLateral;
    public float obstacleForward = 160f;
    public float obstacleDepth = 100f;
    public float obstacleRadius = 12f;

    private SubmarineController controller;
    private SubmarinePower power;
    private SubmarineHull hull;

    private const int Resolution = 192;
    private const float RefreshInterval = 0.05f;

    private Texture2D screenTexture;
    private Color32[] pixels;
    private Color[] background;
    private float[] pixelAngles;
    private float[] pixelRadii;

    private Echo[] echoes;
    private float scanAngle;
    private float refreshTimer;
    private bool lastWorking;

    public event System.Action ScanCompleted;

    public bool IsWorking =>
        isActiveAndEnabled &&
        power != null &&
        power.SonarWorking &&
        (fuse == null || fuse.CircuitWorking);

    public bool HasContact { get; private set; }
    public float ContactRange { get; private set; }
    public float ContactDepthDifference { get; private set; }

    private class Echo
    {
        public RepairOceanContact source;

        public Vector3 sampledPosition;
        public Vector3 sampledVelocity;

        public float sampledRadius;
        public float sampledTopDepth;
        public float sampledBottomDepth;

        public RepairContactKind kind;

        public float age = float.PositiveInfinity;
        public float depthBrightness = 1f;
    }

    private void Awake()
    {
        controller = GetComponent<SubmarineController>();
        power = GetComponent<SubmarinePower>();
        hull = GetComponent<SubmarineHull>();

        CreateScreen();
    }

    private void Start()
    {
        if (sonarScreen == null ||
            controller == null ||
            power == null ||
            ocean == null)
        {
            Debug.LogError(
                "请将声呐脚本挂到 Submarine，并连接屏幕和 Ocean。",
                this
            );

            enabled = false;
            return;
        }

        sonarScreen.texture = screenTexture;
        sonarScreen.color = Color.white;
        sonarScreen.raycastTarget = false;
    }

    private void CreateScreen()
    {
        screenTexture = new Texture2D(
            Resolution, Resolution,
            TextureFormat.RGBA32, false
        );

        screenTexture.name = "Runtime Multi Contact Sonar";
        screenTexture.filterMode = FilterMode.Bilinear;
        screenTexture.wrapMode = TextureWrapMode.Clamp;

        int count = Resolution * Resolution;

        pixels = new Color32[count];
        background = new Color[count];
        pixelAngles = new float[count];
        pixelRadii = new float[count];

        float center = (Resolution - 1) * 0.5f;
        float radius = center - 2f;

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                int index = y * Resolution + x;

                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                pixelRadii[index] = distance;

                pixelAngles[index] = Mathf.Repeat(
                    Mathf.Atan2(dx, dy) * Mathf.Rad2Deg,
                    360f
                );

                Color color =
                    new Color(0.008f, 0.018f, 0.014f);

                if (distance <= 1f)
                {
                    color = new Color(0.01f, 0.055f, 0.03f);

                    bool ring =
                        Mathf.Abs(distance - 0.25f) < 0.008f ||
                        Mathf.Abs(distance - 0.50f) < 0.008f ||
                        Mathf.Abs(distance - 0.75f) < 0.008f;

                    bool cross =
                        Mathf.Abs(dx) < 0.006f ||
                        Mathf.Abs(dy) < 0.006f;

                    if (ring || cross)
                        color = new Color(0.025f, 0.17f, 0.085f);

                    if (distance > 0.98f)
                        color = new Color(0.08f, 0.5f, 0.25f);
                }

                background[index] = color;
            }
        }
    }

    private void BuildEchoes()
    {
        int count =
            ocean.Rocks.Length +
            ocean.Fish.Length +
            ocean.Reefs.Length +
            ocean.OxygenCanisters.Length;

        echoes = new Echo[count];

        int index = 0;

        AddPool(ocean.Rocks, ref index);
        AddPool(ocean.Fish, ref index);
        AddPool(ocean.Reefs, ref index);
        AddPool(ocean.OxygenCanisters, ref index);
    }

    private void AddPool(
        RepairOceanContact[] pool,
        ref int index)
    {
        foreach (RepairOceanContact item in pool)
        {
            echoes[index++] = new Echo
            {
                source = item
            };
        }
    }

    private void Update()
    {
        if (controller == null || power == null || ocean == null)
            return;

        if (echoes == null && ocean.Ready)
            BuildEchoes();

        bool working = IsWorking;
        // Animate the title-screen display while voyage simulation stays paused.
        // The in-game pause menu still freezes the sweep and its echoes.
        float dt = GameMenuController.IsStartMenuVisible ? Time.unscaledDeltaTime : Time.deltaTime;
        float previousAngle = scanAngle;

        float sweep = working
            ? 360f / Mathf.Max(0.2f, scanPeriod) * dt
            : 0f;

        if (working)
        {
            scanAngle = Mathf.Repeat(scanAngle + sweep, 360f);
            // One shared sweep clock drives the screen and its ping, including after
            // the title screen has advanced the animation while gameplay was paused.
            if (sweep > 0f && previousAngle + sweep >= 360f)
                ScanCompleted?.Invoke();
        }

        HasContact = false;
        ContactRange = 0f;
        ContactDepthDifference = 0f;

        float nearestRange = float.PositiveInfinity;
        float range = Mathf.Max(1f, detectionRange);

        if (echoes != null)
        {
            foreach (Echo echo in echoes)
            {
                echo.age += dt;

                RepairOceanContact item = echo.source;

                if (!working ||
                    item == null ||
                    !item.isActiveAndEnabled ||
                    !item.Active)
                {
                    echo.age = float.PositiveInfinity;
                    continue;
                }

                Vector2 offset = new Vector2(
                    item.position.x,
                    item.position.z
                ) - controller.NavigationPosition;

                Vector2 relative = ToLocal(offset);

                float angle = Mathf.Repeat(
                    Mathf.Atan2(relative.x, relative.y)
                        * Mathf.Rad2Deg,
                    360f
                );

                float angleFromStart = Mathf.Repeat(
                    angle - previousAngle,
                    360f
                );

                bool swept =
                    sweep >= 360f || angleFromStart <= sweep;

                float visibleRange = range;

                if (item.kind == RepairContactKind.Reef)
                    visibleRange += item.radius;

                if (relative.sqrMagnitude <=
                    visibleRange * visibleRange && swept)
                {
                    bool firstEcho = float.IsPositiveInfinity(
                        echo.age
                    );

                    echo.sampledPosition = item.position;
                    echo.sampledVelocity = item.velocity;
                    echo.sampledRadius = item.radius;
                    echo.sampledTopDepth = item.topDepth;
                    echo.sampledBottomDepth = item.bottomDepth;
                    echo.kind = item.kind;
                    echo.age = 0f;

                    if (firstEcho)
                        echo.depthBrightness = GetDepthBrightness(echo);
                }

                if (echo.age >= Mathf.Max(0.1f, echoDuration))
                    continue;

                echo.depthBrightness = Mathf.MoveTowards(
                    echo.depthBrightness,
                    GetDepthBrightness(echo),
                    Mathf.Max(0.01f, depthFadeSpeed) * dt
                );

                Vector3 contactPosition =
                    GetEchoPosition(echo, 0f);

                Vector2 contactOffset = new Vector2(
                    contactPosition.x,
                    contactPosition.z
                ) - controller.NavigationPosition;

                float depthDifference =
                    contactPosition.y - controller.Depth;

                if (echo.kind == RepairContactKind.Reef)
                {
                    float top = Mathf.Min(
                        echo.sampledTopDepth,
                        echo.sampledBottomDepth
                    );

                    float bottom = Mathf.Max(
                        echo.sampledTopDepth,
                        echo.sampledBottomDepth
                    );

                    depthDifference = Mathf.Clamp(
                        controller.Depth, top, bottom
                    ) - controller.Depth;
                }

                float distance = Mathf.Sqrt(
                    contactOffset.sqrMagnitude +
                    depthDifference * depthDifference
                );

                if (distance < nearestRange)
                {
                    nearestRange = distance;
                    ContactRange = distance;
                    ContactDepthDifference = depthDifference;
                    HasContact = true;
                }
            }
        }

        refreshTimer += dt;

        if (refreshTimer >= RefreshInterval ||
            working != lastWorking)
        {
            refreshTimer = 0f;
            RenderScreen(working);
        }

        lastWorking = working;
    }

    private float GetDepthBrightness(Echo echo)
    {
        if (!fadeSafeObstacles ||
            echo.kind == RepairContactKind.Fish ||
            echo.kind == RepairContactKind.OxygenCanister || echo.kind == RepairContactKind.Battery)
        {
            return 1f;
        }

        float boatRadius = hull != null
            ? Mathf.Max(0f, hull.submarineRadius)
            : 3f;

        float padding =
            boatRadius + Mathf.Max(0f, depthSafetyMargin);

        float unsafeTop;
        float unsafeBottom;

        if (echo.kind == RepairContactKind.Reef)
        {
            // 大型礁体使用从顶部到海底的深度范围。
            unsafeTop = Mathf.Min(
                echo.sampledTopDepth,
                echo.sampledBottomDepth
            ) - padding;

            unsafeBottom = Mathf.Max(
                echo.sampledTopDepth,
                echo.sampledBottomDepth
            ) + padding;
        }
        else
        {
            // 普通石块使用中心深度和球形半径。
            float radius = Mathf.Max(0f, echo.sampledRadius);

            unsafeTop =
                echo.sampledPosition.y - radius - padding;

            unsafeBottom =
                echo.sampledPosition.y + radius + padding;
        }

        float depth = controller.Depth;

        float clearance = Mathf.Max(
            unsafeTop - depth,
            depth - unsafeBottom,
            0f
        );

        float safety = Mathf.Clamp01(
            clearance / Mathf.Max(0.1f, depthFadeBand)
        );

        return Mathf.Lerp(
            1f,
            Mathf.Clamp01(safeObstacleBrightness),
            safety
        );
    }

    private Vector2 ToLocal(Vector2 offset)
    {
        float radians = controller.Heading * Mathf.Deg2Rad;

        return new Vector2(
            offset.x * Mathf.Cos(radians)
                - offset.y * Mathf.Sin(radians),

            offset.x * Mathf.Sin(radians)
                + offset.y * Mathf.Cos(radians)
        );
    }

    private Vector3 GetEchoPosition(
        Echo echo,
        float historySeconds)
    {
        Vector3 position = echo.sampledPosition;

        if (animateFishEchoes &&
            echo.kind == RepairContactKind.Fish)
        {
            float predictionTime = Mathf.Max(
                0f, echo.age - historySeconds
            );

            position += echo.sampledVelocity * predictionTime;
        }

        return position;
    }

    private void RenderScreen(bool working)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = background[i];

            if (!working)
            {
                pixels[i] = new Color(0.005f, 0.012f, 0.008f);
                continue;
            }

            if (pixelRadii[i] <= 1f)
            {
                float behind = Mathf.Repeat(
                    scanAngle - pixelAngles[i], 360f
                );

                if (behind < 40f)
                {
                    color += new Color(0.02f, 0.22f, 0.08f)
                        * (1f - behind / 40f);
                }

                if (Mathf.Abs(Mathf.DeltaAngle(
                    scanAngle, pixelAngles[i])) < 1.2f)
                {
                    color += new Color(0.12f, 0.65f, 0.3f);
                }
            }

            pixels[i] = color;
        }

        if (working && echoes != null)
        {
            foreach (Echo echo in echoes)
            {
                if (echo.kind == RepairContactKind.Reef)
                    DrawEcho(echo);
            }

            foreach (Echo echo in echoes)
            {
                if (echo.kind == RepairContactKind.Reef)
                    continue;

                if (animateFishEchoes &&
                    echo.kind == RepairContactKind.Fish &&
                    echo.age < Mathf.Max(0.1f, echoDuration))
                {
                    DrawFishTrail(echo);
                }

                DrawEcho(echo);
            }
        }

        if (working)
            DrawSubmarineMarker();

        screenTexture.SetPixels32(pixels);
        screenTexture.Apply(false, false);
    }

    private void DrawFishTrail(Echo echo)
    {
        int samples = Mathf.Clamp(fishTrailSamples, 2, 12);

        float history = Mathf.Min(
            Mathf.Max(0f, fishTrailSeconds), echo.age
        );

        if (history <= 0.01f)
            return;

        for (int step = samples; step >= 1; step--)
        {
            float fraction = step / (float)samples;

            DrawEcho(
                echo,
                history * fraction,
                0.22f * (1f - fraction * 0.8f),
                0.65f
            );
        }
    }

    private void DrawSubmarineMarker()
    {
        int center = Resolution / 2;

        for (int y = center - 2; y <= center + 3; y++)
        {
            int width = y > center ? 1 : 2;

            for (int x = center - width;
                x <= center + width;
                x++)
            {
                pixels[y * Resolution + x] =
                    new Color(0.5f, 1f, 0.65f);
            }
        }
    }

    private void DrawEcho(
        Echo echo,
        float historySeconds = 0f,
        float opacity = 1f,
        float sizeScale = 1f)
    {
        if (echo.source == null ||
            !echo.source.isActiveAndEnabled ||
            !echo.source.Active)
        {
            return;
        }

        float brightness = Mathf.Clamp01(
            1f - echo.age / Mathf.Max(0.1f, echoDuration)
        );

        brightness *=
            Mathf.Clamp01(opacity) * echo.depthBrightness;

        if (brightness <= 0f)
            return;

        float range = Mathf.Max(1f, detectionRange);
        float center = (Resolution - 1) * 0.5f;
        float radarRadius = center - 2f;

        Vector3 displayPosition =
            GetEchoPosition(echo, historySeconds);

        Vector2 offset = new Vector2(
            displayPosition.x,
            displayPosition.z
        ) - controller.NavigationPosition;

        Vector2 relative = ToLocal(offset) / range;

        bool reef = echo.kind == RepairContactKind.Reef;

        float normalizedSize = reef
            ? Mathf.Max(0.025f, echo.sampledRadius / range)
            : Mathf.Clamp(
                echo.sampledRadius / range, 0.025f, 0.08f
            );

        float size =
            normalizedSize * radarRadius *
            Mathf.Max(0.1f, sizeScale);

        Vector2 screenPosition = new Vector2(
            center + relative.x * radarRadius,
            center + relative.y * radarRadius
        );

        int minimumX = Mathf.Max(
            0, Mathf.FloorToInt(screenPosition.x - size)
        );

        int maximumX = Mathf.Min(
            Resolution - 1,
            Mathf.CeilToInt(screenPosition.x + size)
        );

        int minimumY = Mathf.Max(
            0, Mathf.FloorToInt(screenPosition.y - size)
        );

        int maximumY = Mathf.Min(
            Resolution - 1,
            Mathf.CeilToInt(screenPosition.y + size)
        );

        Color tint = echo.kind == RepairContactKind.Fish
            ? fishColor
            : echo.kind == RepairContactKind.Battery
                ? new Color(1f, 0.5f, 0.08f)
            : echo.kind == RepairContactKind.OxygenCanister
                ? oxygenCanisterColor
                : rockColor;

        for (int y = minimumY; y <= maximumY; y++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                int index = y * Resolution + x;

                if (pixelRadii[index] > 1f)
                    continue;

                float distance = Vector2.Distance(
                    new Vector2(x, y), screenPosition
                );

                if (distance > size)
                    continue;

                Color color = pixels[index];

                if (reef)
                {
                    float coverage = Mathf.Clamp01(
                        size - distance
                    );

                    float fill =
                        Mathf.Clamp01(reefFillOpacity) *
                        coverage * brightness;

                    color = Color.Lerp(
                        color, reefFillColor, fill
                    );

                    float edge = Mathf.Clamp01(
                        distance -
                        (size - Mathf.Max(1f, reefOutlineWidth))
                    ) * coverage;

                    color +=
                        reefOutlineColor * edge * brightness;
                }
                else
                {
                    float glow = Mathf.Clamp01(
                        1f - distance / size
                    );

                    color += tint * glow * brightness;
                }

                color.a = 1f;
                pixels[index] = color;
            }
        }
    }

    private void OnDestroy()
    {
        if (sonarScreen != null &&
            sonarScreen.texture == screenTexture)
        {
            sonarScreen.texture = null;
        }

        if (screenTexture != null)
            Destroy(screenTexture);
    }
}
