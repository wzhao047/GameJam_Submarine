using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Discovers the same targets used by the repair click handlers; no scene edits required.
[DefaultExecutionOrder(-100)]
public sealed class RepairHoverOutline : MonoBehaviour
{
    private sealed class Target
    {
        public MonoBehaviour owner;
        public Transform root;
        public Func<Camera> camera;
        public Func<float> distance;
        public Func<bool> available;
    }

    private readonly List<Target> targets = new List<Target>();
    private readonly List<GameObject> shells = new List<GameObject>();
    private readonly List<Mesh> meshes = new List<Mesh>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    [Range(0.1f, 1f)] public float pressedBrightness = 0.65f;

    private sealed class PressedRenderer
    {
        public Renderer renderer;
        public int materialIndex;
        public Material material;
        public MaterialPropertyBlock original;
    }

    private readonly List<PressedRenderer> pressedRenderers = new List<PressedRenderer>();
    private Target pressed;
    private Target hovered;
    private Material material;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallInLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.isLoaded) return;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<RepairHoverOutline>() != null) return;
        var host = new GameObject("Repair Hover Outline");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<RepairHoverOutline>();
    }

    private void Start()
    {
        Shader shader = Resources.Load<Shader>("RepairHoverOutline");
        if (shader == null) { enabled = false; return; }
        material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var item in root.GetComponentsInChildren<LeakRepairInteraction>(true))
            {
                Add(item, item.pipeJoint, () => item.playerCamera, () => item.interactionDistance);
                Add(item, item.valve, () => item.playerCamera, () => item.interactionDistance);
            }
            foreach (var item in root.GetComponentsInChildren<CoolingRepairInteraction>(true))
            {
                Add(item, item.coolingJoint, () => item.playerCamera, () => item.interactionDistance);
                Add(item, item.powerSwitch, () => item.playerCamera, () => item.interactionDistance,
                    () => item.cooling != null);
            }
            foreach (var item in root.GetComponentsInChildren<RepairSharedToolbox>(true))
                Add(item, item.transform, () => item.playerCamera, () => item.interactionDistance);
            foreach (var item in root.GetComponentsInChildren<RepairFileHolderInteraction>(true))
                Add(item, item.transform, () => item.playerCamera, () => item.interactionDistance,
                    () => item.paper != null);
            foreach (var item in root.GetComponentsInChildren<FusePuzzleInteraction>(true))
            {
                Func<bool> canHandle = () => item.HasFault && item.cooling != null && !item.cooling.IsPowered;
                foreach (var fuse in item.fuses)
                    Add(item, fuse, () => item.playerCamera, () => item.interactionDistance, canHandle);
                foreach (var slot in item.slots)
                    Add(item, slot, () => item.playerCamera, () => item.interactionDistance, canHandle);
                Add(item, item.testButton, () => item.playerCamera, () => item.interactionDistance);
            }
        }
    }

    private void Add(MonoBehaviour owner, Transform root, Func<Camera> camera,
        Func<float> distance, Func<bool> available = null)
    {
        if (root != null) targets.Add(new Target {
            owner = owner, root = root, camera = camera, distance = distance, available = available
        });
    }

    // Capture before repair Update handlers move a fuse or open the job card UI.
    private void Update()
    {
        if (GameMenuController.IsInputBlocked) return;
        if (!ReadPointer(out Vector2 pointer, out bool down, out bool held))
        { RestorePressed(); return; }

        if (!held || (pressed != null && (pressed.owner == null ||
            !pressed.owner.isActiveAndEnabled || pressed.root == null ||
            !pressed.root.gameObject.activeInHierarchy))) RestorePressed();

        if (down)
        {
            RestorePressed();
            Press(FindTarget(pointer));
        }
    }

    private static bool ReadPointer(out Vector2 pointer, out bool down, out bool held)
    {
        pointer = default;
        down = held = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        pointer = Mouse.current.position.ReadValue();
        down = Mouse.current.leftButton.wasPressedThisFrame;
        held = Mouse.current.leftButton.isPressed;
        return true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        pointer = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        return true;
#else
        return false;
#endif
    }

    private void LateUpdate()
    {
        // A power switch may swap red/green materials while the mouse is still held.
        // Refresh its tint from the new material instead of retaining the old state's color.
        foreach (var state in pressedRenderers)
        {
            if (state.renderer == null) continue;
            Material[] materials = state.renderer.sharedMaterials;
            if (state.materialIndex >= materials.Length || materials[state.materialIndex] != state.material)
            {
                Target target = pressed;
                RestorePressed();
                Press(target);
                break;
            }
        }
        SetTarget(ReadPointer(out Vector2 pointer, out _, out _) ? FindTarget(pointer) : null);
    }

    private Target FindTarget(Vector2 pointer)
    {
        if (EventSystem.current != null)
        {
            var data = new PointerEventData(EventSystem.current) { position = pointer };
            uiHits.Clear();
            EventSystem.current.RaycastAll(data, uiHits);
            foreach (var hit in uiHits)
                if (hit.module is UnityEngine.UI.GraphicRaycaster) return null;
        }
        foreach (var target in targets)
        {
            if (target.owner == null || !target.owner.isActiveAndEnabled ||
                target.root == null || !target.root.gameObject.activeInHierarchy ||
                (target.available != null && !target.available())) continue;
            Camera camera = target.camera();
            if (camera == null || !camera.isActiveAndEnabled || !camera.pixelRect.Contains(pointer)) continue;
            if (Physics.Raycast(camera.ScreenPointToRay(pointer), out RaycastHit hit, target.distance()) &&
                (hit.transform == target.root || hit.transform.IsChildOf(target.root))) return target;
        }
        return null;
    }

    private void Press(Target target)
    {
        pressed = target;
        if (target == null) return;
        foreach (var renderer in target.root.GetComponentsInChildren<Renderer>())
        {
            // Keep the white hover border white, and do not tint particles or hidden placeholders.
            if (!renderer.enabled || shells.Contains(renderer.gameObject) ||
                (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))) continue;
            var inherited = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(inherited);
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;
                var original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original, i);
                var tinted = new MaterialPropertyBlock();
                // An indexed block overrides the renderer-wide block. Preserve its other properties.
                if (original.isEmpty) renderer.GetPropertyBlock(tinted);
                else renderer.GetPropertyBlock(tinted, i);
                bool changed = DimColor(source, original, inherited, tinted, "_BaseColor");
                changed |= DimColor(source, original, inherited, tinted, "_Color");
                changed |= DimColor(source, original, inherited, tinted, "_EmissionColor");
                if (!changed) continue;
                pressedRenderers.Add(new PressedRenderer {
                    renderer = renderer, materialIndex = i, material = source, original = original
                });
                renderer.SetPropertyBlock(tinted, i);
            }
        }
    }

    private bool DimColor(Material source, MaterialPropertyBlock original,
        MaterialPropertyBlock inherited, MaterialPropertyBlock tinted, string property)
    {
        int id = Shader.PropertyToID(property);
        if (!source.HasProperty(id)) return false;
        // A nonempty indexed block suppresses the entire renderer-wide block.
        Color color = original.HasColor(id) ? original.GetColor(id)
            : original.isEmpty && inherited.HasColor(id) ? inherited.GetColor(id)
            : source.GetColor(id);
        float factor = Mathf.Clamp(pressedBrightness, 0.1f, 1f);
        tinted.SetColor(id, new Color(color.r * factor, color.g * factor, color.b * factor, color.a));
        return true;
    }

    private void RestorePressed()
    {
        foreach (var state in pressedRenderers)
            if (state.renderer != null)
                state.renderer.SetPropertyBlock(state.original.isEmpty ? null : state.original, state.materialIndex);
        pressedRenderers.Clear();
        pressed = null;
    }

    private void OnApplicationFocus(bool focused) { if (!focused) RestorePressed(); }
    private void OnApplicationPause(bool paused) { if (paused) RestorePressed(); }
    private void SetTarget(Target next)
    {
        if (hovered == next) return;
        ClearShells();
        hovered = next;
        if (next == null || material == null) return;
        foreach (var source in next.root.GetComponentsInChildren<MeshRenderer>())
        {
            if (!source.enabled) continue;
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            Mesh mesh = filter.sharedMesh;
            // Smooth split normals for continuous borders on boxes and imported hard edges.
            if (mesh.isReadable)
            {
                mesh = Instantiate(mesh);
                meshes.Add(mesh);
                var sums = new Dictionary<Vector3, Vector3>();
                Vector3[] positions = mesh.vertices;
                Vector3[] normals = mesh.normals;
                if (normals.Length == positions.Length)
                {
                    for (int i = 0; i < positions.Length; i++)
                    {
                        sums.TryGetValue(positions[i], out Vector3 sum);
                        sums[positions[i]] = sum + normals[i];
                    }
                    for (int i = 0; i < normals.Length; i++) normals[i] = sums[positions[i]].normalized;
                    mesh.normals = normals;
                }
            }
            var shell = new GameObject("Hover Outline") { layer = source.gameObject.layer,
                hideFlags = HideFlags.HideAndDontSave };
            shell.transform.SetParent(source.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = shell.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            shells.Add(shell);
        }
    }

    private void ClearShells()
    {
        foreach (var shell in shells) if (shell != null) { shell.SetActive(false); Destroy(shell); }
        shells.Clear();
        foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        meshes.Clear();
    }

    private void OnDisable() { RestorePressed(); ClearShells(); hovered = null; }
    private void OnDestroy() { RestorePressed(); ClearShells(); if (material != null) Destroy(material); }
}