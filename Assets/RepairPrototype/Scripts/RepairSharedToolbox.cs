using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RepairSharedToolbox : MonoBehaviour
{
    public bool showDebugUI = false;
    public Camera playerCamera;
    public float interactionDistance = 10f;

    [Header("Tool visuals (auto-discovered when empty)")]
    public Transform toolsAssembly;
    public Transform heldToolSource;
    public GameObject heldTool;
    [Tooltip("Camera-local position of the held wrench.")]
    public Vector3 heldToolPosition = new Vector3(0.47f, -0.24f, 0.48f);
    public Vector3 heldToolEulerAngles = new Vector3(10.67f, 21.2f, -54.3f);
    public Vector3 heldToolScale = new Vector3(0.19f, 0.1927881f, 0.02386096f);

    [Header("Held wrench repair motion")]
    [Min(0.05f)] public float repairMotionDuration = 0.55f;
    public float repairTwistAngle = 35f;

    [Header("Wrench pickup and return sounds")]
    public AudioClip wrenchPickupClip;
    public AudioClip wrenchReturnClip;
    [Range(0f, 1f)] public float wrenchSoundVolume = 0.55f;
    private AudioSource wrenchAudioSource;

    private bool repairMotionPlaying;
    private float repairMotionElapsed;
    private Vector3 repairRestPosition;
    private Quaternion repairRestRotation;
    private Vector3 repairRestScale;

    public bool HasTools { get; private set; }
    private bool generatedHeldTool;

    private void Start()
    {
        wrenchAudioSource = gameObject.AddComponent<AudioSource>();
        wrenchAudioSource.playOnAwake = false;
        wrenchAudioSource.loop = false;
        wrenchAudioSource.spatialBlend = 0f;
        if (playerCamera == null) playerCamera = Camera.main;
        FindToolVisuals();
        RefreshVisuals();
    }

    private void FindToolVisuals()
    {
        if (toolsAssembly == null)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    string key = NormalizeName(candidate.name);
                    if (key == "toolesassamble" || key == "toolsassamble" || key == "toolsassemble")
                    { toolsAssembly = candidate; break; }
                }
                if (toolsAssembly != null) break;
            }
        }
        // The container must contain visuals only, never the interaction controller or camera.
        if (toolsAssembly != null && (transform.IsChildOf(toolsAssembly) ||
            (playerCamera != null && playerCamera.transform.IsChildOf(toolsAssembly))))
        {
            Debug.LogWarning("Assign a tools-only container, not the toolbox or camera parent.", this);
            toolsAssembly = null;
        }
        if (heldTool != null)
        {
            if (playerCamera != null) heldTool.transform.SetParent(playerCamera.transform, false);
            ApplyHeldToolTransform();
            PrepareHeldTool(heldTool.transform);
            return;
        }
        if (toolsAssembly == null || playerCamera == null) return;
        if (heldToolSource == null)
        {
            foreach (var candidate in toolsAssembly.GetComponentsInChildren<Transform>(true))
            {
                string key = NormalizeName(candidate.name);
                if ((key == "tool" || key == "toolmodel") &&
                    candidate.GetComponentInChildren<MeshFilter>(true) != null)
                { heldToolSource = candidate; break; }
            }
            // Use the complete first model, not one of its internal mesh parts.
            if (heldToolSource == null)
                foreach (Transform child in toolsAssembly)
                    if (child.GetComponentInChildren<MeshFilter>(true) != null)
                    { heldToolSource = child; break; }
        }
        if (heldToolSource == null) return;
        // Build a visual-only copy: no cloned scripts, colliders or physics bodies.
        heldTool = new GameObject("HeldTool");
        generatedHeldTool = true;
        heldTool.transform.SetParent(playerCamera.transform, false);
        foreach (var source in heldToolSource.GetComponentsInChildren<MeshFilter>(true))
        {
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            if (source.sharedMesh == null || sourceRenderer == null) continue;
            var part = new GameObject(source.name);
            part.transform.SetParent(heldTool.transform, false);
            // Preserve nested model transforms, including non-uniform imported scale.
            // Reproduce the hierarchy rather than decompose matrices with possible shear.
            Transform parent = CopyPath(source.transform.parent, heldToolSource, heldTool.transform);
            part.transform.SetParent(parent, false);
            if (source.transform == heldToolSource)
            {
                part.transform.localPosition = Vector3.zero;
                part.transform.localRotation = Quaternion.identity;
                part.transform.localScale = Vector3.one;
            }
            else
            {
                part.transform.localPosition = source.transform.localPosition;
                part.transform.localRotation = source.transform.localRotation;
                part.transform.localScale = source.transform.localScale;
            }
            part.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
        }
        ApplyHeldToolTransform();
        PrepareHeldTool(heldTool.transform);
    }

    private static Transform CopyPath(Transform source, Transform sourceRoot, Transform destinationRoot)
    {
        if (source == null || source == sourceRoot || !source.IsChildOf(sourceRoot)) return destinationRoot;
        Transform parent = CopyPath(source.parent, sourceRoot, destinationRoot);
        // One copied transform per source path, shared by all meshes underneath it.
        string name = source.name + " (Transform)";
        Transform copy = parent.Find(name);
        if (copy != null) return copy;
        copy = new GameObject(name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        return copy;
    }

    private void ApplyHeldToolTransform()
    {
        heldTool.transform.localPosition = heldToolPosition;
        heldTool.transform.localRotation = Quaternion.Euler(heldToolEulerAngles);
        heldTool.transform.localScale = heldToolScale;
    }
    private static string NormalizeName(string value)
    {
        var result = new StringBuilder();
        foreach (char c in value) if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
        return result.ToString();
    }

    private static void PrepareHeldTool(Transform root)
    {
        foreach (var part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2; // Ignore Raycast
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        { renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; }
    }

    private void Update()
    {
        if (GameMenuController.IsInputBlocked) return;
        bool clicked = false;
        Vector2 position = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        clicked = mouse.leftButton.wasPressedThisFrame;
        position = mouse.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
        clicked = Input.GetMouseButtonDown(0);
        position = Input.mousePosition;
#endif
        if (!clicked || playerCamera == null) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Ray ray = playerCamera.ScreenPointToRay(position);
        if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance) &&
            (hit.transform == transform || hit.transform.IsChildOf(transform))) ToggleTools();
    }

    public void ToggleTools()
    {
        if (toolsAssembly == null || heldTool == null) FindToolVisuals();
        ResetRepairMotion();
        HasTools = !HasTools;
        RefreshVisuals();
        AudioClip clip = HasTools ? wrenchPickupClip : wrenchReturnClip;
        if (clip != null && wrenchAudioSource != null)
        {
            wrenchAudioSource.Stop();
            wrenchAudioSource.PlayOneShot(clip, wrenchSoundVolume);
        }
    }

    private void RefreshVisuals()
    {
        if (toolsAssembly != null) toolsAssembly.gameObject.SetActive(!HasTools);
        if (heldTool != null) heldTool.SetActive(HasTools);
    }

    // Repairs require the wrench, but it stays held until the toolbox is clicked again.
    public bool TryUseTools()
    {
        if (!HasTools) return false;
        PlayRepairMotion();
        return true;
    }

    // Called only at the existing successful repair gate, never for inspection or failed attempts.
    private void PlayRepairMotion()
    {
        if (!isActiveAndEnabled || heldTool == null || !heldTool.activeInHierarchy) return;
        ResetRepairMotion();
        Transform tool = heldTool.transform;
        repairRestPosition = tool.localPosition;
        repairRestRotation = tool.localRotation;
        repairRestScale = tool.localScale;
        repairMotionElapsed = 0f;
        repairMotionPlaying = true;
    }

    private void LateUpdate()
    {
        if (!repairMotionPlaying) return;
        if (!HasTools || heldTool == null || !heldTool.activeInHierarchy)
        { ResetRepairMotion(); return; }

        repairMotionElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(repairMotionElapsed / Mathf.Max(0.05f, repairMotionDuration));
        if (progress >= 1f) { ResetRepairMotion(); return; }
        // One eased tightening stroke, followed by a return to the exact captured pose.
        float twist = Mathf.Sin(Mathf.PI * Mathf.SmoothStep(0f, 1f, progress));
        heldTool.transform.localRotation = repairRestRotation *
            Quaternion.AngleAxis(repairTwistAngle * twist, Vector3.forward);
    }

    private void ResetRepairMotion()
    {
        if (repairMotionPlaying && heldTool != null)
        {
            heldTool.transform.localPosition = repairRestPosition;
            heldTool.transform.localRotation = repairRestRotation;
            heldTool.transform.localScale = repairRestScale;
        }
        repairMotionPlaying = false;
        repairMotionElapsed = 0f;
    }

    private void OnDisable()
    {
        ResetRepairMotion();
        if (wrenchAudioSource != null) wrenchAudioSource.Stop();
    }

    private void OnDestroy()
    {
        if (wrenchAudioSource != null) Destroy(wrenchAudioSource);
        ResetRepairMotion();
        if (generatedHeldTool && heldTool != null) Destroy(heldTool);
    }

    private void OnGUI()
    {
        if (!showDebugUI) return;
        GUI.Box(new Rect(10f, 180f, 330f, 55f),
            HasTools ? "TOOLS: HELD - CLICK TOOLBOX TO RETURN" : "TOOLS: NOT TAKEN - CLICK TOOLBOX");
    }
}