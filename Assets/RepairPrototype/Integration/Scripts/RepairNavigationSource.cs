using UnityEngine;

public class RepairNavigationSource : MonoBehaviour
{
    [Header("你现有的系统")]
    public SubmarineController controller;
    public SubmarinePower power;
    public SubmarineOxygen oxygen;
    public FusePuzzleInteraction fuse;

    [Header("海洋模拟原点，可暂时留空")]
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

    public float Heading =>
        controller != null ? controller.Heading : 0f;

    public bool SonarOnline =>
        power != null &&
        fuse != null &&
        power.SonarWorking &&
        fuse.CircuitWorking;

    public bool IsRunning =>
        controller != null &&
        controller.isActiveAndEnabled &&
        oxygen != null &&
        !oxygen.HasFailed;

    public Vector3 WorldPosition
    {
        get
        {
            Vector3 origin = oceanOrigin != null
                ? oceanOrigin.position
                : Vector3.zero;

            if (controller == null)
                return origin;

            Vector2 position = controller.NavigationPosition;

            return origin + new Vector3(
                position.x,
                -controller.Depth,
                position.y
            );
        }
    }

    private void Start()
    {
        if (controller == null || power == null ||
            oxygen == null || fuse == null)
        {
            Debug.LogError("请补齐航行数据接口的四个系统引用。", this);
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