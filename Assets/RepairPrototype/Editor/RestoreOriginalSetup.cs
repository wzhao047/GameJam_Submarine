#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time recovery for the abandoned sonar integration experiment.
// Works on the loaded scene, including changes not saved to disk yet.
public static class RestoreOriginalSetup
{
    private const string MenuPath = "Tools/Submarine/Restore Original Sonar";
    private static bool scheduled;

    private static string ReportPath => Path.Combine(
        Path.GetDirectoryName(Application.dataPath),
        "Library", "OriginalSonarRestored-v1.txt");

    [MenuItem(MenuPath)]
    private static void RestoreFromMenu()
    {
        Schedule();
    }

    private static void Schedule()
    {
        if (scheduled) return;
        scheduled = true;
        EditorApplication.delayCall += RestoreWhenReady;
    }

    private static void RestoreWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RestoreWhenReady;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += RestoreWhenReady;
            return;
        }

        scheduled = false;

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogWarning("Restore Original Sonar: return to your scene, then choose " + MenuPath + ". No prefab assets were changed.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Restore original sonar setup");

        var reports = new List<string>();
        bool savedAll = true;

        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;

            RepairSonarPowerLink owner = FindOriginalOwner(scene);
            if (owner == null) continue;

            string backupPath = BackupSavedScene(scene);
            int changes = RestoreScene(scene, owner);
            EditorSceneManager.MarkSceneDirty(scene);

            bool saved = !string.IsNullOrEmpty(scene.path) && EditorSceneManager.SaveScene(scene);
            savedAll &= saved;

            reports.Add(scene.name + ": " + changes + " settings restored; saved=" + saved +
                (backupPath == null ? "" : "; previous saved scene=" + backupPath));
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (reports.Count == 0)
        {
            Debug.LogWarning("Restore Original Sonar: no open scene with your original RepairSystem and sonar was found. Open SampleScene and choose " + MenuPath + ".");
            return;
        }

        string report = DateTime.Now.ToString("O") + "\n" + string.Join("\n", reports);
        if (savedAll)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report);
        }

        Debug.Log("Original sonar restored. Integration objects/interfaces are disabled; repair, result, controls and teammate assets are preserved.\n" + report);
    }

    private static RepairSonarPowerLink FindOriginalOwner(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RepairSonarPowerLink link in root.GetComponentsInChildren<RepairSonarPowerLink>(true))
            {
                if (IsIntegrationObject(link.transform)) continue;
                if (link.gameObject.name != "RepairSystem" || link.power == null || link.sonarScreen == null) continue;

                SubmarineSonar sonar = link.power.GetComponent<SubmarineSonar>();
                if (sonar != null && sonar.sonarScreen == link.sonarScreen) return link;
            }
        }
        return null;
    }

    private static bool IsIntegrationObject(Transform item)
    {
        for (Transform current = item; current != null; current = current.parent)
        {
            string name = current.name;
            if (name == "RepairSonarDisplay" ||
                name.StartsWith("RepairIntegratedGameplay", StringComparison.Ordinal) ||
                name.StartsWith("Submarine_Gameplay", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static int RestoreScene(Scene scene, RepairSonarPowerLink owner)
    {
        int changes = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            {
                string name = item.name;
                if (name == "RepairSonarDisplay" ||
                    name.StartsWith("RepairIntegratedGameplay", StringComparison.Ordinal) ||
                    name.StartsWith("Submarine_Gameplay", StringComparison.Ordinal))
                {
                    SetActive(item.gameObject, false, ref changes);
                }
            }

            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (script == null) continue;
                string type = script.GetType().Name;
                if (type == "SubmarineIntegrationState" || type == "RepairNavigationSource" || type == "RepairIntegratedSonar")
                    SetEnabled(script, false, ref changes);
            }
        }

        GameObject submarine = owner.power.gameObject;
        SetActive(submarine, true, ref changes);
        SetEnabled(submarine.GetComponent<SubmarineController>(), true, ref changes);
        SetEnabled(submarine.GetComponent<SubmarinePower>(), true, ref changes);
        SetEnabled(submarine.GetComponent<SubmarineOxygen>(), true, ref changes);
        SetEnabled(submarine.GetComponent<SubmarineSonar>(), true, ref changes);
        SetEnabled(submarine.GetComponent<SubmarineHull>(), true, ref changes);
        SetEnabled(submarine.GetComponent<SubmarineReadout>(), true, ref changes);
        SetEnabled(owner, true, ref changes);

        for (Transform current = owner.sonarScreen.transform; current != null; current = current.parent)
        {
            SetActive(current.gameObject, true, ref changes);
            if (current.gameObject == submarine) break;
        }
        SetEnabled(owner.sonarScreen, true, ref changes);

        // The original sonar creates its own runtime texture on the next Play.
        if (owner.sonarScreen.texture != null)
        {
            Undo.RecordObject(owner.sonarScreen, "Restore original sonar texture");
            owner.sonarScreen.texture = null;
            if (PrefabUtility.IsPartOfPrefabInstance(owner.sonarScreen))
                PrefabUtility.RecordPrefabInstancePropertyModifications(owner.sonarScreen);
            EditorUtility.SetDirty(owner.sonarScreen);
            changes++;
        }

        SubmarineController controller = submarine.GetComponent<SubmarineController>();
        RepairStationView view = controller != null ? controller.stationView : null;
        if (view != null)
        {
            SetActive(view.gameObject, true, ref changes);
            SetEnabled(view, true, ref changes);
            SetEnabled(view.GetComponent<Camera>(), true, ref changes);
            SetEnabled(view.GetComponent<StationViewSwitcher>(), false, ref changes);
        }
        return changes;
    }

    private static void SetActive(GameObject target, bool value, ref int count)
    {
        if (target == null || target.activeSelf == value) return;
        Undo.RecordObject(target, "Restore original sonar setup");
        target.SetActive(value);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
        count++;
    }

    private static void SetEnabled(Behaviour target, bool value, ref int count)
    {
        if (target == null || target.enabled == value) return;
        Undo.RecordObject(target, "Restore original sonar setup");
        target.enabled = value;
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        EditorUtility.SetDirty(target);
        count++;
    }

    private static string BackupSavedScene(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return null;
        string project = Path.GetDirectoryName(Application.dataPath);
        string source = Path.Combine(project, scene.path);
        if (!File.Exists(source)) return null;

        string folder = Path.Combine(project, "Temp", "OriginalSonarRestoreBackups");
        Directory.CreateDirectory(folder);
        string destination = Path.Combine(folder, scene.name + "-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".unity");
        File.Copy(source, destination, false);
        return destination;
    }
}
#endif
