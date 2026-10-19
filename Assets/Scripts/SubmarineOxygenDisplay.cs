using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SubmarineOxygenDisplay : MonoBehaviour
{
    [Header("已有氧气系统")]
    public SubmarineOxygen oxygen;

    [Header("屏幕文字")]
    public TMP_Text oxygenText;

    [Header("颜色")]
    public Color normalColor =
        new Color(0.5f, 1f, 0.35f);

    public Color warningColor =
        new Color(1f, 0.55f, 0.15f);

    [Header("低氧警告阈值")]
    [Range(0f, 100f)]
    public float warningPercentage = 20f;

    [Header("校准测试：只改变显示")]
    public bool calibrationMode = false;

    [Range(0f, 100f)]
    public float calibrationPercentage = 100f;

    [Header("Pickup feedback")]
    public Color pickupColor = new Color(0.4f, 1f, 1f);
    [Min(0.1f)] public float pickupFeedbackDuration = 2f;
    [Tooltip("Normalized screen position: (0,0) bottom-left, (1,1) top-right.")]
    public Vector2 pickupAnchor = new Vector2(0.5f, 0.8f);
    [Tooltip("Offset in UI reference pixels from the anchor.")]
    public Vector2 pickupOffset = Vector2.zero;
    [Tooltip("How far the notification floats upward in UI reference pixels.")]
    public float pickupRiseDistance = 30f;
    private bool previewingPickup;
    private float pickupTimer;
    private GameObject feedbackRoot;
    private TextMeshProUGUI pickupLabel;
    private int lastPercentage = -1;

    private void Start()
    {
        if (oxygen == null || oxygenText == null)
        {
            Debug.LogError(
                "请连接 Oxygen 和 Oxygen Text。",
                this
            );

            enabled = false;
            return;
        }

        CreatePickupFeedback();
        oxygen.OxygenCollected += ShowPickupFeedback;
        RefreshDisplay();
    }

    private void Update()
    {
        pickupTimer = Mathf.Max(0f, pickupTimer - (previewingPickup ? Time.unscaledDeltaTime : Time.deltaTime));
        AnimatePickupFeedback();
        RefreshDisplay();
    }

    [ContextMenu("Preview Oxygen Pickup (Play Mode)")]
    public void PreviewPickupFeedback()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || oxygenText == null) return;
        if (pickupLabel == null) CreatePickupFeedback();
        ShowPickupFeedback(20f);
        // Calibration also animates while the start or pause menu is open.
        previewingPickup = true;
    }
    private void CreatePickupFeedback()
    {
        feedbackRoot = new GameObject("Oxygen Pickup Feedback", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = feedbackRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = feedbackRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        GameObject labelObject = new GameObject("Pickup Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(feedbackRoot.transform, false);
        pickupLabel = labelObject.GetComponent<TextMeshProUGUI>();
        pickupLabel.font = oxygenText.font;
        pickupLabel.fontSize = 32f;
        pickupLabel.fontStyle = FontStyles.Bold;
        pickupLabel.alignment = TextAlignmentOptions.Center;
        pickupLabel.raycastTarget = false;
        RectTransform rect = pickupLabel.rectTransform;
        rect.anchorMin = rect.anchorMax = pickupAnchor;
        rect.anchoredPosition = pickupOffset;
        rect.sizeDelta = new Vector2(600f, 70f);
        feedbackRoot.SetActive(false);
    }

    private void ShowPickupFeedback(float restored)
    {
        if (!isActiveAndEnabled || pickupLabel == null) return;
        previewingPickup = false;
        pickupTimer = Mathf.Max(0.1f, pickupFeedbackDuration);
        pickupLabel.text = restored > 0.01f
            ? $"OXYGEN +{restored:0.#}s"
            : "OXYGEN FULL";
        feedbackRoot.SetActive(true);
        AnimatePickupFeedback();
    }

    private void AnimatePickupFeedback()
    {
        if (pickupLabel == null) return;
        if (pickupTimer <= 0f)
        {
            feedbackRoot.SetActive(false);
            return;
        }
        float progress = 1f - pickupTimer / Mathf.Max(0.1f, pickupFeedbackDuration);
        float eased = 1f - (1f - progress) * (1f - progress);
        pickupLabel.rectTransform.anchorMin = pickupLabel.rectTransform.anchorMax = pickupAnchor;
        pickupLabel.rectTransform.anchoredPosition = pickupOffset + Vector2.up * (eased * pickupRiseDistance);
        pickupLabel.rectTransform.localScale = Vector3.one *
            (1f + 0.15f * Mathf.Clamp01(1f - progress * 5f));
        Color tint = pickupColor;
        tint.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, progress));
        pickupLabel.color = tint;
    }

    private void OnDisable()
    {
        pickupTimer = 0f;
        if (feedbackRoot != null) feedbackRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (oxygen != null) oxygen.OxygenCollected -= ShowPickupFeedback;
        if (feedbackRoot != null) Destroy(feedbackRoot);
    }
    private void RefreshDisplay()
    {
        float percentage = calibrationMode
            ? calibrationPercentage
            : oxygen.OxygenPercentage;

        percentage = Mathf.Clamp(percentage, 0f, 100f);

        int displayedPercentage =
            Mathf.CeilToInt(percentage);

        if (displayedPercentage != lastPercentage)
        {
            oxygenText.text =
                $"O2 {displayedPercentage}%";

            lastPercentage = displayedPercentage;
        }

        oxygenText.color =
            percentage <= warningPercentage
                ? warningColor
                : normalColor;

        if (pickupTimer > 0f)
        {
            float elapsed = Mathf.Max(0.1f, pickupFeedbackDuration) - pickupTimer;
            float flash = Mathf.Clamp01(1f - elapsed / 0.8f);
            oxygenText.color = Color.Lerp(oxygenText.color, pickupColor, flash);
        }
    }
}