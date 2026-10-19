using UnityEngine;
using UnityEngine.SceneManagement;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameMenuController : MonoBehaviour
{
    public GameObject startPanel;
    public GameObject pausePanel;

    public static bool IsInputBlocked { get; private set; }

    private bool started;
    private bool paused;

    private void Start()
    {
        startPanel.SetActive(true);
        pausePanel.SetActive(false);
        SetPaused(true);
    }

    private void Update()
    {
        if (!started) return;

#if ENABLE_INPUT_SYSTEM
        bool escapePressed =
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape);
#endif

        if (escapePressed)
        {
            if (paused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    public void StartGame()
    {
        started = true;
        startPanel.SetActive(false);
        ResumeGame();
    }

    public void PauseGame()
    {
        if (!started) return;

        pausePanel.SetActive(true);
        SetPaused(true);
    }

    public void ResumeGame()
    {
        if (!started) return;

        pausePanel.SetActive(false);
        SetPaused(false);
    }

    public void RestartGame()
    {
        Scene scene = gameObject.scene;

        if (!Application.CanStreamedLevelBeLoaded(scene.path))
        {
            Debug.LogError("请先把当前场景加入构建场景列表。");
            return;
        }

        IsInputBlocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(scene.path);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetPaused(bool value)
    {
        paused = value;
        IsInputBlocked = value;

        Time.timeScale = value ? 0f : 1f;
        AudioListener.pause = value;
    }

    private void OnDestroy()
    {
        IsInputBlocked = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}