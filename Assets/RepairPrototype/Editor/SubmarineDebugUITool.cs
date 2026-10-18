using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SubmarineDebugUITool
{
    private static readonly string[] TargetFiles =
    {
        "Assets/Scripts/SubmarineController.cs",

        "Assets/RepairPrototype/Scripts/LeakRepairInteraction.cs",

        "Assets/RepairPrototype/Scripts/CoolingRepairInteraction.cs",

        "Assets/RepairPrototype/Scripts/FusePuzzleInteraction.cs",

        "Assets/RepairPrototype/Scripts/RepairCoolingPrototype.cs",

        "Assets/RepairPrototype/Scripts/RepairFloodPrototype.cs",

        "Assets/RepairPrototype/Scripts/RepairSharedToolbox.cs"
    };

    [MenuItem("Tools/Submarine/添加调试面板开关")]
    private static void AddSwitches()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出播放模式。");
            return;
        }

        // 一字节对应一个字符，保留原脚本的编码和其他内容。
        Encoding encoding = Encoding.GetEncoding(28591);

        Dictionary<string, byte[]> originals =
            new Dictionary<string, byte[]>();

        Dictionary<string, byte[]> updates =
            new Dictionary<string, byte[]>();

        Regex classPattern = new Regex(
            @"public\s+class\s+\w+\s*:\s*MonoBehaviour\s*\{"
        );

        Regex guiPattern = new Regex(
            @"private\s+void\s+OnGUI\s*\(\s*\)\s*\{"
        );

        try
        {
            // 先检查全部文件，通过后才写入。
            foreach (string path in TargetFiles)
            {
                if (!File.Exists(path))
                    throw new Exception("找不到脚本：" + path);

                byte[] bytes = File.ReadAllBytes(path);
                string text = encoding.GetString(bytes);

                if (text.Contains("if (!showDebugUI) return;"))
                    continue;

                if (text.Contains("showDebugUI"))
                {
                    throw new Exception(
                        "脚本已有同名设置，需要先检查：" + path
                    );
                }

                if (classPattern.Matches(text).Count != 1 ||
                    guiPattern.Matches(text).Count != 1)
                {
                    throw new Exception(
                        "脚本结构与预期不同，未进行修改：" + path
                    );
                }

                string newline = text.Contains("\r\n")
                    ? "\r\n"
                    : "\n";

                string field =
                    newline +
                    "    [Header(\"Debug UI\")]" +
                    newline +
                    "    public bool showDebugUI = false;" +
                    newline;

                text = classPattern.Replace(
                    text,
                    match => match.Value + field,
                    1
                );

                string guard =
                    newline +
                    "        if (!showDebugUI) return;" +
                    newline;

                text = guiPattern.Replace(
                    text,
                    match => match.Value + guard,
                    1
                );

                originals.Add(path, bytes);
                updates.Add(path, encoding.GetBytes(text));
            }

            if (updates.Count == 0)
            {
                Debug.Log(
                    "这些脚本已经添加过开关，不需要重复执行。"
                );
                return;
            }

            string backupFolder =
                "Library/DebugUIBackups/" +
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");

            foreach (var entry in originals)
            {
                string backupPath = Path.Combine(
                    backupFolder,
                    entry.Key
                );

                Directory.CreateDirectory(
                    Path.GetDirectoryName(backupPath)
                );

                File.WriteAllBytes(
                    backupPath,
                    entry.Value
                );
            }

            try
            {
                foreach (var entry in updates)
                    File.WriteAllBytes(entry.Key, entry.Value);
            }
            catch
            {
                // 写入失败则恢复本次修改。
                foreach (var entry in originals)
                    File.WriteAllBytes(entry.Key, entry.Value);

                throw;
            }

            AssetDatabase.Refresh();

            Debug.Log(
                "已为 " + updates.Count +
                " 个脚本添加 Show Debug UI 开关，默认关闭。" +
                "\n备份位置：" + backupFolder
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "调试面板设置未完成：" + exception.Message
            );
        }
    }
}