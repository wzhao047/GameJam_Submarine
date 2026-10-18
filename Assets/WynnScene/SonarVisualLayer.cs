using UnityEngine;

// Every visual has its own scene-authored renderer and Inspector settings.
[ExecuteAlways]
public abstract class SonarVisualLayer : MonoBehaviour
{
    public SonarScreen sonar;
    public MeshRenderer surface;
    public bool visible=true;
    protected MaterialPropertyBlock properties;
    protected virtual void OnEnable()=>RefreshVisual();
    protected virtual void LateUpdate()=>RefreshVisual();
    protected virtual void OnDisable() { if(surface!=null) surface.enabled=false; }
    public void RefreshVisual()
    {
        if(surface==null) return;
        bool powered=sonar==null || sonar.resources==null || sonar.resources.SonarWorking;
        surface.enabled=visible && powered;
        if(!surface.enabled) return;
        if(properties==null) properties=new MaterialPropertyBlock();
        surface.GetPropertyBlock(properties);
        properties.SetFloat("_Aspect",Mathf.Abs(surface.transform.lossyScale.x/Mathf.Max(0.001f,Mathf.Abs(surface.transform.lossyScale.y))));
        WriteProperties(properties);
        surface.SetPropertyBlock(properties);
    }
    protected abstract void WriteProperties(MaterialPropertyBlock block);
}
