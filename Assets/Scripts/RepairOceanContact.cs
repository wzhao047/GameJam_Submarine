using UnityEngine;

public enum RepairContactKind
{
    Rock,       // 普通岩石
    Fish,       // 鱼群
    OxygenCanister = 3,
    Battery = 4,
    Reef = 2    // 大型海底礁石
}

public class RepairOceanContact : MonoBehaviour
{
    [Header("Contact type / 目标类型")]
    public RepairContactKind kind;

    [Header("Simulation position / 模拟航行位置")]
    [Tooltip("X：横向，Y：深度（向下为正），Z：前进距离")]
    public Vector3 position;

    [Header("Movement / 移动")]
    public Vector3 velocity;

    [Header("Collision size / 碰撞尺寸")]
    [Min(0.1f)]
    public float radius = 10f;

    [Header("Reef depth range / 大型礁石深度范围")]
    public float topDepth = 80f;
    public float bottomDepth = 500f;

    [Header("Runtime state / 运行状态")]
    [SerializeField] private bool active;
    [SerializeField] private bool hitRegistered;

    public bool Active => active;

    // Raised only when a fresh contact is accepted by the existing hull collision check.
    public static event System.Action<RepairOceanContact> HitRegistered;

    // 同一个目标在一次生成期间，最多造成一次损伤。
    public bool CanDamage => active && !hitRegistered;

    public void Activate(
        Vector3 spawnPosition,
        Vector3 moveVelocity,
        float contactRadius)
    {
        position = spawnPosition;
        velocity = moveVelocity;
        radius = Mathf.Max(0.1f, contactRadius);

        hitRegistered = false;
        active = true;
    }

    public void Advance(float deltaTime)
    {
        if (!active || deltaTime <= 0f)
            return;

        position += velocity * deltaTime;
    }

    public void RegisterHit()
    {
        if (!CanDamage)
            return;

        hitRegistered = true;
        HitRegistered?.Invoke(this);
    }

    public void ResetHitAfterSeparation() { hitRegistered = false; }
    public void Deactivate()
    {
        active = false;
        velocity = Vector3.zero;
    }
}