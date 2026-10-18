using UnityEngine;

public class SubmarineIntegrationState : MonoBehaviour
{
    [Header("你现有的驾驶与资源系统")]
    public SubmarineController controller;
    public SubmarinePower power;
    public SubmarineOxygen oxygen;

    [Header("你的保险丝维修系统")]
    public FusePuzzleInteraction fuse;

    [Header("海洋模拟原点，暂时可以留空")]
    public Transform oceanOrigin;

    [Header("实时数据，仅用于观察")]
    [SerializeField] private Vector3 worldPosition;
    [SerializeField] private float speed;
    [SerializeField] private float depth;
    [SerializeField] private float heading;
    [SerializeField] private bool sonarOnline;

    public float Speed =>
        controller != null ? controller.CurrentSpeed : 0f;

    public float Depth =>
        controller != null ? controller.Depth : 0f;

    // 0 度朝 +Z，与现有航行数据一致。
    public float Heading =>
        controller != null ? controller.Heading : 0f;

    public Vector3 WorldPosition
    {
        get
        {
            Vector3 origin = oceanOrigin != null
                ? oceanOrigin.position
                : Vector3.zero;

            if (controller == null)
                return origin;

            Vector2 navigation = controller.NavigationPosition;

            // Unity 中 Y 向上，因此深度转换为负 Y。
            return origin + new Vector3(
                navigation.x,
                -controller.Depth,
                navigation.y
            );
        }
    }

    public bool SonarOnline =>
        power != null &&
        fuse != null &&
        power.SonarWorking &&
        fuse.CircuitWorking;

    public bool IsVoyageRunning =>
        controller != null &&
        controller.isActiveAndEnabled &&
        oxygen != null &&
        !oxygen.HasFailed;

    private void Start()
    {
        if (controller == null || power == null ||
            oxygen == null || fuse == null)
        {
            Debug.LogError(
                "请补齐航行连接接口的驾驶、供电、氧气和保险丝引用。",
                this
            );

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        worldPosition = WorldPosition;
        speed = Speed;
        depth = Depth;
        heading = Heading;
        sonarOnline = SonarOnline;
    }
}
