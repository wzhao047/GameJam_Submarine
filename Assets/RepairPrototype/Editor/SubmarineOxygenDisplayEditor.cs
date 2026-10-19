using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SubmarineOxygenDisplay))]
public class SubmarineOxygenDisplayEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        SubmarineOxygenDisplay display = (SubmarineOxygenDisplay)target;
        using (new EditorGUI.DisabledScope(!Application.isPlaying ||
            !display.isActiveAndEnabled || display.oxygenText == null))
        {
            if (GUILayout.Button("Preview Oxygen Pickup (+20s)"))
                display.PreviewPickupFeedback();
        }
        EditorGUILayout.HelpBox(
            "Enter Play Mode, then click to preview. Oxygen is unchanged. " +
            "Copy tuned values back after leaving Play Mode to keep them.",
            MessageType.Info);
    }
}
