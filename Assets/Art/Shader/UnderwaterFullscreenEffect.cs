using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>Camera-local fullscreen presentation, without editing shared URP renderer assets.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public sealed class UnderwaterFullscreenEffect : MonoBehaviour
{
    [Tooltip("Edit this material to tune noise, hue and distortion, including during Play mode.")]
    public Material effectMaterial;
    [Tooltip("Freeze shader animation to inspect a single frame of noise and distortion.")]
    public bool freezeAnimation;
    [Tooltip("Keep below the station button canvas (sorting order 20).")]
    public int canvasSortingOrder = -100;
    [Range(0.5f, 1f)] public float resolutionScale = 1f;

    [Header("Collision sources (auto-detected when empty)")]
    public SubmarineMovement collisionMovement;
    public SubmarineHull collisionHull;

    private Camera sourceCamera;
    private Camera displayCamera;
    private RenderTexture originalTarget;
    private RenderTexture capture;
    private Material runtimeMaterial;
    private Canvas presentationCanvas;
    private RawImage presentation;
    private float effectTime;
    private float collisionGlitchElapsed = -1f;
    private SubmarineMovement subscribedMovement;
    private static readonly int CollisionElapsedId = Shader.PropertyToID("_CollisionGlitchElapsed");
    private static readonly int CollisionDurationId = Shader.PropertyToID("_CollisionGlitchDuration");

    private void OnEnable()
    {
        sourceCamera = GetComponent<Camera>();
        if (effectMaterial == null || effectMaterial.shader == null || !effectMaterial.shader.isSupported)
        {
            Debug.LogError("Underwater fullscreen: assign a supported effect material.", this);
            enabled = false;
            return;
        }
        originalTarget = sourceCamera.targetTexture;
        if (originalTarget != null)
        {
            Debug.LogError("Underwater fullscreen needs a screen camera without an existing target texture.", this);
            enabled = false;
            return;
        }
        runtimeMaterial = new Material(effectMaterial) { hideFlags = HideFlags.DontSave };
        var canvasObject = new GameObject("Underwater Fullscreen (Runtime)", typeof(RectTransform), typeof(Canvas));
        canvasObject.hideFlags = HideFlags.DontSave;
        canvasObject.transform.SetParent(transform, false);
        presentationCanvas = canvasObject.GetComponent<Canvas>();
        presentationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        presentationCanvas.targetDisplay = sourceCamera.targetDisplay;
        var imageObject = new GameObject("Processed Camera Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(canvasObject.transform, false);
        presentation = imageObject.GetComponent<RawImage>();
        presentation.raycastTarget = false;
        presentation.material = runtimeMaterial;
        var rect = presentation.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // The source renders into a texture, so keep a real screen camera for Display 1.
        // It clears only the background; the overlay above presents the processed image.
        var displayObject = new GameObject("Underwater Display Camera (Runtime)", typeof(Camera));
        displayObject.hideFlags = HideFlags.DontSave;
        displayObject.transform.SetParent(transform, false);
        displayCamera = displayObject.GetComponent<Camera>();
        displayCamera.cullingMask = 0;
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = Color.black;
        displayCamera.allowHDR = false;
        displayCamera.allowMSAA = false;
        displayCamera.useOcclusionCulling = false;
        displayCamera.targetTexture = null;
        var cameraData = displayCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.volumeLayerMask = 0;
        effectTime = 0f;
        collisionGlitchElapsed = -1f;
        BindCollisionSources();
        LateUpdate();
    }

    private void BindCollisionSources()
    {
        if (collisionMovement == null && collisionHull == null)
        {
            collisionHull = FindAnyObjectByType<SubmarineHull>();
            if (collisionHull == null)
                collisionMovement = FindAnyObjectByType<SubmarineMovement>();
        }
        subscribedMovement = collisionMovement;
        if (subscribedMovement != null)
            subscribedMovement.CollisionEntered += OnMovementCollision;
        RepairOceanContact.HitRegistered += OnLegacyContact;
    }

    private void OnMovementCollision(float impactSpeed) => TriggerCollisionGlitch();

    private void OnLegacyContact(RepairOceanContact contact)
    {
        if (collisionHull == null || !collisionHull.isActiveAndEnabled || collisionHull.ocean == null)
            return;
        RepairOceanDirector ocean = collisionHull.ocean;
        if (ContainsContact(ocean.Rocks, contact) || ContainsContact(ocean.Fish, contact)
            || ContainsContact(ocean.Reefs, contact))
            TriggerCollisionGlitch();
    }

    private static bool ContainsContact(RepairOceanContact[] pool, RepairOceanContact contact)
        => pool != null && System.Array.IndexOf(pool, contact) >= 0;

    [ContextMenu("Trigger Collision Glitch")]
    public void TriggerCollisionGlitch()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || runtimeMaterial == null) return;
        // A fresh impact restarts this short burst without changing the random schedule.
        collisionGlitchElapsed = 0f;
    }

    private void LateUpdate()
    {
        if (presentation == null || effectMaterial == null) return;
        // Copy editable asset values into a private material; animation never dirties the asset.
        runtimeMaterial.CopyPropertiesFromMaterial(effectMaterial);
        if (!freezeAnimation)
        {
            effectTime += Time.deltaTime;
            if (collisionGlitchElapsed >= 0f) collisionGlitchElapsed += Time.deltaTime;
        }
        if (collisionGlitchElapsed >= 0f && runtimeMaterial.HasProperty(CollisionDurationId)
            && collisionGlitchElapsed >= Mathf.Max(0f, runtimeMaterial.GetFloat(CollisionDurationId)))
            collisionGlitchElapsed = -1f;
        if (runtimeMaterial.HasProperty(CollisionElapsedId))
            runtimeMaterial.SetFloat(CollisionElapsedId, collisionGlitchElapsed);
        runtimeMaterial.SetFloat("_EffectTime", effectTime);
        presentationCanvas.sortingOrder = canvasSortingOrder;
        presentationCanvas.enabled = sourceCamera.enabled;
        presentationCanvas.targetDisplay = sourceCamera.targetDisplay;
        if (displayCamera != null)
        {
            displayCamera.enabled = sourceCamera.enabled;
            displayCamera.targetDisplay = sourceCamera.targetDisplay;
            displayCamera.depth = sourceCamera.depth + 1f;
        }
        int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * resolutionScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * resolutionScale));
        if (capture != null && capture.width == width && capture.height == height) return;
        ReleaseCapture();
        // LDR target matches the final display image consumed by the UI shader.
        capture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Underwater Fullscreen Capture",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1
        };
        capture.Create();
        sourceCamera.targetTexture = capture;
        presentation.texture = capture;
    }

    private void ReleaseCapture()
    {
        if (capture == null) return;
        if (sourceCamera != null && sourceCamera.targetTexture == capture)
            sourceCamera.targetTexture = originalTarget;
        capture.Release();
        Destroy(capture);
        capture = null;
    }

    private void OnDisable()
    {
        if (subscribedMovement != null)
            subscribedMovement.CollisionEntered -= OnMovementCollision;
        subscribedMovement = null;
        RepairOceanContact.HitRegistered -= OnLegacyContact;
        collisionGlitchElapsed = -1f;
        if (displayCamera != null)
        {
            displayCamera.enabled = false;
            displayCamera.gameObject.SetActive(false);
            Destroy(displayCamera.gameObject);
            displayCamera = null;
        }
        if (presentationCanvas != null)
        {
            presentationCanvas.gameObject.SetActive(false);
            Destroy(presentationCanvas.gameObject);
        }
        ReleaseCapture();
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
        presentation = null;
        presentationCanvas = null;
        runtimeMaterial = null;
    }
}
