#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Explicit opt-in editor integration checks. Never included in a player build.
[InitializeOnLoad]
public static class VoyageModeSmokeTests
{
    private const string Request = "Temp/VoyageModeSmoke.request";
    private const string Report = "Temp/VoyageModeSmoke.txt";
    private const string Key = "Submarine.ModeSmoke.";
    private static double waitUntil;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static VoyageModeSmokeTests()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += Changed;
    }

    [MenuItem("Tools/Submarine/Run voyage mode smoke tests")]
    public static void RequestRun() { Directory.CreateDirectory("Temp"); File.WriteAllText(Request, "run"); }
    public static void RunBatch()
    {
        SessionState.SetBool(Key + "Batch", true);
        SessionState.SetBool(Key + "Failed", false);
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        RequestRun();
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            SessionState.SetInt(Key + "Phase", 1);
            waitUntil = EditorApplication.timeSinceStartup + 2;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            int index = SessionState.GetInt(Key + "Index", 0) + 1;
            SessionState.SetInt(Key + "Index", index);
            SessionState.SetInt(Key + "Phase", 0);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!SessionState.GetBool(Key + "Active", false))
        {
            if (!File.Exists(Request)) return;
            // Explicit cleanup for scores created during development of this new feature.
            if (File.ReadAllText(Request).Trim() == "reset-test-records")
            {
                foreach (VoyageMode mode in Enum.GetValues(typeof(VoyageMode)))
                    foreach (string metric in new[] { ".Time", ".Distance", ".Hits", ".Oxygen" })
                        PlayerPrefs.DeleteKey("Voyage.Best." + mode + metric);
                PlayerPrefs.Save();
            }
            File.Delete(Request);
            File.WriteAllText(Report, "Voyage mode integration tests\n");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)
            { File.AppendAllText(Report, "NOT RUN: stop Play Mode and save the current scene, then use Tools/Submarine/Run voyage mode smoke tests.\n"); return; }
            if (EditorSceneManager.GetActiveScene().name != "SampleScene")
            { File.AppendAllText(Report, "NOT RUN: open SampleScene first.\n"); return; }
            SessionState.SetInt(Key + "PreviousMode", (int)VoyageModeSession.Selected);
            SessionState.SetInt(Key + "PreviousTutorial", PlayerPrefs.GetInt(TutorialOverlay.FirstVisitPreferenceKey, -1));
            SessionState.SetInt(Key + "Index", 0);
            SessionState.SetInt(Key + "Phase", 0);
            SessionState.SetBool(Key + "Active", true);
        }
        try
        {
            int index = SessionState.GetInt(Key + "Index", 0);
            if (index >= 3) { Finish(); return; }
            int phase = SessionState.GetInt(Key + "Phase", 0);
            if (phase == 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                VoyageModeSession.Select((VoyageMode)index);
                SessionState.SetInt(Key + "Phase", -1);
                EditorApplication.isPlaying = true;
            }
            else if (phase == 1 && EditorApplication.isPlaying && Time.frameCount > 30 && EditorApplication.timeSinceStartup >= waitUntil)
            {
                var menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
                Assert(menu != null, "menu exists");
                menu.ShowModeSelection();
                var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
                view.Focus();
                SessionState.SetInt(Key + "Phase", 2);
                waitUntil = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 2 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                ScreenCapture.CaptureScreenshot("Temp/VoyageMode-" + index + ".png");
                SessionState.SetInt(Key + "Phase", 4);
                waitUntil = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 4 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                TestMode((VoyageMode)index);
                File.AppendAllText(Report, "PASS " + (VoyageMode)index + "\n");
                SessionState.SetInt(Key + "Phase", 5);
                waitUntil = EditorApplication.timeSinceStartup + 2;
            }
            else if (phase == 5 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                ScreenCapture.CaptureScreenshot("Temp/VoyageResults-" + index + ".png");
                SessionState.SetInt(Key + "Phase", 6);
                waitUntil = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 6 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                if (index == 2)
                {
                    var menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
                    menu.ShowModeSelection();
                    Set(menu, "pendingMode", VoyageMode.Hard);
                    Invoke(menu, "ConfirmModeSelection");
                    SessionState.SetInt(Key + "Phase", 7);
                    waitUntil = EditorApplication.timeSinceStartup + 2;
                    return;
                }
                SessionState.SetInt(Key + "Phase", 3);
                EditorApplication.isPlaying = false;
            }
            else if (phase == 7 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                Assert(VoyageModeSession.Current.Mode == VoyageMode.Hard && !GameMenuController.IsInputBlocked,
                    "confirm switches mode by reloading and starting a fresh voyage");
                var menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
                menu.PauseGame();
                menu.ShowModeSelection();
                Assert(((Text)Get(menu, "modeConfirmation")).text.Contains("RESTART"), "pause mode change explicitly confirms restart");
                SessionState.SetInt(Key + "Phase", 8);
                waitUntil = EditorApplication.timeSinceStartup + 2;
                File.AppendAllText(Report, "PASS confirmed mode switch, fresh voyage and pause confirmation\n");
            }
            else if (phase == 8 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                ScreenCapture.CaptureScreenshot("Temp/VoyagePauseModes.png");
                SessionState.SetInt(Key + "Phase", 9);
                waitUntil = EditorApplication.timeSinceStartup + 1;
            }
            else if (phase == 9 && EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= waitUntil)
            {
                SessionState.SetInt(Key + "Phase", 3);
                EditorApplication.isPlaying = false;
            }
        }
        catch (Exception error)
        {
            File.AppendAllText(Report, "FAIL " + error + "\n");
            SessionState.SetBool(Key + "Failed", true);
            Finish();
        }
    }

    private static void TestMode(VoyageMode expected)
    {
        var mode = VoyageModeSession.Current;
        var menu = UnityEngine.Object.FindAnyObjectByType<GameMenuController>();
        var boat = UnityEngine.Object.FindAnyObjectByType<SubmarineController>();
        var oxygen = UnityEngine.Object.FindAnyObjectByType<SubmarineOxygen>();
        var power = UnityEngine.Object.FindAnyObjectByType<SubmarinePower>();
        var hull = UnityEngine.Object.FindAnyObjectByType<SubmarineHull>();
        var ocean = UnityEngine.Object.FindAnyObjectByType<RepairOceanDirector>();
        var faults = UnityEngine.Object.FindAnyObjectByType<RepairFaultDirector>();
        var result = UnityEngine.Object.FindAnyObjectByType<RepairVoyageResult>();
        Assert(mode != null && mode.Mode == expected, "correct mode applied before scene Start");
        Set(mode, "recordScores", false);
        Assert(Time.timeScale == 0f, "start menu pauses simulation");
        Assert(Mathf.Abs(oxygen.OxygenRemaining - mode.Profile.oxygenSeconds) < 0.01f,
            $"initial oxygen: actual {oxygen.OxygenRemaining}, expected {mode.Profile.oxygenSeconds}, duration {oxygen.oxygenDuration}");
        Assert(EventSystem.current.currentSelectedGameObject != null, "mode selection keyboard focus");
        var panel = (GameObject)Get(menu, "modePanel");
        foreach (var button in panel.GetComponentsInChildren<Button>())
        {
            var rect = button.GetComponent<RectTransform>();
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Assert(corners[0].y >= -1f && corners[2].y <= Screen.height + 1f, "mode buttons fit screen");
        }
        Invoke(menu, "CancelModeSelection");
        Assert(VoyageModeSession.Selected == expected, "cancel preserves mode");
        menu.StartGame();
        Assert(Time.timeScale == 1f && !GameMenuController.IsInputBlocked, "start unblocks gameplay");
        float initial = oxygen.OxygenRemaining;
        Assert(Mathf.Abs(oxygen.AddOxygen(80f) - 80f) < 0.01f && oxygen.OxygenPercentage > 100f, "oxygen overflow fully retained");
        Set(boat, "gear", 2);
        float drain = (float)Invoke(power, "GetTotalDrain");
        Assert(Mathf.Abs(power.batteryCapacity / drain - mode.Profile.batteryMinutes * 60f) < 0.1f, "cruise battery endurance");
        Set(power, "battery", 0f);
        Set(boat, "gear", 1);
        Invoke(boat, "SimulateNavigation", 0f, 0f, 1f);
        Assert(boat.CurrentSpeed > 0f && !power.SonarWorking, "empty battery retains emergency propulsion");
        power.ResetForMode();
        Set(boat, "heading", 170f);
        Invoke(boat, "SimulateNavigation", 0f, 1f, 4f);
        Assert(boat.Heading < -100f, "heading passes 180 degrees");
        ValidateEncounters(ocean, hull, expected);
        if (expected == VoyageMode.Endless)
        {
            Assert(!(bool)Invoke(result, "HasArrived"), "endless cannot win at destination");
            ClearOcean(ocean);
            Assert((bool)Invoke(ocean, "SpawnOxygenCanister", true), "endless battery encounter spawns");
            var battery = Array.Find(ocean.OxygenCanisters, x => x.Active && x.kind == RepairContactKind.Battery);
            Assert(battery != null, "battery has distinct contact kind");
            Set(power, "battery", power.batteryCapacity * 0.25f);
            Vector2 nav = boat.NavigationPosition;
            battery.position = new Vector3(nav.x, boat.Depth, nav.y);
            Invoke(hull, "CheckCollision");
            Assert(Mathf.Abs(power.BatteryPercentage - 50f) < 0.01f && !battery.Active && mode.BatteriesCollected == 1, "battery pickup restores 25 percent once");
            Set(mode, "<Elapsed>k__BackingField", 1800f);
            Invoke(mode, "ApplyStage");
            Assert(mode.Challenge == 1f && mode.MaximumFaults == 2 && ocean.oxygenRestoreSeconds == 15f, "endless progression caps");
        }
        ClearOcean(ocean);
        Set(mode, "<Elapsed>k__BackingField", mode.Profile.faultGrace + 1f);
        for (int i = 0; i < 4; i++) Invoke(faults, "TryTriggerFault");
        int active = (faults.leak.HasFault ? 1 : 0) + (faults.cooling.HasFault ? 1 : 0) + (faults.fuse.HasFault ? 1 : 0);
        Assert(active <= mode.MaximumFaults && !(faults.cooling.HasFault && faults.fuse.HasFault), "fault concurrency and propulsion protection");
        Invoke(hull, "TakeCollisionDamage");
        Invoke(hull, "TakeCollisionDamage");
        Assert(hull.CollisionCount == 1, "one contact cannot immediately remove multiple hull cells");
        Set(hull, "damageProtectionUntil", -1f);
        Invoke(hull, "TakeCollisionDamage");
        Set(hull, "safeSeconds", 90f);
        Invoke(hull, "UpdateRecovery");
        Assert(hull.CollisionCount == (expected == VoyageMode.Easy ? 1 : 2), "easy-only hull recovery");
        if (expected == VoyageMode.Easy)
        {
            Set(hull, "touchingReef", true);
            Set(hull, "safeSeconds", 90f);
            Invoke(hull, "UpdateRecovery");
            Assert(hull.CollisionCount == 1 && (float)Get(hull, "safeSeconds") == 0f, "scraping prevents recovery");
            Set(hull, "touchingReef", false);
        }
        menu.PauseGame();
        float before = mode.Elapsed;
        Invoke(mode, "Update");
        Assert(Time.timeScale == 0f && mode.Elapsed == before, "pause stops voyage clock");
        menu.ResumeGame();
        while (!hull.HasFailed) { Set(hull, "damageProtectionUntil", -1f); Invoke(hull, "TakeCollisionDamage"); }
        Assert(hull.CollisionCount == 3, "third hull damage is fatal");
        Invoke(result, "LateUpdate");
        Assert(RepairEndingCredits.IsVisible && Time.timeScale == 0f && mode.Ended,
            "ending credits display after recording the voyage and pausing gameplay");
        // Screenshots must not accidentally publish test records.
    }

    private static void ValidateEncounters(RepairOceanDirector ocean, SubmarineHull hull, VoyageMode mode)
    {
        UnityEngine.Random.InitState(7123);
        for (int run = 0; run < 100; run++)
        {
            ClearOcean(ocean);
            Assert((bool)Invoke(ocean, "SpawnOxygenCanister", false), "reward encounter generated");
            var list = (IList)Get(ocean, "encounters");
            var group = list[0];
            int exits = (int)Get(group, "exits");
            var walls = (RepairOceanContact[])Get(group, "walls");
            var rewards = (RepairOceanContact[])Get(group, "rewards");
            var center = (Vector3)Get(group, "center");
            Assert(walls.Length >= 4 && walls.Length <= 6, "reef count 4 to 6");
            Assert(rewards.Length == 4 - exits + (walls.Length == 6 ? 1 : 0), "difficulty-based oxygen count");
            float turning = exits == 1 ? 42f : exits == 2 ? 30f : 20f;
            foreach (var wall in walls)
            {
                Vector2 offset = new Vector2(wall.position.x - center.x, wall.position.z - center.z);
                Assert(offset.magnitude - wall.radius - hull.submarineRadius >= turning - 0.01f, "central turning clearance");
            }
            Array.Sort(walls, (a,b) => Mathf.Atan2(a.position.z-center.z,a.position.x-center.x).CompareTo(Mathf.Atan2(b.position.z-center.z,b.position.x-center.x)));
            int openings = 0;
            for (int i=0; i<walls.Length; i++)
            {
                var a = walls[i]; var b = walls[(i+1)%walls.Length];
                float gap = new Vector2(a.position.x-b.position.x,a.position.z-b.position.z).magnitude-a.radius-b.radius;
                if (gap >= 11.99f) openings++;
                else Assert(gap <= 0.01f, "non-exit wall seams overlap");
            }
            Assert(openings == exits, "actual geometry has chosen number of exits");
        }
        File.AppendAllText(Report, "  100 generated reef layouts validated\n");
    }
    private static void ClearOcean(RepairOceanDirector ocean)
    {
        foreach (var pool in new[] { ocean.Reefs, ocean.Rocks, ocean.Fish, ocean.OxygenCanisters })
            foreach (var item in pool) item.Deactivate();
        ((IList)Get(ocean,"encounters")).Clear();
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Finish()
    {
        int previousTutorial = SessionState.GetInt(Key + "PreviousTutorial", -1);
        if (previousTutorial < 0) PlayerPrefs.DeleteKey(TutorialOverlay.FirstVisitPreferenceKey);
        else PlayerPrefs.SetInt(TutorialOverlay.FirstVisitPreferenceKey, previousTutorial);
        VoyageModeSession.Select((VoyageMode)SessionState.GetInt(Key + "PreviousMode", 0));
        VoyageModeSession.AutoStartAfterReload = false;
        SessionState.SetBool(Key + "Active", false);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        };
        File.AppendAllText(Report, "Finished; original mode restored.\n");
        if (SessionState.GetBool(Key + "Batch", false))
            EditorApplication.Exit(SessionState.GetBool(Key + "Failed", false) ? 1 : 0);
    }
}
#endif
