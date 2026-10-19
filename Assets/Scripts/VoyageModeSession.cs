using System;
using UnityEngine;

public enum VoyageMode { Easy, Hard, Endless }

[Serializable]
public sealed class VoyageModeProfile
{
    public float oxygenSeconds, oxygenPickupSeconds, batteryMinutes, goalCruiseSeconds;
    public float rockInterval, fishInterval, reefInterval, oxygenInterval;
    public float faultGrace, faultMinimum, faultMaximum, repairCooldown;
    public int maxFaults;
    public float healSeconds;
}

[Serializable]
public sealed class VoyageModeSettings
{
    public VoyageModeProfile easy, hard, endless;
}

// Created by the menu before scene Start methods; scene references remain authored in Unity.
[DefaultExecutionOrder(-200)]
public sealed class VoyageModeSession : MonoBehaviour
{
    public static VoyageModeSession Current { get; private set; }
    public static bool AutoStartAfterReload;
    public static VoyageMode Selected => (VoyageMode)Mathf.Clamp(PlayerPrefs.GetInt("Voyage.Mode", 0), 0, 2);
    public VoyageMode Mode { get; private set; }
    public VoyageModeProfile Profile { get; private set; }
    public float Elapsed { get; private set; }
    public bool Ended { get; private set; }
    public int OxygenCollected { get; private set; }
    public int BatteriesCollected { get; private set; }
    public float Challenge => Mode == VoyageMode.Endless ? Mathf.Min(5, Mathf.Floor(Elapsed / 180f)) / 5f : Mode == VoyageMode.Hard ? 1f : 0f;
    private VoyageModeSettings settings;
    private SubmarineController boat;
    private SubmarineOxygen oxygen;
    private SubmarinePower power;
    private RepairOceanDirector ocean;
    private RepairFaultDirector faults;
    private SubmarineHull hull;
    private int stage = -1;
    private bool recordScores = true;
    private AudioSource repairSource;
    private AudioClip repairTone;

    public static string Name(VoyageMode mode) => mode == VoyageMode.Easy ? "EASY" : mode == VoyageMode.Hard ? "HARD" : "ENDLESS";
    public static void Select(VoyageMode mode)
    {
        PlayerPrefs.SetInt("Voyage.Mode", (int)mode);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        Current = this;
        Mode = Selected;
        var asset = Resources.Load<TextAsset>("VoyageModes");
        if (asset == null) { Debug.LogError("Missing Resources/VoyageModes.json"); enabled = false; return; }
        settings = JsonUtility.FromJson<VoyageModeSettings>(asset.text);
        Profile = Mode == VoyageMode.Easy ? settings.easy : Mode == VoyageMode.Hard ? settings.hard : settings.endless;
        boat = FindAnyObjectByType<SubmarineController>();
        oxygen = FindAnyObjectByType<SubmarineOxygen>();
        power = FindAnyObjectByType<SubmarinePower>();
        ocean = FindAnyObjectByType<RepairOceanDirector>();
        faults = FindAnyObjectByType<RepairFaultDirector>();
        hull = FindAnyObjectByType<SubmarineHull>();
        if (boat == null || oxygen == null || power == null || ocean == null || faults == null || hull == null)
        { Debug.LogError("Voyage modes require the SampleScene gameplay systems."); enabled = false; return; }
        oxygen.ResetForMode(Profile.oxygenSeconds);
        oxygen.OxygenCollected += CountOxygen;
        float drain = power.batteryCapacity / (Profile.batteryMinutes * 60f);
        power.baseDrain = drain * 0.1f;
        power.sonarDrain = drain * 0.3f;
        power.lowGearDrain = drain * 0.25f;
        power.cruiseGearDrain = drain * 0.6f;
        power.highGearDrain = drain * 1.1f;
        power.ResetForMode();
        faults.firstFaultDelay = new Vector2(Profile.faultGrace, Profile.faultGrace + 15f);
        var goal = FindAnyObjectByType<SubmarineGoalMarker>();
        if (goal != null && Mode != VoyageMode.Endless)
        {
            goal.goalLateral = 0f;
            goal.goalForward = boat.cruiseSpeed * Profile.goalCruiseSeconds;
        }
        ApplyStage();
        repairSource = gameObject.AddComponent<AudioSource>();
        repairSource.playOnAwake = false;
        repairSource.spatialBlend = 0f;
        const int sampleRate = 16000;
        var samples = new float[4800];
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            samples[i] = Mathf.Sin(2f * Mathf.PI * (t < 0.15f ? 660f : 880f) * t)
                * Mathf.Sin(Mathf.PI * i / samples.Length) * 0.1f;
        }
        repairTone = AudioClip.Create("Hull recovery", samples.Length, 1, sampleRate, false);
        repairTone.SetData(samples, 0);
    }

    private void Update()
    {
        if (!enabled || Ended || GameMenuController.IsInputBlocked || !boat.isActiveAndEnabled || oxygen.HasFailed) return;
        Elapsed += Time.deltaTime;
        int next = Mode == VoyageMode.Endless ? Mathf.Min(5, Mathf.FloorToInt(Elapsed / 180f)) : 0;
        if (next != stage) ApplyStage();
    }

    private void ApplyStage()
    {
        stage = Mode == VoyageMode.Endless ? Mathf.Min(5, Mathf.FloorToInt(Elapsed / 180f)) : 0;
        float t = Mode == VoyageMode.Endless ? Challenge : 0f;
        ocean.rockInterval = Mathf.Lerp(Profile.rockInterval, settings.hard.rockInterval, t);
        ocean.fishInterval = Mathf.Lerp(Profile.fishInterval, settings.hard.fishInterval, t);
        ocean.reefInterval = Mathf.Lerp(Profile.reefInterval, settings.hard.reefInterval, t);
        ocean.oxygenSpawnInterval = Mathf.Lerp(Profile.oxygenInterval, settings.hard.oxygenInterval, t);
        ocean.oxygenRestoreSeconds = Mode == VoyageMode.Endless ? Mathf.Lerp(25f, 15f, t) : Profile.oxygenPickupSeconds;
        faults.eventInterval = new Vector2(Mathf.Lerp(Profile.faultMinimum, settings.hard.faultMinimum, t),
            Mathf.Lerp(Profile.faultMaximum, settings.hard.faultMaximum, t));
        faults.repairedSystemCooldown = Mathf.Lerp(Profile.repairCooldown, settings.hard.repairCooldown, t);
    }

    public int MaximumFaults => Mode == VoyageMode.Endless ? (Elapsed >= 540f ? 2 : 1) : Profile.maxFaults;
    public bool FaultSafeToStart => Time.time - hull.LastDamageTime >= 8f;
    public int ChooseExitCount()
    {
        float roll = UnityEngine.Random.value;
        float one = Mathf.Lerp(0.05f, 0.45f, Challenge);
        float two = Mathf.Lerp(0.25f, 0.4f, Challenge);
        return roll < one ? 1 : roll < one + two ? 2 : 3;
    }
    private void CountOxygen(float restored) { if (restored > 0f) OxygenCollected++; }
    public void CollectedBattery() { BatteriesCollected++; }
    public void ReportHullRecovery()
    {
        repairSource.PlayOneShot(repairTone);
    }

    public string Finish(bool success)
    {
        Ended = true;
        string prefix = "Voyage.Best." + Mode;
        float distance = boat.DistanceTravelled;
        if (recordScores && Mode == VoyageMode.Endless)
        {
            if (Elapsed > PlayerPrefs.GetFloat(prefix + ".Time", 0f))
            {
                PlayerPrefs.SetFloat(prefix + ".Time", Elapsed);
                PlayerPrefs.SetFloat(prefix + ".Distance", distance);
                PlayerPrefs.SetInt(prefix + ".Oxygen", OxygenCollected);
            }
        }
        else if (recordScores && success && (PlayerPrefs.GetFloat(prefix + ".Time", 0f) <= 0f || Elapsed < PlayerPrefs.GetFloat(prefix + ".Time")))
        {
            PlayerPrefs.SetFloat(prefix + ".Time", Elapsed);
            PlayerPrefs.SetInt(prefix + ".Hits", hull.TotalCollisions);
        }
        if (recordScores) PlayerPrefs.Save();
        float best = PlayerPrefs.GetFloat(prefix + ".Time", 0f);
        return Name(Mode) + $"\nTIME {Mathf.FloorToInt(Elapsed / 60f):0}:{(int)Elapsed % 60:00}   DISTANCE {distance:0} m"
            + $"\nHITS {hull.TotalCollisions}   HULL {hull.HullPercentage:0}%"
            + $"\nOXYGEN {OxygenCollected}   BATTERIES {BatteriesCollected}"
            + (best > 0f ? $"\nBEST {Mathf.FloorToInt(best / 60f):0}:{(int)best % 60:00}" : "");
    }
    private void OnDestroy()
    {
        if (oxygen != null) oxygen.OxygenCollected -= CountOxygen;
        if (repairTone != null) Destroy(repairTone);
        if (Current == this) Current = null;
    }
}
