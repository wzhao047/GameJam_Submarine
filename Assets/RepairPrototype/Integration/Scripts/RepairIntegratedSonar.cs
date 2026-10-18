using UnityEngine;

[DefaultExecutionOrder(-200)]
public class RepairIntegratedSonar : MonoBehaviour
{
    [Header("你的航行数据接口")]
    public RepairNavigationSource source;

    [Header("复用的四层声呐网格")]
    public MeshRenderer mapSurface;
    public MeshRenderer sweepSurface;
    public MeshRenderer markerSurface;
    public MeshRenderer echoSurface;

    [Header("扫描设置")]
    [Min(0.5f)]
    public float sweepDuration = 2f;

    [Range(5f, 100f)]
    public float trailDegrees = 40f;

    private float scanAngle;
    private MaterialPropertyBlock properties;

    private void Awake()
    {
        // 仅停用这个声呐副本中的旧控制组件。
        // 不修改原脚本，也不影响其他场景。
        MonoBehaviour[] scripts =
            GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour script in scripts)
        {
            if (script is SonarVisualLayer ||
                script is CircularSonar ||
                script is SonarScreen)
            {
                script.enabled = false;
            }
        }

        properties = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (source == null ||
            mapSurface == null ||
            sweepSurface == null ||
            markerSurface == null ||
            echoSurface == null)
        {
            Debug.LogError(
                "请绑定航行数据接口和四层声呐网格。",
                this
            );

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        bool working = source.SonarOnline;

        if (working && source.IsRunning)
        {
            scanAngle = Mathf.Repeat(
                scanAngle -
                360f / Mathf.Max(0.5f, sweepDuration) *
                Time.deltaTime,
                360f
            );
        }

        DrawLayer(
            mapSurface, 0,
            new Color(0.04f, 0.65f, 0.45f, 0.55f),
            working
        );

        DrawLayer(
            sweepSurface, 1,
            new Color(0.04f, 0.85f, 0.58f, 0.9f),
            working
        );

        DrawLayer(
            markerSurface, 2,
            new Color(0.18f, 1f, 0.62f, 1f),
            working
        );

        // 障碍回波下一步接入，当前先清空。
        DrawLayer(
            echoSurface, 3,
            Color.white,
            working
        );
    }

    private void DrawLayer(
        MeshRenderer surface,
        int layer,
        Color tint,
        bool working)
    {
        surface.enabled = working;

        if (!working) return;

        properties.Clear();

        float width = Mathf.Abs(surface.transform.lossyScale.x);
        float height = Mathf.Max(
            0.001f,
            Mathf.Abs(surface.transform.lossyScale.y)
        );

        properties.SetFloat("_Aspect", width / height);
        properties.SetFloat("_Layer", layer);
        properties.SetColor("_Tint", tint);

        properties.SetFloat("_RingCount", 4f);
        properties.SetFloat("_Crosshair", 1f);
        properties.SetFloat("_MapRotation", 0f);

        properties.SetFloat("_ScanAngle", scanAngle * Mathf.Deg2Rad);
        properties.SetFloat("_TrailAngle", trailDegrees * Mathf.Deg2Rad);
        properties.SetFloat("_SweepDirection", -1f);

        properties.SetFloat("_DotRadius", 0.018f);
        properties.SetFloat("_Heading", Mathf.PI * 0.5f);
        properties.SetFloat("_HeadingArrow", 1f);

        properties.SetFloat("_EchoCount", 0f);

        // 只设置当前物体的绘制参数，不改共享材质。
        surface.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        SetSurfaceEnabled(mapSurface, false);
        SetSurfaceEnabled(sweepSurface, false);
        SetSurfaceEnabled(markerSurface, false);
        SetSurfaceEnabled(echoSurface, false);
    }

    private void SetSurfaceEnabled(
        MeshRenderer surface,
        bool value)
    {
        if (surface != null)
            surface.enabled = value;
    }
}
