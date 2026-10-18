using UnityEngine;
[ExecuteAlways,DefaultExecutionOrder(200)]
public class SonarSubmarineMarker : SonarVisualLayer
{
    [Range(0.005f,0.05f)] public float dotRadius=0.018f;
    public bool showHeadingArrow=true;
    public Color color=new Color(0.18f,1f,0.62f,1f);
    protected override void WriteProperties(MaterialPropertyBlock block)
    {
        block.SetFloat("_Layer",2f);
        block.SetFloat("_DotRadius",dotRadius);
        // Heading-up: the vessel stays upright; only the chart and echoes rotate.
        block.SetFloat("_Heading",Mathf.PI*0.5f);
        block.SetFloat("_HeadingArrow",showHeadingArrow ? 1f : 0f);
        block.SetColor("_Tint",color);
    }
}
