using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// WynnScene owns these settings so the original prefab's simulation stays unchanged.
public class VoyageResources : MonoBehaviour
{
    public SubmarineMovement movement;
    [Header("Resource budgets")]
    [Min(0f)] public float batteryCapacity = 100f;
    [Min(0f)] public float oxygenDuration = 600f;
    [Header("Drain per second")]
    [Min(0f)] public float baseDrain = 0.01f;
    [Min(0f)] public float sonarDrain = 0.03f;
    [Min(0f)] public float pumpDrain = 0.08f;
    public Vector3 propulsionDrain = new Vector3(0.02f, 0.06f, 0.14f);
    [Header("Live controls")]
    public bool keyboardInput = true;
    public bool sonarSwitch = true;
    public bool pumpSwitch;
    [Tooltip("For movement testing without a time limit or battery drain.")]
    public bool freezeResources;
    [Header("Runtime telemetry / debugging")]
    [SerializeField] private float battery;
    [SerializeField] private float oxygen;
    [SerializeField] private bool failed;
    [SerializeField] private bool won;
    public bool HasPower => battery > 0f;
    public bool SonarWorking => HasPower && sonarSwitch;
    public bool Failed => failed;
    public bool Won => won;
    public float Battery => battery;
    public float Oxygen => oxygen;
    public float Drain { get; private set; }

    private void Awake() => ResetResources();

    [ContextMenu("Reset resources")]
    public void ResetResources()
    {
        battery = Mathf.Max(0f, batteryCapacity);
        oxygen = Mathf.Max(0f, oxygenDuration);
        failed = oxygen <= 0f;
        won = false;
    }

    public bool CompleteVoyage()
    {
        if(failed || oxygen<=0f) return false;
        won=true;
        return true;
    }

    private void Update()
    {
        if (keyboardInput)
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.zKey.wasPressedThisFrame) sonarSwitch = !sonarSwitch;
                if (keys.xKey.wasPressedThisFrame) pumpSwitch = !pumpSwitch;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Z)) sonarSwitch = !sonarSwitch;
            if (Input.GetKeyDown(KeyCode.X)) pumpSwitch = !pumpSwitch;
#endif
        }
        int gear = movement == null ? 0 : movement.gear;
        float driveDrain = gear == 1 ? propulsionDrain.x : gear == 2 ? propulsionDrain.y : gear == 3 ? propulsionDrain.z : 0f;
        Drain = HasPower ? Mathf.Max(0f, baseDrain + driveDrain + (sonarSwitch ? sonarDrain : 0f) + (pumpSwitch ? pumpDrain : 0f)) : 0f;
        if (failed || won || freezeResources) return;
        battery = Mathf.Max(0f, battery - Drain * Time.deltaTime);
        oxygen = Mathf.Max(0f, oxygen - Time.deltaTime);
        failed = oxygen <= 0f;
    }
}
