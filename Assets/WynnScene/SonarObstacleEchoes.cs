using UnityEngine;
[ExecuteAlways,DefaultExecutionOrder(200)]
public class SonarObstacleEchoes : SonarVisualLayer
{
    public CircularSonar contacts;
    [Range(0.005f,0.05f)] public float dotRadius=0.018f;
    [InspectorName("Fixed Obstacle And Reef Color")]
    public Color color=new Color(0.45f,0.012f,0.008f,1f);
    public Color fishSchoolColor=new Color(1f,0.04f,0.025f,1f);
    public Color goalColor=new Color(1f,0.85f,0.04f,1f);
    private readonly Vector4[] colors=new Vector4[CircularSonar.MaximumContacts];
    protected override void WriteProperties(MaterialPropertyBlock block)
    {
        block.SetFloat("_Layer",3f);
        block.SetFloat("_MapRotation",(contacts!=null ? contacts.SampledHeading : sonar!=null && sonar.movement!=null ? sonar.movement.Heading : 0f)*Mathf.Deg2Rad);
        block.SetFloat("_DotRadius",dotRadius);
        block.SetFloat("_EchoCount",contacts!=null ? contacts.VisibleEchoes : 0f);
        if(contacts!=null)
        {
            block.SetVectorArray("_Echoes",contacts.Points);
            for(int i=0;i<contacts.VisibleEchoes;i++)
            {
                OceanKind kind=contacts.Kinds[i];
                Color tint=kind==OceanKind.Goal ? goalColor : kind==OceanKind.FishSchool ? fishSchoolColor : color;
                colors[i]=new Vector4(tint.r,tint.g,tint.b,(float)kind);
            }
            block.SetVectorArray("_EchoColors",colors);
        }
        block.SetColor("_Tint",Color.white);
    }
}
