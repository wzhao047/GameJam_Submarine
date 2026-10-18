using UnityEngine;

public class RepairWarningLamp : MonoBehaviour
{
    [Header("三种维修系统")]
    public LeakRepairInteraction leak;
    public CoolingRepairInteraction cooling;
    public FusePuzzleInteraction fuse;

    [Header("灯的渲染器")]
    public Renderer lampRenderer;

    [Header("灯光颜色")]
    public Color offColor = new Color(0.12f, 0.12f, 0.12f);
    public Color onColor = Color.red;

    [Header("自发光强度")]
    public float emissionIntensity = 3f;

    private Material lampMaterial;
    private bool lastWarning;

    private void Start()
    {
        if (lampRenderer == null)
            lampRenderer = GetComponent<Renderer>();

        if (lampRenderer == null ||
            leak == null || cooling == null || fuse == null)
        {
            Debug.LogError(
                "请绑定总故障灯的三个维修系统和灯渲染器。",
                this
            );

            enabled = false;
            return;
        }

        // 使用独立运行时材质，不改变其他物体的颜色。
        lampMaterial = lampRenderer.material;
        lampMaterial.EnableKeyword("_EMISSION");

        SetLamp(false);
    }

    private void Update()
    {
        bool warning =
            leak.HasFault ||
            cooling.HasFault ||
            fuse.HasFault;

        if (warning == lastWarning) return;

        SetLamp(warning);
    }

    private void SetLamp(bool warning)
    {
        lastWarning = warning;

        lampMaterial.SetColor(
            "_BaseColor",
            warning ? onColor : offColor
        );

        lampMaterial.SetColor(
            "_EmissionColor",
            warning
                ? onColor * Mathf.Max(0f, emissionIntensity)
                : Color.black
        );
    }

    private void OnDestroy()
    {
        if (lampMaterial != null)
            Destroy(lampMaterial);
    }
}
