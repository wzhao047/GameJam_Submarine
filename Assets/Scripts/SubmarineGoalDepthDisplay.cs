using TMPro;
using UnityEngine;

/// <summary>The fixed goal readout on the small screen below the depth drums.</summary>
public sealed class SubmarineGoalDepthDisplay : MonoBehaviour
{
    private GameObject screen;

    private void Start()
    {
        var goal = FindAnyObjectByType<SubmarineGoalMarker>();
        var oxygenDisplay = FindAnyObjectByType<SubmarineOxygenDisplay>();
        Canvas oxygenCanvas = oxygenDisplay != null && oxygenDisplay.oxygenText != null
            ? oxygenDisplay.oxygenText.canvas : null;
        Transform screenMesh = oxygenCanvas != null ? oxygenCanvas.transform.parent : null;
        Transform console = screenMesh != null ? screenMesh.parent : null;
        if (goal == null || console == null)
        {
            Debug.LogWarning("Goal depth display requires the goal marker and console oxygen screen.", this);
            return;
        }

        screen = new GameObject("GoalDepthScreen", typeof(RectTransform), typeof(Canvas));
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = oxygenCanvas.worldCamera;
        var rect = screen.GetComponent<RectTransform>();
        rect.SetParent(console, false);
        // Right panel below the depth drums. FBX X is mirrored on Unity import:
        // face x -93.258..-52.758 cm becomes +.93258..+.52758 metres.
        // Its y bounds are 1.13..1.355; leave room for the painted frame.
        rect.localPosition = new Vector3(0.73008f, 1.2425f, 0.158f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * 0.001f;
        rect.sizeDelta = new Vector2(310f, 130f);

        var labelObject = new GameObject("GoalDepthText", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 8f);
        labelRect.offsetMax = new Vector2(-8f, -8f);
        var label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = oxygenDisplay.oxygenText.font;
        label.color = oxygenDisplay.normalColor;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 34f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 20f;
        label.fontSizeMax = 34f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.text = VoyageModeSession.Current != null && VoyageModeSession.Current.Mode == VoyageMode.Endless
            ? "goal depth: -- m" : $"goal depth: {goal.goalDepth:0} m";
    }

    private void OnDestroy()
    {
        if (screen != null) Destroy(screen);
    }
}
