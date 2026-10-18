using UnityEngine;

// Sample positions once per full scan; all obstacle echoes share the same fade.
[ExecuteAlways,DefaultExecutionOrder(100)]
public class CircularSonar : MonoBehaviour
{
    public const int MaximumContacts=64;
    public SonarScreen sonar;
    public OceanSystem ocean;
    public OceanObject[] objects;
    [Min(1f)] public float detectionRadius=100f;
    [Header("Scan timing")]
    [Tooltip("Contact positions refresh once per full sweep. Set Sweep Duration on this animation (seconds).")]
    public SonarSweep scanAnimation;
    [Header("Echo fade")]
    [Tooltip("Seconds to fade fixed obstacles, fish and reefs from full opacity to invisible after each scan. The yellow goal stays visible.")]
    [Min(0.01f)] public float echoFadeDuration=2f;
    [SerializeField] private int visibleEchoes;
    [SerializeField] private float scanElapsed;
    [SerializeField] private float sampledHeading;
    public float ScanInterval=>scanAnimation!=null ? Mathf.Max(0.5f,scanAnimation.sweepDuration) : 2f;
    public float SampledHeading=>sampledHeading;
    public float EchoOpacity=>Application.isPlaying ? Mathf.Clamp01(1f-scanElapsed/Mathf.Max(0.01f,echoFadeDuration)) : 1f;
    public int VisibleEchoes=>visibleEchoes;
    public Vector4[] Points=>points;
    public OceanKind[] Kinds=>kinds;
    private readonly Vector4[] points=new Vector4[MaximumContacts];
    private readonly OceanKind[] kinds=new OceanKind[MaximumContacts];
    private readonly OceanObject[] sampledObjects=new OceanObject[MaximumContacts];
    private readonly uint[] sampledVersions=new uint[MaximumContacts];
    private readonly Vector4[] sampledPoints=new Vector4[MaximumContacts];
    private readonly OceanKind[] sampledKinds=new OceanKind[MaximumContacts];
    private int sampledCount;
    private bool hasSnapshot;

    private void OnEnable()=>ClearEchoes();
    private void LateUpdate()=>RefreshRadar(Application.isPlaying ? Time.deltaTime : 0f);
    [ContextMenu("Clear radar contacts")]
    public void ClearEchoes()
    {
        visibleEchoes=sampledCount=0;
        scanElapsed=0f; hasSnapshot=false;
    }
    [ContextMenu("Collect ocean contacts")]
    public void CollectObstacles()
    {
        if(ocean!=null) objects=ocean.allObjects;
        ClearEchoes();
    }

    public void RefreshRadar(float dt)
    {
        visibleEchoes=0;
        if(sonar==null || sonar.movement==null || objects==null ||
            (sonar.resources!=null && !sonar.resources.SonarWorking))
        {
            ClearEchoes(); return;
        }
        if(!Application.isPlaying || !hasSnapshot)
        {
            CaptureContacts(); scanElapsed=0f;
        }
        else
        {
            scanElapsed+=Mathf.Max(0f,dt);
            float cycles=Mathf.Floor((scanElapsed+0.00001f)/ScanInterval);
            if(cycles>=1f)
            {
                scanElapsed=Mathf.Max(0f,scanElapsed-cycles*ScanInterval);
                CaptureContacts();
            }
        }
        for(int i=0;i<sampledCount;i++)
        {
            OceanObject item=sampledObjects[i];
            // Consumed red points must disappear immediately, between scans too.
            if(item==null || !item.Active || item.ActivationVersion!=sampledVersions[i]) continue;
            float opacity=sampledKinds[i]==OceanKind.Goal ? 1f : EchoOpacity;
            Vector4 point=sampledPoints[i]; point.z=opacity;
            points[visibleEchoes]=point;
            kinds[visibleEchoes]=sampledKinds[i];
            visibleEchoes++;
        }
        if(sonar.statusText!=null)
        {
            if(ocean!=null && ocean.Won) sonar.statusText.text="TARGET REACHED / VOYAGE COMPLETE";
            else if(ocean!=null && ocean.GoalApproaching) sonar.statusText.text="TARGET APPROACHING / NO NEW GIANT REEFS";
            else if(!sonar.movement.Contact) sonar.statusText.text="DARK RED: SOLID / BRIGHT RED: FISH / YELLOW: GOAL";
        }
    }

    private void CaptureContacts()
    {
        sampledCount=0; hasSnapshot=true;
        sampledHeading=sonar.movement.Heading;
        Vector3 origin=sonar.movement.WorldPosition;
        float radius=Mathf.Max(1f,detectionRadius);
        // Large reef regions underneath point contacts, with the yellow objective always last.
        for(int pass=0;pass<3;pass++) foreach(OceanObject item in objects)
        {
            if(item==null || !item.Active || sampledCount>=MaximumContacts) continue;
            int itemPass=item.kind==OceanKind.GiantReef ? 0 : item.kind==OceanKind.Goal ? 2 : 1;
            if(itemPass!=pass) continue;
            Vector3 offset=item.transform.position-origin;
            Vector2 relative=new Vector2(offset.x,offset.z);
            Vector2 normalized=relative/radius;
            float pointRadius=item.radarRadius>0f ? item.radarRadius/radius*0.78f : 0f;
            if(item.kind==OceanKind.Goal)
            {
                Vector2 guideDirection=new Vector2(ocean!=null ? ocean.GoalFallbackDirection.x : 1f,ocean!=null ? ocean.GoalFallbackDirection.z : 0f).normalized;
                // Before the depth gate this is a distant bearing, not a nearby world contact.
                if(ocean==null || !ocean.GoalApproaching) normalized=guideDirection*(0.75f/0.78f);
                else
                {
                    Vector2 direction=relative.sqrMagnitude>0.000001f ? relative.normalized : guideDirection;
                    normalized=direction*(Mathf.Clamp01(relative.magnitude/Mathf.Max(0.001f,ocean.GoalApproachStartDistance))*0.75f/0.78f);
                }
                pointRadius=0.024f;
            }
            else
            {
                bool reef=item.kind==OceanKind.GiantReef;
                // All contacts use planar range, regardless of depth or relative heading.
                // Test the full reef disc, including portions whose center is off-screen.
                float visibleRange=reef ? radius+Mathf.Max(0f,item.radarRadius) : radius*1.0001f;
                if(relative.sqrMagnitude>visibleRange*visibleRange) continue;
            }
            sampledObjects[sampledCount]=item;
            sampledVersions[sampledCount]=item.ActivationVersion;
            sampledPoints[sampledCount]=new Vector4(normalized.x*0.78f,normalized.y*0.78f,1f,pointRadius);
            sampledKinds[sampledCount]=item.kind;
            sampledCount++;
        }
    }
}
