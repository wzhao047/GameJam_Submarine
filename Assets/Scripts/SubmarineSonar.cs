using UnityEngine;
using UnityEngine.UI;

public class SubmarineSonar : MonoBehaviour
{
    [Header("Screen / 声呐屏幕")]
    public RawImage sonarScreen;

    [Header("Ocean systems / 海域系统")]
    public RepairOceanDirector ocean;
    public FusePuzzleInteraction fuse;

    [Header("Scanning / 扫描")]
    public float detectionRange = 200f;
    public float scanPeriod = 3f;
    public float echoDuration = 2.5f;

    // 暂时保留，避免原碰撞脚本出现编译错误。
    [Header("Legacy obstacle / 旧版障碍参数")]
    public float obstacleLateral;
    public float obstacleForward = 160f;
    public float obstacleDepth = 100f;
    public float obstacleRadius = 12f;

    private SubmarineController controller;
    private SubmarinePower power;

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
        public float sampledRadius;
        public float sampledTopDepth;
        public float sampledBottomDepth;
        public RepairContactKind kind;
        public float age = float.PositiveInfinity;
    }

    private void Awake()
    {
        controller = GetComponent<SubmarineController>();
        power = GetComponent<SubmarinePower>();

        CreateScreen();
    }

    private void Start()
    {
        if (sonarScreen == null || controller == null ||
            power == null || ocean == null)
        {
            Debug.LogError(
                "请绑定声呐屏幕和 Ocean 海域生成器，" +
                "并将脚本保留在 Submarine 上。",
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
            Resolution,
            Resolution,
            TextureFormat.RGBA32,
            false
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

                Color color = new Color(0.008f, 0.018f, 0.014f);

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
            ocean.Reefs.Length;

        echoes = new Echo[count];

        int index = 0;

        AddPool(ocean.Rocks, ref index);
        AddPool(ocean.Fish, ref index);
        AddPool(ocean.Reefs, ref index);
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
        float dt = Time.deltaTime;
        float previousAngle = scanAngle;

        float sweep = working
            ? 360f / Mathf.Max(0.2f, scanPeriod) * dt
            : 0f;

        if (working)
            scanAngle = Mathf.Repeat(scanAngle + sweep, 360f);

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

                if (!working || item == null ||
                    !item.isActiveAndEnabled || !item.Active)
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
                    echo.sampledPosition = item.position;
                    echo.sampledRadius = item.radius;
                    echo.sampledTopDepth = item.topDepth;
                    echo.sampledBottomDepth = item.bottomDepth;
                    echo.kind = item.kind;
                    echo.age = 0f;
                }

                if (echo.age >= Mathf.Max(0.1f, echoDuration))
                    continue;

                Vector2 sampledOffset = new Vector2(
                    echo.sampledPosition.x,
                    echo.sampledPosition.z
                ) - controller.NavigationPosition;

                float depthDifference =
                    echo.sampledPosition.y - controller.Depth;

                // 大型礁石读取其垂直范围，
                // 避免把深埋海底的中心误当成安全距离。
                if (echo.kind == RepairContactKind.Reef)
                {
                    depthDifference =
                        Mathf.Clamp(
                            controller.Depth,
                            echo.sampledTopDepth,
                            echo.sampledBottomDepth
                        ) - controller.Depth;
                }

                float distance = Mathf.Sqrt(
                    sampledOffset.sqrMagnitude +
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
                    scanAngle - pixelAngles[i],
                    360f
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
            // 先画大型区域，再画小目标，避免相互遮挡。
            foreach (Echo echo in echoes)
            {
                if (echo.kind == RepairContactKind.Reef)
                    DrawEcho(echo);
            }

            foreach (Echo echo in echoes)
            {
                if (echo.kind != RepairContactKind.Reef)
                    DrawEcho(echo);
            }
        }

        if (working)
        {
            int center = Resolution / 2;

            for (int y = center - 2; y <= center + 3; y++)
            {
                int width = y > center ? 1 : 2;

                for (int x = center - width;
                    x <= center + width; x++)
                {
                    pixels[y * Resolution + x] =
                        new Color(0.5f, 1f, 0.65f);
                }
            }
        }

        screenTexture.SetPixels32(pixels);
        screenTexture.Apply(false, false);
    }

    private void DrawEcho(Echo echo)
    {
        if (echo.source == null || !echo.source.Active)
            return;

        float brightness = Mathf.Clamp01(
            1f - echo.age / Mathf.Max(0.1f, echoDuration)
        );

        if (brightness <= 0f)
            return;

        float range = Mathf.Max(1f, detectionRange);
        float center = (Resolution - 1) * 0.5f;
        float radarRadius = center - 2f;

        Vector2 offset = new Vector2(
            echo.sampledPosition.x,
            echo.sampledPosition.z
        ) - controller.NavigationPosition;

        Vector2 relative = ToLocal(offset) / range;

        bool reef = echo.kind == RepairContactKind.Reef;

        float normalizedSize = reef
            ? Mathf.Max(0.025f, echo.sampledRadius / range)
            : Mathf.Clamp(
                echo.sampledRadius / range,
                0.025f,
                0.08f
            );

        float size = normalizedSize * radarRadius;

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

        Color tint = reef
            ? new Color(1f, 0.45f, 0.08f)
            : echo.kind == RepairContactKind.Fish
                ? new Color(0.65f, 1f, 0.55f)
                : new Color(0.15f, 0.7f, 0.3f);

        for (int y = minimumY; y <= maximumY; y++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                int index = y * Resolution + x;

                if (pixelRadii[index] > 1f)
                    continue;

                float distance = Vector2.Distance(
                    new Vector2(x, y),
                    screenPosition
                );

                float glow = reef
                    ? Mathf.Clamp01((size - distance) / 2f)
                    : Mathf.Clamp01(1f - distance / size);

                Color color = pixels[index];
                color += tint * glow * brightness;
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