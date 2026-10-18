using UnityEngine;

public enum OceanKind { FixedObstacle=0, FishSchool=1, GiantReef=2, Goal=3 }

// Every slot is authored in WynnScene; activation never creates a new scene object.
public class OceanObject : MonoBehaviour
{
    public OceanKind kind;
    public Collider collisionShape;
    [Tooltip("World-space radar radius. Zero uses the normal point size.")]
    public float radarRadius;
    [Min(0f)] public float blinkPeriod=1f;
    [Range(0.05f,0.95f)] public float blinkDuty=0.5f;
    [Header("Runtime telemetry")]
    [SerializeField] private Vector3 worldVelocity;
    [SerializeField] private Vector3 destination;
    [SerializeField] private float age;
    [SerializeField] private float spawnDepth;
    public Vector3 WorldVelocity=>worldVelocity;
    public Vector3 Destination=>destination;
    public float Age=>age;
    public float SpawnDepth=>spawnDepth;
    public bool Active=>isActiveAndEnabled && collisionShape!=null && collisionShape.enabled;
    public bool BlinkVisible=>blinkPeriod<=0f || Mathf.Repeat(age,blinkPeriod)<blinkPeriod*blinkDuty;
    public bool Arrived { get; private set; }

    public void Activate(Vector3 position,Vector3 velocity,Vector3 endpoint,float depth)
    {
        transform.position=position;
        worldVelocity=velocity;
        destination=endpoint;
        age=0f; spawnDepth=depth; Arrived=false;
        gameObject.SetActive(true);
    }
    public void Deactivate() { gameObject.SetActive(false); }
    public void Advance(float dt)
    {
        if(!Active || dt<=0f) return;
        age+=dt;
        if(kind!=OceanKind.FishSchool) return;
        // The route endpoint chooses a heading; keep swimming if it is still on radar.
        transform.position+=worldVelocity*dt;
        if(!Arrived && worldVelocity.sqrMagnitude>0f && Vector3.Dot(destination-transform.position,worldVelocity)<=0f)
            Arrived=true;
    }
}
