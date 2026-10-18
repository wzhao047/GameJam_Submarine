using UnityEngine;

public class RepairOceanDirector : MonoBehaviour
{
    [Header("Existing systems / 现有系统")]
    public SubmarineController controller;
    public SubmarineSonar sonar;

    [Header("Object pools / 对象池")]
    public Transform rockPool;
    public Transform fishPool;
    public Transform reefPool;

    [Header("Spawn intervals / 生成间隔（秒）")]
    [Min(1f)] public float rockInterval = 12f;
    [Min(1f)] public float fishInterval = 6f;
    [Min(1f)] public float reefInterval = 24f;

    [Header("Reef settings / 大型礁石设置")]
    [Min(1f)] public float firstReefDelay = 20f;
    [Min(1f)] public float reefRiseToAvoid = 20f;
    [Min(1f)] public float seabedDepth = 500f;

    [Header("Fish movement / 鱼群移动")]
    public Vector2 fishSpeedRange = new Vector2(2f, 5f);

    private RepairOceanContact[] rocks;
    private RepairOceanContact[] fish;
    private RepairOceanContact[] reefs;

    private float rockTimer;
    private float fishTimer;
    private float reefTimer;
    private bool ready;

    // 提供给后续的声呐和碰撞脚本。
    public RepairOceanContact[] Rocks => rocks;
    public RepairOceanContact[] Fish => fish;
    public RepairOceanContact[] Reefs => reefs;
    public bool Ready => ready;

    private float DetectionRange =>
        Mathf.Max(80f, sonar.detectionRange);

    private Vector3 BoatPosition
    {
        get
        {
            Vector2 navigation = controller.NavigationPosition;

            return new Vector3(
                navigation.x,
                controller.Depth,
                navigation.y
            );
        }
    }

    private void Start()
    {
        if (controller == null || sonar == null ||
            rockPool == null || fishPool == null ||
            reefPool == null)
        {
            Debug.LogError(
                "请补齐海域生成管理器的五个引用。",
                this
            );

            enabled = false;
            return;
        }

        rocks = rockPool.GetComponentsInChildren
            <RepairOceanContact>(true);

        fish = fishPool.GetComponentsInChildren
            <RepairOceanContact>(true);

        reefs = reefPool.GetComponentsInChildren
            <RepairOceanContact>(true);

        ResetPool(rocks);
        ResetPool(fish);
        ResetPool(reefs);

        rockTimer = Mathf.Max(1f, rockInterval);
        fishTimer = Mathf.Max(1f, fishInterval);
        reefTimer = Mathf.Max(1f, firstReefDelay);

        ready = true;

        // 开局先放置三个岩石和一群鱼。
        for (int i = 0; i < 3; i++)
            SpawnRock();

        SpawnFish();
    }

    private void Update()
    {
        if (!ready || !controller.isActiveAndEnabled)
            return;

        float dt = Time.deltaTime;

        AdvancePool(rocks, dt);
        AdvancePool(fish, dt);
        AdvancePool(reefs, dt);

        rockTimer -= dt;
        fishTimer -= dt;
        reefTimer -= dt;

        if (rockTimer <= 0f)
        {
            SpawnRock();
            rockTimer = Mathf.Max(1f, rockInterval);
        }

        if (fishTimer <= 0f)
        {
            SpawnFish();
            fishTimer = Mathf.Max(1f, fishInterval);
        }

        if (reefTimer <= 0f)
        {
            SpawnReef();
            reefTimer = Mathf.Max(1f, reefInterval);
        }
    }

    private void ResetPool(RepairOceanContact[] pool)
    {
        foreach (RepairOceanContact item in pool)
            item.Deactivate();
    }

    private RepairOceanContact GetFree(
        RepairOceanContact[] pool)
    {
        foreach (RepairOceanContact item in pool)
        {
            if (item.isActiveAndEnabled && !item.Active)
                return item;
        }

        // 对象池用完时跳过本次生成。
        return null;
    }

    private void AdvancePool(
        RepairOceanContact[] pool,
        float dt)
    {
        Vector3 boat = BoatPosition;

        foreach (RepairOceanContact item in pool)
        {
            if (!item.Active)
                continue;

            item.Advance(dt);

            Vector3 offset = item.position - boat;
            float planarDistance = new Vector2(
                offset.x, offset.z
            ).magnitude;

            // 离开声呐范围一段距离后才回收。
            float recycleDistance =
                DetectionRange + item.radius + 60f;

            if (planarDistance > recycleDistance)
                item.Deactivate();
        }
    }

    private Vector3 Direction(float heading)
    {
        float angle = heading * Mathf.Deg2Rad;

        return new Vector3(
            Mathf.Sin(angle),
            0f,
            Mathf.Cos(angle)
        );
    }

    private void SpawnRock()
    {
        RepairOceanContact item = GetFree(rocks);

        if (item == null)
            return;

        Vector3 direction = Direction(
            controller.Heading + Random.Range(-65f, 65f)
        );

        Vector3 position =
            BoatPosition
            + direction * Random.Range(
                DetectionRange * 0.65f,
                DetectionRange * 0.95f
            );

        position.y = Mathf.Max(
            1f,
            controller.Depth + Random.Range(-12f, 12f)
        );

        item.kind = RepairContactKind.Rock;

        item.Activate(
            position,
            Vector3.zero,
            Random.Range(8f, 14f)
        );
    }

    private void SpawnFish()
    {
        RepairOceanContact item = GetFree(fish);

        if (item == null)
            return;

        Vector3 forward = Direction(controller.Heading);

        Vector3 right = new Vector3(
            forward.z, 0f, -forward.x
        );

        float side = Random.value < 0.5f ? -1f : 1f;

        // 从前方一侧横穿，而非静止等待潜艇撞上。
        Vector3 position =
            BoatPosition
            + forward * Random.Range(
                DetectionRange * 0.2f,
                DetectionRange * 0.6f
            )
            + right * side * DetectionRange * 0.7f;

        position.y = Mathf.Max(
            1f,
            controller.Depth + Random.Range(-6f, 6f)
        );

        float minimumSpeed = Mathf.Max(
            0.5f,
            Mathf.Min(fishSpeedRange.x, fishSpeedRange.y)
        );

        float maximumSpeed = Mathf.Max(
            minimumSpeed,
            Mathf.Max(fishSpeedRange.x, fishSpeedRange.y)
        );

        Vector3 velocity =
            -right * side * Random.Range(
                minimumSpeed, maximumSpeed
            );

        item.kind = RepairContactKind.Fish;

        item.Activate(
            position,
            velocity,
            Random.Range(5f, 9f)
        );
    }

    private void SpawnReef()
    {
        // 太浅时不生成需要上浮避开的礁石。
        if (controller.Depth <= reefRiseToAvoid + 10f)
            return;

        RepairOceanContact item = GetFree(reefs);

        if (item == null)
            return;

        Vector3 direction = Direction(
            controller.Heading + Random.Range(-15f, 15f)
        );

        Vector3 position =
            BoatPosition
            + direction * DetectionRange * 0.95f;

        item.kind = RepairContactKind.Reef;

        item.topDepth = Mathf.Max(
            5f,
            controller.Depth - reefRiseToAvoid
        );

        item.bottomDepth = Mathf.Max(
            seabedDepth,
            controller.Depth + 50f
        );

        position.y =
            (item.topDepth + item.bottomDepth) * 0.5f;

        item.Activate(
            position,
            Vector3.zero,
            DetectionRange * 0.25f
        );
    }
}
