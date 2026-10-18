using UnityEngine;

// Live world positions with independent blink clocks. The scanning beam is visual only.
[ExecuteAlways,DefaultExecutionOrder(100)]
public class CircularSonar : MonoBehaviour
{
    public const int MaximumContacts=64;
    public SonarScreen sonar;
    public OceanSystem ocean;
    public OceanObject[] objects;
    [Min(1f)] public float detectionRadius=100f;
    public bool previewAllContacts=true;
    [SerializeField] private int visibleEchoes;
    public int VisibleEchoes=>visibleEchoes;
    public Vector4[] Points=>points;
    public OceanKind[] Kinds=>kinds;
    private readonly Vector4[] points=new Vector4[MaximumContacts];
    private readonly OceanKind[] kinds=new OceanKind[MaximumContacts];

    private void OnEnable()=>ClearEchoes();
    private void LateUpdate()=>RefreshRadar(Application.isPlaying ? Time.deltaTime : 0f);
    [ContextMenu("Clear radar contacts")]
    public void ClearEchoes()=>visibleEchoes=0;
    [ContextMenu("Collect ocean contacts")]
    public void CollectObstacles()
    {
        if(ocean!=null) objects=ocean.allObjects;
        ClearEchoes();
    }

    public void RefreshRadar(float dt)
    {
        visibleEchoes=0;
        if(sonar==null || sonar.movement==null || objects==null) return;
        if(sonar.resources!=null && !sonar.resources.SonarWorking) return;
        Vector3 origin=sonar.movement.WorldPosition;
        float radius=Mathf.Max(1f,detectionRadius);
        // Large blue regions underneath red contacts, with the yellow objective always last.
        for(int pass=0;pass<3;pass++) foreach(OceanObject item in objects)
        {
            if(item==null || !item.Active || visibleEchoes>=MaximumContacts) continue;
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
                // Test the full blue disc, including portions whose center is off-screen.
                float visibleRange=reef ? radius+Mathf.Max(0f,item.radarRadius) : radius*1.0001f;
                if(relative.sqrMagnitude>visibleRange*visibleRange) continue;
            }
            bool blink=item.BlinkVisible || (!Application.isPlaying && previewAllContacts);
            points[visibleEchoes]=new Vector4(normalized.x*0.78f,normalized.y*0.78f,blink ? 1f : 0f,pointRadius);
            kinds[visibleEchoes]=item.kind;
            visibleEchoes++;
        }
        if(sonar.statusText!=null)
        {
            if(ocean!=null && ocean.Won) sonar.statusText.text="TARGET REACHED / VOYAGE COMPLETE";
            else if(ocean!=null && ocean.GoalApproaching) sonar.statusText.text="TARGET APPROACHING / NO NEW GIANT REEFS";
            else if(!sonar.movement.Contact) sonar.statusText.text="RED: OBSTACLES / BLUE: RISE 20m / YELLOW: GOAL";
        }
    }
}
