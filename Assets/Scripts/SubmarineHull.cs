using UnityEngine;

using TMPro;



[DefaultExecutionOrder(50)]
public class SubmarineHull : MonoBehaviour

{

    [Header("Hull display / 船体状态文字")]

    public TMP_Text hullStatus;



    [Header("Camera shake / 镜头震动")]

    public Transform shakeCamera;

    public float shakeDuration = 0.45f;

    public float shakeStrength = 0.035f;

    public float shakeFrequency = 28f;



    [Header("Collision / 碰撞")]

    public float submarineRadius = 3f;



    [Header("Cabin light / 船舱氛围灯")]

    public Light cabinDamageLight;



    [Tooltip("第一次碰撞后的闪烁频率")]

    public float flickerFrequency = 3f;



    [Tooltip("第二次碰撞后，红灯相对原灯的亮度")]

    public float redIntensityMultiplier = 1.5f;



    private SubmarineController controller;

    [Header("Ocean system / 海域系统")]
    public RepairOceanDirector ocean;

    private SubmarinePower power;

    private SubmarineOxygen oxygen;



    private int collisionCount;

    private bool failed;


    private Vector3 previousPosition;



    private Color normalLightColor;

    private float normalLightIntensity;

    private bool normalLightEnabled;



    private float shakeRemaining;

    private float shakeElapsed;

    private Vector3 cameraBasePosition;

    private Vector3 lastAppliedPosition;

    private bool hasCameraOffset;



    public bool HasFailed => failed;

    public int CollisionCount => collisionCount;



    public float HullPercentage =>

        Mathf.Clamp01((3f - collisionCount) / 3f) * 100f;



    private void Awake()

    {

        controller = GetComponent<SubmarineController>();


        power = GetComponent<SubmarinePower>();

        oxygen = GetComponent<SubmarineOxygen>();



        if (shakeCamera == null && Camera.main != null)

            shakeCamera = Camera.main.transform;



        if (cabinDamageLight != null)

        {

            normalLightColor = cabinDamageLight.color;

            normalLightIntensity = cabinDamageLight.intensity;

            normalLightEnabled = cabinDamageLight.enabled;

        }

    }



    private void Start()

    {

        if (controller == null || ocean == null)

        {

            Debug.LogError(
                "SubmarineHull：请绑定 Ocean，并挂在 Submarine 上。",
                this
            );

            enabled = false;

            return;

        }



        previousPosition = GetPosition();

        RefreshStatus();

    }



    private void LateUpdate()

    {

        RestoreCameraOffset();



        if (!failed && controller.isActiveAndEnabled)

            CheckCollision();



        UpdateCameraShake();

        UpdateCabinLight();

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

        if (ocean.isActiveAndEnabled && ocean.Ready)
        {
            CheckPool(ocean.Rocks, currentPosition);
            CheckPool(ocean.Fish, currentPosition);
            CheckPool(ocean.Reefs, currentPosition);
            CheckPool(ocean.OxygenCanisters, currentPosition);
        }

        previousPosition = currentPosition;
    }

    private void CheckPool(
        RepairOceanContact[] pool,
        Vector3 currentPosition)
    {
        foreach (RepairOceanContact item in pool)
        {
            if (failed) return;

            if (item == null || !item.isActiveAndEnabled ||
                !item.CanDamage)
                continue;

            bool touched;

            if (item.kind == RepairContactKind.Reef)
            {
                touched = CrossesReef(
                    previousPosition, currentPosition, item);
            }
            else
            {
                Vector3 previousTarget =
                    item.position - item.velocity * Time.deltaTime;

                Vector3 start = previousPosition - previousTarget;
                Vector3 end = currentPosition - item.position;
                Vector3 movement = end - start;

                float t = movement.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(-Vector3.Dot(start, movement)
                        / movement.sqrMagnitude)
                    : 0f;

                float radius = Mathf.Max(0f, submarineRadius)
                    + Mathf.Max(0f, item.radius);

                touched = (start + movement * t).sqrMagnitude
                    <= radius * radius;
            }

            if (!touched) continue;
            if (item.kind == RepairContactKind.OxygenCanister &&
                (GameMenuController.IsInputBlocked || Time.timeScale <= 0f ||
                 oxygen == null || !oxygen.isActiveAndEnabled || oxygen.HasFailed))
                continue;

            // 每次生成的同一个目标只触发一次反馈。
            item.RegisterHit();

            if (item.kind == RepairContactKind.OxygenCanister)
            {
                if (oxygen != null) oxygen.AddOxygen(ocean.oxygenRestoreSeconds);
                item.Deactivate();
            }
            else if (item.kind == RepairContactKind.Fish)
            {
                // 鱼群只震动，不损伤船体，也不停止潜艇。
                shakeRemaining = Mathf.Max(0f, shakeDuration);
                shakeElapsed = 0f;
            }
            else
            {
                TakeCollisionDamage();
            }
        }
    }

    private bool CrossesReef(
        Vector3 start, Vector3 end, RepairOceanContact reef)
    {
        float hullRadius = Mathf.Max(0f, submarineRadius);
        float top = Mathf.Min(reef.topDepth, reef.bottomDepth)
            - hullRadius;
        float bottom = Mathf.Max(reef.topDepth, reef.bottomDepth)
            + hullRadius;
        Vector3 movement = end - start;
        float enter = 0f;
        float exit = 1f;

        if (Mathf.Abs(movement.y) < 0.000001f)
        {
            if (start.y < top || start.y > bottom) return false;
        }
        else
        {
            float first = (top - start.y) / movement.y;
            float second = (bottom - start.y) / movement.y;
            enter = Mathf.Max(0f, Mathf.Min(first, second));
            exit = Mathf.Min(1f, Mathf.Max(first, second));
            if (enter > exit) return false;
        }

        Vector2 offset = new Vector2(
            start.x - reef.position.x, start.z - reef.position.z);
        Vector2 planar = new Vector2(movement.x, movement.z);

        float t = planar.sqrMagnitude > 0.000001f
            ? Mathf.Clamp(-Vector2.Dot(offset, planar)
                / planar.sqrMagnitude, enter, exit)
            : enter;

        float radius = hullRadius + Mathf.Max(0f, reef.radius);
        return (offset + planar * t).sqrMagnitude <= radius * radius;
    }

    private void TakeCollisionDamage()

    {

        if (failed)

            return;



        collisionCount = Mathf.Min(3, collisionCount + 1);



        shakeRemaining = Mathf.Max(0f, shakeDuration);

        shakeElapsed = 0f;



        if (collisionCount >= 3)

        {

            failed = true;

            controller.enabled = false;



            if (power != null)

                power.enabled = false;



            if (oxygen != null)

                oxygen.enabled = false;

        }

    }



    private void UpdateCabinLight()

    {

        if (cabinDamageLight == null)

            return;



        if (collisionCount == 0)

        {

            cabinDamageLight.enabled = normalLightEnabled;

            cabinDamageLight.color = normalLightColor;

            cabinDamageLight.intensity = normalLightIntensity;

        }

        else if (collisionCount == 1)

        {

            cabinDamageLight.enabled = true;

            cabinDamageLight.color = normalLightColor;



            float wave = Mathf.Sin(

                Time.time * Mathf.PI * 2f

                * Mathf.Max(0.1f, flickerFrequency)

            );



            cabinDamageLight.intensity =

                normalLightIntensity * (wave > 0f ? 1f : 0.1f);

        }

        else if (collisionCount == 2)

        {

            cabinDamageLight.enabled = true;

            cabinDamageLight.color = Color.red;

            cabinDamageLight.intensity =

                normalLightIntensity

                * Mathf.Max(0f, redIntensityMultiplier);

        }

        else

        {

            cabinDamageLight.enabled = false;

        }

    }



    private void RestoreCameraOffset()

    {

        if (!hasCameraOffset || shakeCamera == null)

            return;



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



        float fade =

            shakeRemaining / Mathf.Max(0.01f, shakeDuration);



        float strength =

            Mathf.Max(0f, shakeStrength) * fade * fade;



        float phase = shakeElapsed * shakeFrequency;



        Vector3 offset = new Vector3(

            Mathf.Sin(phase * 1.7f),

            Mathf.Sin(phase * 2.3f + 0.8f),

            Mathf.Sin(phase * 1.3f + 1.6f) * 0.35f

        ) * strength;



        cameraBasePosition = shakeCamera.localPosition;

        offset = shakeCamera.localRotation * offset;



        lastAppliedPosition = cameraBasePosition + offset;

        shakeCamera.localPosition = lastAppliedPosition;

        hasCameraOffset = true;

    }



    private void RefreshStatus()

    {

        if (hullStatus == null)

            return;



        hullStatus.color = collisionCount >= 2

            ? new Color(1f, 0.35f, 0.25f)

            : new Color(0.5f, 1f, 0.65f);



        hullStatus.text = failed

            ? "HULL DESTROYED - VOYAGE FAILED"

            : $"HULL {HullPercentage:F0}% | HITS {collisionCount}/3";

    }



    private void OnDisable()

    {

        RestoreCameraOffset();

        shakeRemaining = 0f;

    }

}
