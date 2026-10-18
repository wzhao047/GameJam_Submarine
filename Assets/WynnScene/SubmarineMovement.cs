using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody),typeof(BoxCollider))]
public class SubmarineMovement : MonoBehaviour
{
    [Header("Scene references")]
    public Rigidbody body;
    public BoxCollider hull;
    public Transform courseOrigin;
    public VoyageResources resources;
    public Transform obstacles;
    [Header("Propulsion")]
    public float lowSpeed=1f, cruiseSpeed=3f, highSpeed=5f;
    [Min(0.01f)] public float acceleration=1f;
    [Header("Heading: 0 = north/+Z, 90 = east/+X")]
    [Range(0f,360f)] public float heading=90f;
    [Min(0f)] public float maximumTurnRate=30f;
    [Min(0.01f)] public float turnAcceleration=90f;
    [Header("Depth and trim")]
    public float maximumVerticalSpeed=2f;
    [Min(0.01f)] public float verticalAcceleration=0.8f;
    public float naturalSinkSpeed=0.3f, trimStep=0.1f;
    [Min(0f)] public float maximumTrim=1f;
    [Header("Input / live Inspector controls")]
    public bool keyboardInput=true;
    [Range(0,3)] public int gear;
    [Range(-1f,1f)] public float diveInput;
    [Tooltip("A = -1, D = +1. Adjusts heading continuously; does not strafe.")]
    [Range(-1f,1f)] public float turnInput;
    public float trim;
    [Header("Collision")]
    [Min(0.001f)] public float skin=0.02f;
    [Header("Runtime telemetry")]
    [SerializeField] private float speed, verticalSpeed, angularSpeed, distance, depth;
    [SerializeField] private int collisionCount;
    [SerializeField] private bool contact;
    [SerializeField] private string lastObstacle;
    public float Speed=>speed;
    public float Depth=>body!=null && courseOrigin!=null ? -(body.position.y-courseOrigin.position.y) : depth;
    public float Distance=>distance;
    public float Heading=>heading;
    public float AngularSpeed=>angularSpeed;
    public int CollisionCount=>collisionCount;
    public bool Contact=>contact;
    public Vector3 WorldPosition=>body!=null ? body.position : transform.position;
    public Vector3 Forward=>new Vector3(Mathf.Sin(heading*Mathf.Deg2Rad),0f,Mathf.Cos(heading*Mathf.Deg2Rad));
    public float LastContactTime { get; private set; }=float.NegativeInfinity;
    private readonly RaycastHit[] hits=new RaycastHit[64];
    private readonly Collider[] overlaps=new Collider[64];
    private bool ready;
    private bool externalContact;
    private bool hasInputFocus=true;
    public void SetExternalContact(bool value,string obstacleName)
    {
        externalContact=value;
        if(value) lastObstacle=obstacleName;
    }

    private void Awake()
    {
        if(body==null) body=GetComponent<Rigidbody>();
        if(hull==null) hull=GetComponent<BoxCollider>();
        ready=body!=null && hull!=null && obstacles!=null && courseOrigin!=null;
        if(!ready) { Debug.LogError("Submarine movement needs the scene's 3D body, hull, course and obstacles.",this); enabled=false; return; }
        heading=Mathf.Repeat(heading,360f);
        body.rotation=Quaternion.Euler(0f,heading-90f,0f);
        RefreshTelemetry();
    }
    private void Update()
    {
        if(!keyboardInput) return;
        diveInput=turnInput=0f;
        if(!hasInputFocus) return;
#if ENABLE_INPUT_SYSTEM
        Keyboard keys=Keyboard.current;
        if(keys==null) return;
        if(keys.rKey.wasPressedThisFrame) gear++;
        if(keys.fKey.wasPressedThisFrame) gear--;
        if(keys.wKey.isPressed) diveInput--;
        if(keys.sKey.isPressed) diveInput++;
        if(keys.aKey.isPressed) turnInput--;
        if(keys.dKey.isPressed) turnInput++;
        if(keys.qKey.wasPressedThisFrame) trim-=trimStep;
        if(keys.eKey.wasPressedThisFrame) trim+=trimStep;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if(Input.GetKeyDown(KeyCode.R)) gear++;
        if(Input.GetKeyDown(KeyCode.F)) gear--;
        if(Input.GetKey(KeyCode.W)) diveInput--;
        if(Input.GetKey(KeyCode.S)) diveInput++;
        if(Input.GetKey(KeyCode.A)) turnInput--;
        if(Input.GetKey(KeyCode.D)) turnInput++;
        if(Input.GetKeyDown(KeyCode.Q)) trim-=trimStep;
        if(Input.GetKeyDown(KeyCode.E)) trim+=trimStep;
#endif
        gear=Mathf.Clamp(gear,0,3); trim=Mathf.Clamp(trim,-maximumTrim,maximumTrim);
    }
    private void OnApplicationFocus(bool focused)
    {
        hasInputFocus=focused;
        if(!focused && keyboardInput) ClearKeyboardSteering();
    }
    private void OnDisable()
    {
        if(keyboardInput) ClearKeyboardSteering();
    }
    private void ClearKeyboardSteering() { diveInput=turnInput=angularSpeed=0f; }
    private void FixedUpdate()=>Simulate(Time.fixedDeltaTime);
    public void Simulate(float dt)
    {
        if(!ready || dt<=0f) return;
        if(resources!=null && (resources.Failed || resources.Won)) { speed=verticalSpeed=angularSpeed=0f; return; }
        gear=Mathf.Clamp(gear,0,3); trim=Mathf.Clamp(trim,-maximumTrim,maximumTrim);
        float target=gear==1 ? lowSpeed : gear==2 ? cruiseSpeed : gear==3 ? highSpeed : 0f;
        if(resources!=null && !resources.HasPower) target=0f;
        speed=Mathf.MoveTowards(speed,Mathf.Max(0f,target),Mathf.Max(0.01f,acceleration)*dt);
        angularSpeed=Mathf.MoveTowards(angularSpeed,Mathf.Clamp(turnInput,-1f,1f)*Mathf.Max(0f,maximumTurnRate),Mathf.Max(0.01f,turnAcceleration)*dt);
        bool turnBlocked=!RotateTo(heading+angularSpeed*dt);
        if(turnBlocked) angularSpeed=0f;
        float verticalTarget=Mathf.Clamp(diveInput,-1f,1f)*maximumVerticalSpeed+naturalSinkSpeed+trim;
        verticalSpeed=Mathf.MoveTowards(verticalSpeed,verticalTarget,Mathf.Max(0.01f,verticalAcceleration)*dt);
        ApplyMotion((Forward*speed+Vector3.down*verticalSpeed)*dt,turnBlocked);
    }
    private bool RotateTo(float target)
    {
        // Heading is the control state. Do not feed physics/Euler readback into steering.
        float current=Mathf.Repeat(heading,360f);
        float delta=Mathf.DeltaAngle(current,target);
        int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(delta)/2f));
        float acceptedHeading=current;
        Quaternion accepted=Quaternion.Euler(0f,current-90f,0f);
        bool clear=true;
        for(int step=1;step<=steps;step++)
        {
            float candidateHeading=current+delta*step/steps;
            Quaternion rotation=Quaternion.Euler(0f,candidateHeading-90f,0f);
            Vector3 centre=body.position+rotation*Vector3.Scale(hull.center,hull.transform.lossyScale);
            int count=Physics.OverlapBoxNonAlloc(centre,HalfSize(),overlaps,rotation,~0,QueryTriggerInteraction.Ignore);
            bool blocked=false;
            for(int i=0;i<count;i++) if(IsObstacle(overlaps[i])) { blocked=true; lastObstacle=overlaps[i].name; break; }
            if(blocked) { clear=false; break; }
            accepted=rotation; acceptedHeading=candidateHeading;
        }
        body.rotation=accepted; heading=Mathf.Repeat(acceptedHeading,360f);
        return clear;
    }
    public void Move(Vector3 displacement) { if(ready) ApplyMotion(displacement,false); }
    private void ApplyMotion(Vector3 displacement,bool turnBlocked)
    {
        Vector3 start=body.position;
        Vector3 position=Sweep(start,Vector3.right*Mathf.Sign(displacement.x),Mathf.Abs(displacement.x),out bool blockedX);
        position=Sweep(position,Vector3.forward*Mathf.Sign(displacement.z),Mathf.Abs(displacement.z),out bool blockedZ);
        position=Sweep(position,Vector3.up*Mathf.Sign(displacement.y),Mathf.Abs(displacement.y),out bool blockedY);
        body.position=position;
        Vector3 moved=position-start; distance+=new Vector2(moved.x,moved.z).magnitude;
        if(blockedX || blockedZ) speed=0f;
        if(blockedY) verticalSpeed=0f;
        bool touched=blockedX || blockedY || blockedZ || turnBlocked || externalContact;
        if(touched && !contact) { collisionCount++; LastContactTime=Time.time; }
        contact=touched; RefreshTelemetry();
    }
    private Vector3 HalfSize()
    {
        Vector3 size=Vector3.Scale(hull.size,hull.transform.lossyScale)*0.5f;
        return new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
    }
    private bool IsObstacle(Collider item)=>item!=null && item!=hull && item.transform.IsChildOf(obstacles);
    private Vector3 Sweep(Vector3 start,Vector3 direction,float amount,out bool blocked)
    {
        blocked=false;
        if(amount<0.000001f) return start;
        Vector3 centre=start+body.rotation*Vector3.Scale(hull.center,hull.transform.lossyScale);
        int count=Physics.BoxCastNonAlloc(centre,HalfSize(),direction,hits,body.rotation,amount+skin,~0,QueryTriggerInteraction.Ignore);
        float allowed=amount;
        for(int i=0;i<count;i++)
        {
            RaycastHit hit=hits[i];
            if(!IsObstacle(hit.collider) || Vector3.Dot(direction,hit.normal)>=-0.001f) continue;
            float travel=Mathf.Max(0f,hit.distance-skin);
            if(travel>=allowed) continue;
            allowed=travel; blocked=true; lastObstacle=hit.collider.name;
        }
        return start+direction*allowed;
    }
    private void RefreshTelemetry()=>depth=-(body.position.y-courseOrigin.position.y);
}
