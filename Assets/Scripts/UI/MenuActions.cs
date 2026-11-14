using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuActions : MonoBehaviour
{
    public string gameSceneName = "Game";
    [Tooltip("Short wait so the click sound can play before the scene changes.")]
    public float loadDelay = 0.25f;

    public void LoadGame()
    {
        StartCoroutine(LoadAfterDelay());
    }

    IEnumerator LoadAfterDelay()
    {
        yield return new WaitForSecondsRealtime(loadDelay);
        SceneManager.LoadScene(gameSceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}