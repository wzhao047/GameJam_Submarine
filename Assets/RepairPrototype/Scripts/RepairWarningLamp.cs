using System;
using UnityEngine;

public class RepairWarningLamp : MonoBehaviour
{
    [Header("Repair fault sources")]
    public LeakRepairInteraction leak;
    public CoolingRepairInteraction cooling;
    public FusePuzzleInteraction fuse;

    [Header("LightBody renderer (resolved from the model at startup)")]
    public Renderer lampRenderer;

    [Header("Lamp colors")]
    public Color offColor = new Color(0.12f, 0.12f, 0.12f);
    public Color onColor = Color.red;
    [Header("Emission intensity")]
    public float emissionIntensity = 3f;
    [Header("Warning flashes per second")]
    [Min(0.1f)] public float blinkFrequency = 2f;

    private Material[] lampMaterials;
    private Material[] originalMaterials;
    private bool lastWarning;
    private bool lampLit;
    private float blinkElapsed;

    private void Start()
    {
        // Prefer the new model even when the scene still references the old sphere renderer.
        Renderer lightBody = FindLightBody();
        if (lightBody != null) lampRenderer = lightBody;
        if (lampRenderer == null || lampRenderer.transform == transform ||
            !lampRenderer.transform.IsChildOf(transform) ||
            !string.Equals(lampRenderer.name, "LightBody", StringComparison.OrdinalIgnoreCase) ||
            leak == null || cooling == null || fuse == null)
        {
            Debug.LogError("RepairWarningLamp: assign the model's LightBody Renderer and all three repair systems.", this);
            enabled = false;
            return;
        }

        originalMaterials = lampRenderer.sharedMaterials;
        lampMaterials = new Material[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            if (originalMaterials[i] == null) continue;
            lampMaterials[i] = new Material(originalMaterials[i]);
            lampMaterials[i].EnableKeyword("_EMISSION");
        }
        lampRenderer.sharedMaterials = lampMaterials;
        SetLamp(false);
    }

    private Renderer FindLightBody()
    {
        Transform body = transform.Find("Art_034/LightBody");
        if (body != null && body.TryGetComponent(out Renderer exact)) return exact;
        // FBX names may use ART_034 or an instance suffix; still restrict emission to LightBody.
        foreach (var candidate in GetComponentsInChildren<Transform>(true))
            if (string.Equals(candidate.name, "LightBody", StringComparison.OrdinalIgnoreCase) &&
                candidate.TryGetComponent(out Renderer renderer)) return renderer;
        return null;
    }

    private void Update()
    {
        bool warning = leak.HasFault || cooling.HasFault || fuse.HasFault;
        if (warning != lastWarning)
        {
            lastWarning = warning;
            blinkElapsed = 0f;
        }
        // Each new fault period starts lit; fixing every fault turns it off immediately.
        float period = 1f / Mathf.Max(0.1f, blinkFrequency);
        bool lit = warning && Mathf.Repeat(blinkElapsed, period) < period * 0.5f;
        if (lit != lampLit) SetLamp(lit);
        if (warning) blinkElapsed = Mathf.Repeat(blinkElapsed + Time.deltaTime, period);
    }

    private void OnDisable()
    {
        lastWarning = false;
        blinkElapsed = 0f;
        if (lampMaterials != null) SetLamp(false);
    }
    private void SetLamp(bool warning)
    {
        lampLit = warning;
        Color color = warning ? onColor : offColor;
        Color emission = warning ? onColor * Mathf.Max(0f, emissionIntensity) : Color.black;
        foreach (var material in lampMaterials)
        {
            if (material == null) continue;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission);
        }
    }

    private void OnDestroy()
    {
        if (lampRenderer != null && originalMaterials != null) lampRenderer.sharedMaterials = originalMaterials;
        if (lampMaterials == null) return;
        foreach (var material in lampMaterials) if (material != null) Destroy(material);
    }
}