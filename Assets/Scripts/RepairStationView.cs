using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class RepairStationView : MonoBehaviour
{
    [Header("三个视角定位点")]
    public Transform powerView;
    public Transform drivingView;
    public Transform repairView;

    [Header("左右箭头按钮")]
    public Button leftButton;
    public Button rightButton;

    [Header("转头时间，单位：秒")]
    [Min(0.1f)]
    public float transitionDuration = 0.8f;

    // 0：配电板，1：驾驶台，2：维修区。
    private int currentIndex = 1;
    private bool transitioning;
    private Coroutine transition;

    public bool IsDriving =>
        isActiveAndEnabled &&
        currentIndex == 1 &&
        !transitioning;

    public string StationName =>
        transitioning
            ? "MOVING"
            : currentIndex == 0
                ? "POWER"
                : currentIndex == 1
                    ? "DRIVING"
                    : "REPAIR";

    private void Start()
    {
        Transform target = GetView(currentIndex);

        if (target != null)
        {
            transform.SetPositionAndRotation(
                target.position,
                target.rotation
            );
        }

        RefreshButtons();
    }

    public void LookLeft()
    {
        ChangeView(-1);
    }

    public void LookRight()
    {
        ChangeView(1);
    }

    private void ChangeView(int direction)
    {
        if (!isActiveAndEnabled) return;

        int nextIndex = Mathf.Clamp(currentIndex + direction, 0, 2);

        if (nextIndex == currentIndex) return;

        Transform target = GetView(nextIndex);
        if (target == null) return;

        if (transition != null)
            StopCoroutine(transition);

        currentIndex = nextIndex;
        transitioning = true;

        transition = StartCoroutine(MoveToView(target));
        RefreshButtons();
    }

    private Transform GetView(int index)
    {
        switch (index)
        {
            case 0: return powerView;
            case 1: return drivingView;
            case 2: return repairView;
            default: return null;
        }
    }

    private IEnumerator MoveToView(Transform target)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;

        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, transitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.SmoothStep(
                0f, 1f,
                Mathf.Clamp01(elapsed / duration)
            );

            transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, target.position, progress),
                Quaternion.Slerp(startRotation, target.rotation, progress)
            );

            yield return null;
        }

        transform.SetPositionAndRotation(
            target.position,
            target.rotation
        );

        transitioning = false;
        transition = null;
    }

    private void RefreshButtons()
    {
        if (leftButton != null)
        {
            leftButton.interactable =
                isActiveAndEnabled && currentIndex > 0;
        }

        if (rightButton != null)
        {
            rightButton.interactable =
                isActiveAndEnabled && currentIndex < 2;
        }
    }

    private void OnEnable()
    {
        RefreshButtons();
    }

    private void OnDisable()
    {
        if (transition != null)
            StopCoroutine(transition);

        transition = null;
        transitioning = false;

        RefreshButtons();
    }
}