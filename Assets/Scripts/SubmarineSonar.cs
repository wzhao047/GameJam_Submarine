using UnityEngine;
using UnityEngine.UI;

public class SubmarineSonar : MonoBehaviour
{
    [Header("操纵台上的声呐屏幕")]
    public RawImage sonarScreen;

    [Header("扫描设置")]
    public float detectionRange = 200f;
    public float scanPeriod = 3f;
    public float echoDuration = 2.5f;

    [Header("测试礁石的虚拟位置")]
    public float obstacleLateral = 0f;
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

    private float scanAngle;
    private float echoAge = 999f;
    private float refreshTimer;
    private float contactRange;
    private float contactDepthDifference;

    private Vector2 echoWorldPosition;
    private bool lastWorking;

    public bool IsWorking =>
        isActiveAndEnabled && power != null && power.SonarWorking;

    public bool HasContact =>
        IsWorking &&
        echoAge < Mathf.Max(0.1f, echoDuration);

    // 以下读数保留最近一次扫描得到的结果。
    public float ContactRange => contactRange;

    // 正数表示目标比潜艇更深，负数表示目标更浅。
    public float ContactDepthDifference => contactDepthDifference;

    private void Awake()
    {
        controller = GetComponent<SubmarineController>();
        power = GetComponent<SubmarinePower>();
        CreateScreen();
    }

    private void Start()
    {
        if (sonarScreen == null || controller == null || power == null)
        {
            Debug.LogError(
                "请把声呐脚本挂到 Submarine，并连接 SonarScreen。",
                this
            );

            enabled = false;
            return;
        }

        sonarScreen.texture = screenTexture;
        sonarScreen.color = Color.white;
        sonarScreen.raycastTarget = false;

        RenderScreen(IsWorking);
    }

    private void CreateScreen()
    {
        screenTexture = new Texture2D(
            Resolution, Resolution,
            TextureFormat.RGBA32, false
        );

        screenTexture.name = "Runtime Sonar Screen";
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
                    Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f
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

    private void Update()
    {
        if (controller == null || power == null)
            return;

        float deltaTime = Time.deltaTime;
        bool working = IsWorking;
        echoAge += deltaTime;

        if (working)
        {
            float previousAngle = scanAngle;

            float sweep =
                360f / Mathf.Max(0.2f, scanPeriod) * deltaTime;

            scanAngle = Mathf.Repeat(scanAngle + sweep, 360f);

            Vector2 target = new Vector2(
                obstacleLateral, obstacleForward
            );

            Vector2 offset = target - controller.NavigationPosition;
            Vector2 relative = ToLocal(offset);

            float targetAngle = Mathf.Repeat(
                Mathf.Atan2(relative.x, relative.y)
                * Mathf.Rad2Deg,
                360f
            );

            float angleToTarget = Mathf.Repeat(
                targetAngle - previousAngle, 360f
            );

            bool sweptTarget =
                sweep >= 360f || angleToTarget <= sweep;

            if (relative.magnitude <= Mathf.Max(1f, detectionRange)
                && sweptTarget)
            {
                echoWorldPosition = target;
                echoAge = 0f;

                float depthDifference =
                    obstacleDepth - controller.Depth;

                // 目标距离采用空间直线距离，包含深度差。
                contactRange = Mathf.Sqrt(
                    offset.sqrMagnitude
                    + depthDifference * depthDifference
                );

                contactDepthDifference = depthDifference;
            }
        }
        else
        {
            echoAge = 999f;
        }

        refreshTimer += deltaTime;

        if (refreshTimer >= RefreshInterval || working != lastWorking)
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
        float center = (Resolution - 1) * 0.5f;
        float radius = center - 2f;
        float range = Mathf.Max(1f, detectionRange);

        Vector2 echo = Vector2.zero;

        if (controller != null)
        {
            echo = ToLocal(
                echoWorldPosition - controller.NavigationPosition
            ) / range;
        }

        float echoBrightness = Mathf.Clamp01(
            1f - echoAge / Mathf.Max(0.1f, echoDuration)
        );

        float echoSize = Mathf.Clamp(
            obstacleRadius / range, 0.025f, 0.15f
        );

        bool showEcho =
            working && echoBrightness > 0f && echo.magnitude <= 1f;

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                int index = y * Resolution + x;
                Color color = background[index];

                if (!working)
                {
                    pixels[index] =
                        new Color(0.005f, 0.012f, 0.008f);
                    continue;
                }

                if (pixelRadii[index] <= 1f)
                {
                    float behind = Mathf.Repeat(
                        scanAngle - pixelAngles[index], 360f
                    );

                    if (behind < 40f)
                    {
                        color += new Color(0.02f, 0.22f, 0.08f)
                            * (1f - behind / 40f);
                    }

                    if (Mathf.Abs(Mathf.DeltaAngle(
                        scanAngle, pixelAngles[index])) < 1.2f)
                    {
                        color += new Color(0.12f, 0.65f, 0.3f);
                    }

                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;

                    if (showEcho)
                    {
                        float distanceToEcho = Vector2.Distance(
                            new Vector2(dx, dy), echo
                        );

                        float glow = Mathf.Clamp01(
                            1f - distanceToEcho / echoSize
                        );

                        color += new Color(0.5f, 1f, 0.2f)
                            * glow * echoBrightness;
                    }

                    bool vessel =
                        dy >= -0.025f &&
                        dy <= 0.055f &&
                        Mathf.Abs(dx) < (0.055f - dy) * 0.45f;

                    if (vessel)
                        color = new Color(0.5f, 1f, 0.65f);
                }

                color.a = 1f;
                pixels[index] = color;
            }
        }

        screenTexture.SetPixels32(pixels);
        screenTexture.Apply(false, false);
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