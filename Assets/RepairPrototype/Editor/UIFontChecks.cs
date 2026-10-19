#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UIFontChecks
{
    [InitializeOnLoadMethod]
    private static void Schedule()
    {
        if (!SessionState.GetBool("Submarine.UIFontChecks.v1", false))
            EditorApplication.delayCall += Validate;
    }

    [MenuItem("Tools/Submarine/Validate UI font spacing")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var report = new StringBuilder();
        var font = Resources.Load<Font>("MenuFont");
        try
        {
            if (font == null || font.lineHeight <= 0) throw new Exception("Missing embedded MenuFont or invalid font metrics.");
            report.AppendLine("Embedded font: " + font.name + ", lineHeight=" + font.lineHeight);
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GameMenuDefaults.prefab").GetComponent<GameMenuController>();
            var ending = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EndingCreditsDefaults.prefab").GetComponent<RepairEndingCredits>();
            if (menu.font != font || ending.font != font) throw new Exception("Menu and ending prefabs must reference the embedded font.");
            var credits = Resources.Load<TextAsset>("MenuCredits");
            Check(report, font, "Menu title", menu.gameTitle, 68, 1.15f, 560);
            Check(report, font, "Menu credits", credits.text, 26, 1.15f, 1000);
            Check(report, font, "Ending title", ending.gameTitle, 76, 1.3f, 1000);
            Check(report, font, "Ending crew", ending.teamCredits, 30, 1.3f, 1000);
            Check(report, font, "Result statistics", "TIME 02:30\nDISTANCE 800 m\nHITS 2\nHULL 33%\nOXYGEN 3\nBATTERIES 1", 30, 1.3f, 1000);
            report.AppendLine("PASS: embedded references and multiline spacing.");
            SessionState.SetBool("Submarine.UIFontChecks.v1", true);
        }
        catch (Exception error) { report.AppendLine("FAIL: " + error.Message); Debug.LogError(report.ToString()); }
        Directory.CreateDirectory("Temp");
        File.WriteAllText("Temp/UIFontChecks.txt", report.ToString());
    }

    private static void Check(StringBuilder report, Font font, string name, string value, int size, float spacing, float width)
    {
        var root = new GameObject("Font spacing check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        root.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var text = root.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.lineSpacing = spacing;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var settings = text.GetGenerationSettings(new Vector2(width, 10000));
            var generator = text.cachedTextGenerator;
            if (!generator.Populate(value, settings)) throw new Exception(name + ": text generation failed.");
            var lines = generator.lines;
            for (int i = 1; i < lines.Count; i++)
                if (Mathf.Abs(lines[i].topY - lines[i - 1].topY) < size * 0.85f)
                    throw new Exception(name + ": overlapping line metrics at line " + i);
            report.AppendLine(name + ": " + lines.Count + " lines, PASS");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
