using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Shows the authored tutorial for the current station without consuming pointer input.</summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent, RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
public sealed class TutorialOverlay : MonoBehaviour
{
    [Header("Station camera")]
    public RepairStationView stationView;
    [Header("Authored UI objects")]
    public GameObject openTutorialUI;
    public GameObject controllerTutorialUI;
    public GameObject leftTutorialUI;
    public GameObject rightTutorialUI;

    public bool IsTutorialVisible => shownStation >= 0;
    private int shownStation = -1;
    private RepairStationView subscribedView;

    private void OnEnable()
    {
        ResolveStation();
        BindStation();
        // Both the canvas group and every graphic allow clicks to pass through.
        CanvasGroup group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        if (openTutorialUI != null) openTutorialUI.SetActive(true);
        HideTutorial();
    }

    private void Start()
    {
        ResolveStation();
        BindStation();
    }

    private void ResolveStation()
    {
        if (stationView == null && Camera.main != null)
            stationView = Camera.main.GetComponent<RepairStationView>();
    }

    private void BindStation()
    {
        if (subscribedView == stationView) return;
        if (subscribedView != null) subscribedView.ViewChanging -= HideTutorial;
        subscribedView = stationView;
        if (subscribedView != null) subscribedView.ViewChanging += HideTutorial;
        HideTutorial();
    }

    private void Update()
    {
        BindStation();
        if (IsTutorialVisible && (stationView == null || !stationView.isActiveAndEnabled
            || stationView.IsTransitioning || shownStation != stationView.CurrentStationIndex))
            HideTutorial();
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            ToggleTutorial();
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.T)) ToggleTutorial();
#endif
    }

    public void ToggleTutorial()
    {
        if (GameMenuController.IsInputBlocked) return;
        if (!isActiveAndEnabled) return;
        if (IsTutorialVisible)
        {
            HideTutorial();
            return;
        }
        if (stationView == null || !stationView.isActiveAndEnabled || stationView.IsTransitioning)
            return;
        GameObject panel = stationView.CurrentStationIndex == 0 ? leftTutorialUI
            : stationView.CurrentStationIndex == 1 ? controllerTutorialUI : rightTutorialUI;
        if (panel == null) return;
        HideTutorial();
        panel.SetActive(true);
        shownStation = stationView.CurrentStationIndex;
    }

    public void HideTutorial()
    {
        if (controllerTutorialUI != null) controllerTutorialUI.SetActive(false);
        if (leftTutorialUI != null) leftTutorialUI.SetActive(false);
        if (rightTutorialUI != null) rightTutorialUI.SetActive(false);
        shownStation = -1;
    }

    private void OnDisable()
    {
        if (subscribedView != null) subscribedView.ViewChanging -= HideTutorial;
        subscribedView = null;
        HideTutorial();
    }
}
