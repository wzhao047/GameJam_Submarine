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

    [Header("Oxygen canister rewards")]
    [Min(1)] public int oxygenPoolSize = 4;
    [Min(1f)] public float oxygenSpawnInterval = 30f;
    [Min(0f)] public float oxygenRestoreSeconds = 20f;
    [Min(0.1f)] public float oxygenPickupRadius = 6f;
    [Min(0f)] public float oxygenDepthSpread = 4f;
    [Range(0f, 65f)] public float oxygenSpawnAngle = 20f;

    private RepairOceanContact[] oxygenCanisters;
    private float oxygenTimer;
    private float batteryTimer = 100f;
    private int encounterCapacity;
    private SubmarineHull submarineHull;
    public RepairOceanContact[] OxygenCanisters => oxygenCanisters;
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
    private SubmarineGoalMarker goal;
    [Min(1f)] public float goalSafetyRadius = 65f;
    [Min(12f)] public float oxygenPassageWidth = 12f;

    private bool NearGoal(Vector3 position, float radius)
    {
        if (goal == null || (VoyageModeSession.Current != null && VoyageModeSession.Current.Mode == VoyageMode.Endless)) return false;
        Vector3 offset = position - goal.GoalPosition;
        return new Vector2(offset.x, offset.z).magnitude < radius + goalSafetyRadius;
    }

    private bool ClearOfHazards(Vector3 position, float radius)
    {
        foreach (var reef in reefs)
            if (reef.Active && new Vector2(position.x - reef.position.x,
                position.z - reef.position.z).magnitude < reef.radius + radius) return false;
        foreach (var rock in rocks)
            if (rock.Active && Vector3.Distance(position, rock.position) < rock.radius + radius) return false;
        return true;
    }

    private sealed class RewardEncounter
    {
        public Vector3 center;
        public int exits;
        public float radius;
        public RepairOceanContact[] walls;
        public RepairOceanContact[] rewards;
    }
    private readonly System.Collections.Generic.List<RewardEncounter> encounters =
        new System.Collections.Generic.List<RewardEncounter>();

    private bool ClearOfRewards(Vector3 position, float radius)
    {
        foreach (var encounter in encounters)
            if (new Vector2(position.x - encounter.center.x,
                position.z - encounter.center.z).magnitude < radius + encounter.radius + 8f) return false;
        return true;
    }

    private void RecycleEncounters()
    {
        for (int i = encounters.Count - 1; i >= 0; i--)
        {
            var encounter = encounters[i];
            Vector3 offset = encounter.center - BoatPosition;
            if (new Vector2(offset.x, offset.z).magnitude <= DetectionRange + encounter.radius + 60f) continue;
            foreach (var wall in encounter.walls) wall.Deactivate();
            foreach (var reward in encounter.rewards) reward.Deactivate();
            encounters.RemoveAt(i);
        }
    }

    private bool BelongsToEncounter(RepairOceanContact contact)
    {
        foreach (var encounter in encounters)
        {
            if (System.Array.IndexOf(encounter.walls, contact) >= 0) return true;
            if (System.Array.IndexOf(encounter.rewards, contact) >= 0) return true;
        }
        return false;
    }
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
        goal = Object.FindAnyObjectByType<SubmarineGoalMarker>();
        submarineHull = controller.GetComponent<SubmarineHull>();
        encounterCapacity = Mathf.Max(1, oxygenPoolSize) +
            (VoyageModeSession.Current != null && VoyageModeSession.Current.Mode == VoyageMode.Endless ? 2 : 0);
        int authoredCount = reefs.Length;
        System.Array.Resize(ref reefs, Mathf.Max(authoredCount, encounterCapacity * 6 + 4));
        for (int i = authoredCount; i < reefs.Length; i++)
        {
            var reef = new GameObject("RewardReef_" + i);
            reef.transform.SetParent(reefPool, false);
            reefs[i] = reef.AddComponent<RepairOceanContact>();
        }

        // Build once at startup; reuse the same contacts throughout the voyage.
        GameObject rewardPool = new GameObject("OxygenCanisterPool");
        rewardPool.transform.SetParent(transform, false);
        oxygenCanisters = new RepairOceanContact[encounterCapacity * 4];
        for (int i = 0; i < oxygenCanisters.Length; i++)
        {
            GameObject canister = new GameObject("OxygenCanister_" + i);
            canister.transform.SetParent(rewardPool.transform, false);
            oxygenCanisters[i] = canister.AddComponent<RepairOceanContact>();
            oxygenCanisters[i].kind = RepairContactKind.OxygenCanister;
        }
        oxygenTimer = Mathf.Max(1f, oxygenSpawnInterval);
        ResetPool(rocks);
        ResetPool(fish);
        ResetPool(reefs);

        rockTimer = Mathf.Max(1f, rockInterval);
        fishTimer = Mathf.Max(1f, fishInterval);
        reefTimer = Mathf.Max(1f, firstReefDelay);

        ready = true;

        // 开局先放置三个岩石和一群鱼。
        SpawnOxygenCanister();
        for (int i = 0; i < 3; i++)
            SpawnRock();

        SpawnFish();
    }

    private void Update()
    {
        if (!ready || !controller.isActiveAndEnabled)
            return;

        float dt = Time.deltaTime;

        RecycleEncounters();
        AdvancePool(rocks, dt);
        AdvancePool(fish, dt);
        AdvancePool(reefs, dt);
        AdvancePool(oxygenCanisters, dt);
        if (VoyageModeSession.Current != null && VoyageModeSession.Current.Mode == VoyageMode.Endless)
        {
            batteryTimer -= dt;
            if (batteryTimer <= 0f)
            {
                batteryTimer = SpawnOxygenCanister(true) ? 75f : 5f;
            }
        }
        oxygenTimer -= dt;
        if (oxygenTimer <= 0f)
        {
            oxygenTimer = SpawnOxygenCanister() ? Mathf.Max(1f, oxygenSpawnInterval) : 5f;
        }

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

            if (planarDistance > recycleDistance && !BelongsToEncounter(item))
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

    private bool SpawnOxygenCanister(bool battery = false)
    {
        if (controller.Depth <= reefRiseToAvoid + 10f || encounters.Count >= encounterCapacity) return false;
        var hull = submarineHull;

        float hullRadius = hull != null ? Mathf.Max(0f, hull.submarineRadius) : 3f;
        int exits = VoyageModeSession.Current != null ? VoyageModeSession.Current.ChooseExitCount() : Random.Range(1, 4);
        int count = Random.Range(exits == 2 ? 4 : 5, 7);
        // Fewer exits and more walls increase the payout, without changing each canister's value.
        int rewardCount = battery ? 1 : 4 - exits + (count == 6 ? 1 : 0);
        var walls = new System.Collections.Generic.List<RepairOceanContact>(count);
        var rewards = new System.Collections.Generic.List<RepairOceanContact>(rewardCount);
        foreach (var reef in reefs)
            if (reef.isActiveAndEnabled && !reef.Active && walls.Count < count) walls.Add(reef);
        foreach (var reward in oxygenCanisters)
            if (reward.isActiveAndEnabled && !reward.Active && !BelongsToEncounter(reward)
                && rewards.Count < rewardCount) rewards.Add(reward);
        if (walls.Count != count || rewards.Count != rewardCount) return false;

        // Spread the openings around a ring, then vary their phase, angle and wall size.
        // Closed seams overlap; open seams must fit the hull plus a steering margin.
        float baseGap = 360f / (count + exits);
        float[] gaps = new float[count];
        float[] angles = new float[count];
        int phase = Random.Range(0, count);
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            int slot = (i + phase) % count;
            bool opening = ((slot + 1) * exits / count) != (slot * exits / count);
            gaps[i] = baseGap * (opening ? 2f : 1f) + Random.Range(-2f, 2f);
            sum += gaps[i];
        }
        float largestClosed = 0f, smallestOpen = 360f;
        for (int i = 0; i < count; i++)
        {
            gaps[i] *= 360f / sum;
            if (gaps[i] > baseGap * 1.5f) smallestOpen = Mathf.Min(smallestOpen, gaps[i]);
            else largestClosed = Mathf.Max(largestClosed, gaps[i]);
        }
        float passage = Mathf.Max(oxygenPassageWidth, hullRadius * 2f + 6f);
        float closedSin = Mathf.Sin(largestClosed * 0.5f * Mathf.Deg2Rad);
        float openSin = Mathf.Sin(smallestOpen * 0.5f * Mathf.Deg2Rad);
        float ringRadius = Mathf.Max(DetectionRange * Random.Range(0.21f, 0.26f),
            (passage * 0.5f + 3f) / Mathf.Max(0.01f, openSin - closedSin));
        // Reserve a clear central turning area, including hull clearance.
        // A single exit requires returning through the entrance, so it gets the most room.
        float turningRadius = exits == 1 ? 42f : exits == 2 ? 30f : 20f;
        turningRadius = Mathf.Max(turningRadius, hullRadius * (exits == 1 ? 14f : exits == 2 ? 10f : 6.5f));
        ringRadius = Mathf.Max(ringRadius,
            (turningRadius + hullRadius + 3f) / Mathf.Max(0.01f, 1f - closedSin));
        float minimumWall = ringRadius * closedSin + 1f;
        float maximumWall = Mathf.Min(ringRadius * openSin - passage * 0.5f,
            ringRadius - turningRadius - hullRadius - 2f);
        float extent = ringRadius + maximumWall;
        Vector3 center = Vector3.zero;
        bool found = false;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            center = BoatPosition + Direction(controller.Heading + Random.Range(-oxygenSpawnAngle, oxygenSpawnAngle))
                * Mathf.Max(DetectionRange * Random.Range(0.75f, 0.9f), extent + hullRadius + 20f);
            center.y = Mathf.Max(1f, controller.Depth + Random.Range(-oxygenDepthSpread, oxygenDepthSpread));
            if (!NearGoal(center, extent) && ClearOfHazards(center, extent)
                && ClearOfRewards(center, extent)) { found = true; break; }
        }
        if (!found) return false;
        float angle = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            angles[i] = angle;
            angle += gaps[i];
            var wall = walls[i];
            wall.kind = RepairContactKind.Reef;
            wall.topDepth = Mathf.Max(5f, center.y - reefRiseToAvoid + Random.Range(-3f, 3f));
            wall.bottomDepth = Mathf.Max(seabedDepth, center.y + 50f);
            Vector3 position = center + Direction(angles[i]) * ringRadius;
            position.y = (wall.topDepth + wall.bottomDepth) * 0.5f;
            wall.Activate(position, Vector3.zero, Random.Range(minimumWall, maximumWall));
        }
        float pickup = Mathf.Max(0.1f, oxygenPickupRadius);
        float innerRadius = Mathf.Max(0f, ringRadius - maximumWall - hullRadius - pickup - 2f);
        float rewardPhase = Random.Range(0f, 360f);
        for (int i = 0; i < rewardCount; i++)
        {
            Vector3 position = center + Direction(rewardPhase + i * 360f / rewardCount)
                * Mathf.Min(innerRadius, rewardCount == 1 ? 0f : pickup + hullRadius + 2f);
            rewards[i].kind = battery ? RepairContactKind.Battery : RepairContactKind.OxygenCanister;
            rewards[i].Activate(position, Vector3.zero, pickup);
        }
        encounters.Add(new RewardEncounter { center = center, radius = extent, exits = exits,
            walls = walls.ToArray(), rewards = rewards.ToArray() });
        return true;
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
        if (NearGoal(position, 14f) || !ClearOfRewards(position, 14f)) return;

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
        if (NearGoal(position, DetectionRange * 0.25f)
            || !ClearOfRewards(position, DetectionRange * 0.25f)) return;

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
