using UnityEngine;
[ExecuteAlways]
public class SonarMap : SonarVisualLayer
{
    [Range(1,8)] public int ringCount=4;
    public bool showCrosshair=true;
    public Color color=new Color(0.04f,0.65f,0.45f,0.55f);
    protected override void WriteProperties(MaterialPropertyBlock block)
    {
        block.SetFloat("_Layer",0f);
        block.SetFloat("_MapRotation",0f); // Keep range rings and crosshair fixed; only echoes follow heading.
        block.SetFloat("_RingCount",ringCount);
        block.SetFloat("_Crosshair",showCrosshair ? 1f : 0f);
        block.SetColor("_Tint",color);
    }
}
