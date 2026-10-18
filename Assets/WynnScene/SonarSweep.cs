using UnityEngine;
[ExecuteAlways,DefaultExecutionOrder(50)]
public class SonarSweep : SonarVisualLayer
{
    [Min(0.5f)] public float sweepDuration=4f;
    public bool clockwise=true;
    [Range(5f,100f)] public float trailDegrees=40f;
    [Range(0f,360f)] public float previewAngle=35f;
    public Color color=new Color(0.04f,0.85f,0.58f,0.9f);
    [SerializeField] private float scanAngle;
    public float ScanAngle=>Application.isPlaying ? scanAngle : previewAngle;
    public float LastStartAngle { get; private set; }
    public float LastSweepDegrees { get; private set; }
    protected override void OnEnable() { scanAngle=previewAngle; base.OnEnable(); }
    protected override void LateUpdate()
    {
        AdvanceScan(Application.isPlaying ? Time.deltaTime : 0f);
        base.LateUpdate();
    }
    public void AdvanceScan(float dt)
    {
        LastStartAngle=scanAngle;
        bool powered=sonar==null || sonar.resources==null || sonar.resources.SonarWorking;
        LastSweepDegrees=powered && enabled ? Mathf.Max(0f,dt)*360f/Mathf.Max(0.5f,sweepDuration)*(clockwise ? -1f : 1f) : 0f;
        scanAngle=Mathf.Repeat(scanAngle+LastSweepDegrees,360f);
    }
    protected override void WriteProperties(MaterialPropertyBlock block)
    {
        block.SetFloat("_Layer",1f);
        block.SetFloat("_ScanAngle",ScanAngle*Mathf.Deg2Rad);
        block.SetFloat("_TrailAngle",trailDegrees*Mathf.Deg2Rad);
        block.SetFloat("_SweepDirection",clockwise ? -1f : 1f);
        block.SetColor("_Tint",color);
    }
}
