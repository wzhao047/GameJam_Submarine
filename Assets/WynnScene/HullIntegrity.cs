using UnityEngine;

// Damage feedback for the authored navigation body; the original SubmarineHull is independent.
[DefaultExecutionOrder(50),RequireComponent(typeof(SubmarineMovement))]
public class HullIntegrity : MonoBehaviour
{
    [Header("Scene references")]
    public SubmarineMovement movement;
    public VoyageResources resources;
    public Transform shakeCamera;
    public TextMesh hullStatus;
    [Header("Hull and impact damage")]
    [Min(1f)] public float maximumHull=100f;
    [Min(0f)] public float baseCollisionDamage=10f;
    [Tooltip("Damage per m/s of forward speed immediately before collision stops the submarine.")]
    [Min(0f)] public float damagePerSpeed=4f;
    [Header("Camera shake")]
    [Min(0f)] public float shakeDuration=0.45f;
    [Min(0f)] public float shakeStrength=0.035f;
    [Min(0f)] public float shakeFrequency=28f;
    [Header("Runtime telemetry")]
    [SerializeField] private float hull;
    [SerializeField] private float lastDamage;
    public float Hull=>hull;
    public float Percentage=>hull/Mathf.Max(1f,maximumHull)*100f;
    public float LastDamage=>lastDamage;
    public bool Destroyed=>hull<=0f;
    private float shakeRemaining,shakeElapsed;
    private Vector3 cameraBasePosition,lastAppliedPosition;
    private Transform offsetCamera;
    private SubmarineMovement subscribedMovement;

    private void Awake()
    {
        if(movement==null) movement=GetComponent<SubmarineMovement>();
        if(resources==null && movement!=null) resources=movement.resources;
        ResetHull();
        if(movement==null || resources==null)
        {
            Debug.LogError("Hull integrity needs the navigation movement and voyage resources.",this);
            enabled=false;
        }
    }
    private void OnEnable()
    {
        subscribedMovement=movement;
        if(subscribedMovement!=null) subscribedMovement.CollisionEntered+=TakeCollisionDamage;
    }
    private void OnDisable()
    {
        if(subscribedMovement!=null) subscribedMovement.CollisionEntered-=TakeCollisionDamage;
        subscribedMovement=null;
        RestoreCameraOffset();
        shakeRemaining=0f;
    }
    public void ResetHull()
    {
        RestoreCameraOffset();
        shakeRemaining=shakeElapsed=lastDamage=0f;
        hull=Mathf.Max(1f,maximumHull);
        RefreshStatus();
    }
    private void TakeCollisionDamage(float impactSpeed)
    {
        if(Destroyed || resources==null || resources.Failed || resources.Won) return;
        bool fishContact=movement.ContactObject!=null && movement.ContactObject.kind==OceanKind.FishSchool;
        // Fish still shake the camera and are consumed, but never damage the hull.
        lastDamage=fishContact ? 0f : Mathf.Max(0f,baseCollisionDamage)+Mathf.Max(0f,impactSpeed)*Mathf.Max(0f,damagePerSpeed);
        hull=Mathf.Max(0f,hull-lastDamage);
        shakeRemaining=Mathf.Max(0f,shakeDuration); shakeElapsed=0f;
        if(Destroyed) resources.FailVoyage("Hull destroyed.");
        RefreshStatus();
        movement.ConsumeRedContact();
    }
    private void LateUpdate()
    {
        RestoreCameraOffset();
        UpdateCameraShake(Time.deltaTime);
        RefreshStatus();
    }
    private void RestoreCameraOffset()
    {
        if(offsetCamera==null) return;
        // A station change owns the new camera position. Never restore an old viewpoint.
        if((offsetCamera.localPosition-lastAppliedPosition).sqrMagnitude<0.00000001f)
            offsetCamera.localPosition=cameraBasePosition;
        else shakeRemaining=0f;
        offsetCamera=null;
    }
    private void UpdateCameraShake(float dt)
    {
        if(shakeCamera==null || shakeRemaining<=0f) return;
        shakeElapsed+=dt;
        shakeRemaining=Mathf.Max(0f,shakeRemaining-dt);
        float fade=shakeRemaining/Mathf.Max(0.01f,shakeDuration);
        float strength=Mathf.Max(0f,shakeStrength)*fade*fade;
        float phase=shakeElapsed*Mathf.Max(0f,shakeFrequency);
        Vector3 offset=new Vector3(Mathf.Sin(phase*1.7f),Mathf.Sin(phase*2.3f+0.8f),Mathf.Sin(phase*1.3f+1.6f)*0.35f)*strength;
        cameraBasePosition=shakeCamera.localPosition;
        lastAppliedPosition=cameraBasePosition+shakeCamera.localRotation*offset;
        shakeCamera.localPosition=lastAppliedPosition;
        offsetCamera=shakeCamera;
    }
    private void RefreshStatus()
    {
        if(hullStatus==null) return;
        hullStatus.color=Percentage<=30f ? new Color(1f,0.65f,0.3f) : new Color(0.5f,1f,0.65f);
        hullStatus.text=$"HULL {Percentage:0}%";
    }
}
