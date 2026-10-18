using UnityEngine;
using UnityEngine.SceneManagement;

public class RepairRestart : MonoBehaviour
{
    private bool loading;

    public void RestartVoyage()
    {
        if (loading) return;

        Scene currentScene = gameObject.scene;

        if (!Application.CanStreamedLevelBeLoaded(currentScene.path))
        {
            Debug.LogError(
                "无法重新加载：请把当前场景加入启用的构建场景列表。",
                this
            );

            return;
        }

        loading = true;

        SceneManager.LoadScene(
            currentScene.path,
            LoadSceneMode.Single
        );
    }
}

