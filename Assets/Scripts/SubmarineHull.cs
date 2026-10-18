using UnityEngine;
using TMPro;

public class SubmarineHull : MonoBehaviour
{
    [Header("船体状态文字")]
    public TMP_Text hullStatus;

    [Header("需要震动的摄像机")]
    public Transform shakeCamera;

    [Header("船体参数")]
    public float maximumHull = 100f;
    public float submarineRadius = 3f;

    [Header("碰撞伤害")]
    public float baseCollisionDamage = 10f;
    public float damagePerSpeed = 4f;

    [Header("碰撞震动")]
    [Tooltip("震动持续时间，单位为秒")]
    public float shakeDuration = 0.45f;

    [Tooltip("震动位移，单位为米")]
    public float shakeStrength = 0.035f;

    [Tooltip("震动频率")]
    public float shakeFrequency = 28f;

    private SubmarineController controller;
    private SubmarineSonar sonar;
    private SubmarinePower power;
    private SubmarineOxygen oxygen;

    private float hull;
    private bool wasTouching;
    private bool failed;

    public bool HasFailed => failed;

    public float HullPercentage =>
        hull / Mathf.Max(1f, maximumHull) * 100f;

    private Vector3 previousPosition;

    private float shakeRemaining;
    private float shakeElapsed;

    private Vector3 cameraBasePosition;
    private Vector3 lastAppliedPosition;
    private bool hasCameraOffset;

    private void Awake()
    {
        controller = GetComponent<SubmarineController>();
        sonar = GetComponent<SubmarineSonar>();
        power = GetComponent<SubmarinePower>();
        oxygen = GetComponent<SubmarineOxygen>();

        hull = Mathf.Max(1f, maximumHull);

        if (shakeCamera == null && Camera.main != null)
            shakeCamera = Camera.main.transform;
    }

    private void Start()
    {
        if (controller == null || sonar == null || hullStatus == null)
        {
            Debug.LogError(
                "请把 SubmarineHull 挂到 Submarine，并连接 HullStatus。",
                this
            );

            enabled = false;
            return;
        }

        previousPosition = GetPosition();

        // 清除旧版碰撞可能留下的红色。
        if (sonar.sonarScreen != null)
            sonar.sonarScreen.color = Color.white;

        RefreshStatus();
    }

    private void LateUpdate()
    {
        // 先去掉上一帧震动，再叠加本帧震动，避免相机漂移。
        RestoreCameraOffset();

        if (!failed && controller.enabled)
            CheckCollision();

        UpdateCameraShake();
        RefreshStatus();
    }

    private Vector3 GetPosition()
    {
        Vector2 position = controller.NavigationPosition;

        return new Vector3(
            position.x,
            controller.Depth,
            position.y
        );
    }

    private void CheckCollision()
    {
        Vector3 currentPosition = GetPosition();

        Vector3 obstaclePosition = new Vector3(
            sonar.obstacleLateral,
            sonar.obstacleDepth,
            sonar.obstacleForward
        );

        float combinedRadius =
            Mathf.Max(0f, submarineRadius)
            + Mathf.Max(0f, sonar.obstacleRadius);

        Vector3 movement = currentPosition - previousPosition;

        float t = movement.sqrMagnitude > 0.000001f
            ? Mathf.Clamp01(
                Vector3.Dot(
                    obstaclePosition - previousPosition,
                    movement
                ) / movement.sqrMagnitude
            )
            : 0f;

        Vector3 closestPoint = previousPosition + movement * t;

        bool crossedObstacle =
            Vector3.Distance(closestPoint, obstaclePosition)
            <= combinedRadius;

        bool touchingNow =
            Vector3.Distance(currentPosition, obstaclePosition)
            <= combinedRadius;

        if (crossedObstacle && !wasTouching)
            TakeCollisionDamage();

        wasTouching = touchingNow;
        previousPosition = currentPosition;
    }

    private void TakeCollisionDamage()
    {
        float damage =
            Mathf.Max(0f, baseCollisionDamage)
            + controller.CurrentSpeed * Mathf.Max(0f, damagePerSpeed);

        hull = Mathf.Max(0f, hull - damage);

        shakeRemaining = Mathf.Max(0f, shakeDuration);
        shakeElapsed = 0f;

        if (hull <= 0f)
        {
            failed = true;
            controller.enabled = false;

            if (power != null)
                power.enabled = false;

            if (oxygen != null)
                oxygen.enabled = false;
        }
    }

    private void RestoreCameraOffset()
    {
        if (!hasCameraOffset || shakeCamera == null)
            return;

        // 如果视角切换脚本已经移动了相机，
        // 就保留新位置，不恢复到旧的操作台。
        if ((shakeCamera.localPosition - lastAppliedPosition)
            .sqrMagnitude < 0.00000001f)
        {
            shakeCamera.localPosition = cameraBasePosition;
        }
        else
        {
            shakeRemaining = 0f;
        }

        hasCameraOffset = false;
    }

    private void UpdateCameraShake()
    {
        if (shakeCamera == null || shakeRemaining <= 0f)
            return;

        shakeElapsed += Time.deltaTime;
        shakeRemaining = Mathf.Max(
            0f,
            shakeRemaining - Time.deltaTime
        );

        float fade = shakeRemaining
            / Mathf.Max(0.01f, shakeDuration);

        // 后半段逐渐减弱，让震动自然停下。
        float strength = Mathf.Max(0f, shakeStrength) * fade * fade;
        float phase = shakeElapsed * shakeFrequency;

        Vector3 offset = new Vector3(
            Mathf.Sin(phase * 1.7f),
            Mathf.Sin(phase * 2.3f + 0.8f),
            Mathf.Sin(phase * 1.3f + 1.6f) * 0.35f
        ) * strength;

        cameraBasePosition = shakeCamera.localPosition;

        // 将震动方向转换到相机自身的左右、上下方向。
        offset = shakeCamera.localRotation * offset;

        lastAppliedPosition = cameraBasePosition + offset;
        shakeCamera.localPosition = lastAppliedPosition;
        hasCameraOffset = true;
    }

    private void RefreshStatus()
    {
        if (hullStatus == null)
            return;

        if (failed)
        {
            hullStatus.color = new Color(1f, 0.65f, 0.3f);
            hullStatus.text = "HULL DESTROYED - VOYAGE FAILED";
            return;
        }

        float percentage =
            hull / Mathf.Max(1f, maximumHull) * 100f;

        hullStatus.color = percentage <= 30f
            ? new Color(1f, 0.65f, 0.3f)
            : new Color(0.5f, 1f, 0.65f);

        hullStatus.text = $"HULL {percentage:F0}%";
    }

    private void OnDisable()
    {
        RestoreCameraOffset();
        shakeRemaining = 0f;
    }
}