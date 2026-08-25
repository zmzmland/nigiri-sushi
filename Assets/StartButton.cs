using UnityEngine;
using UnityEngine.SceneManagement;

public class StartButton : MonoBehaviour
{
    [Tooltip("押したときに進むシーン")]
    public string nextSceneName = "Game Scene";

    public void StartGame()
    {
        GameAudio.Click();
        SceneManager.LoadScene(nextSceneName);
    }
}
