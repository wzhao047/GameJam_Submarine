using UnityEngine;

public class SubmarineDepthDrums : MonoBehaviour
{
    [Header("驾驶系统")]
    public SubmarineController controller;

    [Header("从左到右拖入四个滚筒")]
    public Renderer thousands;
    public Renderer hundreds;
    public Renderer tens;
    public Renderer ones;

    [Header("当前数字贴图设置")]
    public Vector2 textureTiling = new Vector2(1f, 0.25f);
    public Vector2 zeroOffset = new Vector2(0f, -0.0875f);

    [Tooltip("切换到下一个数字时，贴图偏移的变化量")]
    public float offsetPerDigit = 0.1f;

    [Header("数字滚动速度")]
    [Tooltip("每秒经过多少个数字")]
    [Min(0.1f)]
    public float rollSpeed = 6f;

    private Renderer[] drums;

    private MaterialPropertyBlock[] originalBlocks;
    private MaterialPropertyBlock[] workingBlocks;

    private readonly float[] displayedDigits = new float[4];
    private readonly int[] targetDigits = new int[4];

    private bool initialized;

    private static readonly int BaseMapST =
        Shader.PropertyToID("_BaseMap_ST");

    private void Start()
    {
        drums = new Renderer[]
        {
            thousands,
            hundreds,
            tens,
            ones
        };

        if (controller == null)
        {
            Debug.LogError(
                "请连接深度表的 Controller。",
                this
            );

            enabled = false;
            return;
        }

        for (int i = 0; i < drums.Length; i++)
        {
            if (drums[i] == null ||
                drums[i].sharedMaterial == null ||
                !drums[i].sharedMaterial.HasProperty("_BaseMap"))
            {
                Debug.LogError(
                    "请按千、百、十、个位连接四个数字滚筒。",
                    this
                );

                enabled = false;
                return;
            }

            for (int j = 0; j < i; j++)
            {
                if (drums[i] == drums[j])
                {
                    Debug.LogError(
                        "同一个滚筒被重复连接，请检查四个引用。",
                        this
                    );

                    enabled = false;
                    return;
                }
            }
        }

        originalBlocks = new MaterialPropertyBlock[4];
        workingBlocks = new MaterialPropertyBlock[4];

        for (int i = 0; i < 4; i++)
        {
            originalBlocks[i] = new MaterialPropertyBlock();
            workingBlocks[i] = new MaterialPropertyBlock();

            drums[i].GetPropertyBlock(originalBlocks[i]);
            drums[i].GetPropertyBlock(workingBlocks[i]);
        }

        ReadDepth();

        for (int i = 0; i < 4; i++)
        {
            displayedDigits[i] = targetDigits[i];
            ApplyDigit(i);
        }

        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        ReadDepth();

        float maximumStep =
            Mathf.Max(0.1f, rollSpeed) *
            36f *
            Time.deltaTime;

        for (int i = 0; i < 4; i++)
        {
            // 将十个数字视为一圈，保证9与0之间连续滚动。
            float currentAngle = displayedDigits[i] * 36f;
            float targetAngle = targetDigits[i] * 36f;

            float angle = Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                maximumStep
            );

            displayedDigits[i] = Mathf.Repeat(
                angle / 36f,
                10f
            );

            ApplyDigit(i);
        }
    }

    private void ReadDepth()
    {
        // 显示整数米，不显示小数；最大显示9999米。
        int depth = Mathf.FloorToInt(
            Mathf.Clamp(controller.Depth, 0f, 9999f)
        );

        targetDigits[0] = depth / 1000;
        targetDigits[1] = depth / 100 % 10;
        targetDigits[2] = depth / 10 % 10;
        targetDigits[3] = depth % 10;
    }

    private void ApplyDigit(int index)
    {
        float offsetY =
            zeroOffset.y +
            displayedDigits[index] * offsetPerDigit;

        workingBlocks[index].SetVector(
            BaseMapST,
            new Vector4(
                textureTiling.x,
                textureTiling.y,
                zeroOffset.x,
                offsetY
            )
        );

        // 只改变这个滚筒的显示，不修改材质资源。
        drums[index].SetPropertyBlock(
            workingBlocks[index]
        );
    }

    private void OnDisable()
    {
        if (!initialized)
            return;

        for (int i = 0; i < drums.Length; i++)
        {
            if (drums[i] != null)
                drums[i].SetPropertyBlock(originalBlocks[i]);
        }
    }
}