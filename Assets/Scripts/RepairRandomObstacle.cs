using UnityEngine;

public class RepairRandomObstacle : MonoBehaviour
{
    [Header("Your existing systems / 你现有的系统")]
    public SubmarineController controller;
    public SubmarineSonar sonar;

    [Header("Spawn distance / 前方生成距离")]
    public Vector2 forwardDistance = new Vector2(120f, 180f);

    [Header("Position variation / 位置变化")]
    public float lateralVariation = 30f;
    public float depthVariation = 12f;

    [Header("Obstacle radius / 障碍半径")]
    public Vector2 radiusRange = new Vector2(8f, 14f);

    [Header("Passed clearance / 通过后的安全距离")]
    public float passedClearance = 20f;

    private bool ready;

    private void Start()
    {
        if (controller == null || sonar == null)
        {
            Debug.LogError(
                "RandomOcean: 请绑定 Controller 和 Sonar。",
                this
            );

            enabled = false;
            return;
        }

        SpawnObstacle();
        ready = true;
    }

    private void Update()
    {
        if (!ready || !controller.isActiveAndEnabled)
            return;

        Vector2 heading = GetForward();

        Vector2 obstaclePosition = new Vector2(
            sonar.obstacleLateral,
            sonar.obstacleForward
        );

        Vector2 offset =
            obstaclePosition - controller.NavigationPosition;

        float forwardDistanceToObstacle =
            Vector2.Dot(offset, heading);

        float clearance =
            Mathf.Max(0f, sonar.obstacleRadius)
            + Mathf.Max(10f, passedClearance);

        // 障碍已经位于船后方，并留出安全距离后，
        // 才将它替换成下一个障碍。
        if (forwardDistanceToObstacle < -clearance)
            SpawnObstacle();
    }

    private Vector2 GetForward()
    {
        float angle = controller.Heading * Mathf.Deg2Rad;

        return new Vector2(
            Mathf.Sin(angle),
            Mathf.Cos(angle)
        );
    }

    private void SpawnObstacle()
    {
        Vector2 forward = GetForward();
        Vector2 right = new Vector2(forward.y, -forward.x);

        float minimumDistance = Mathf.Max(
            40f,
            Mathf.Min(forwardDistance.x, forwardDistance.y)
        );

        float maximumDistance = Mathf.Max(
            minimumDistance,
            Mathf.Max(forwardDistance.x, forwardDistance.y)
        );

        Vector2 position =
            controller.NavigationPosition
            + forward * Random.Range(
                minimumDistance,
                maximumDistance
            )
            + right * Random.Range(
                -Mathf.Abs(lateralVariation),
                Mathf.Abs(lateralVariation)
            );

        sonar.obstacleLateral = position.x;
        sonar.obstacleForward = position.y;

        sonar.obstacleDepth = Mathf.Max(
            1f,
            controller.Depth + Random.Range(
                -Mathf.Abs(depthVariation),
                Mathf.Abs(depthVariation)
            )
        );

        float minimumRadius = Mathf.Max(
            1f,
            Mathf.Min(radiusRange.x, radiusRange.y)
        );

        float maximumRadius = Mathf.Max(
            minimumRadius,
            Mathf.Max(radiusRange.x, radiusRange.y)
        );

        sonar.obstacleRadius = Random.Range(
            minimumRadius,
            maximumRadius
        );
    }
}