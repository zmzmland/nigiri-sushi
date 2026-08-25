using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトルの「始め」ボタン。
///
/// シーンに ModeSelect があれば、モード選択画面を開きます。
/// 無ければ、そのままゲームを始めます（開発中の確認用）。
/// </summary>
public class StartButton : MonoBehaviour
{
    [Tooltip("ModeSelect が無いときに進むシーン")]
    public string nextSceneName = "Game Scene";

    public void StartGame()
    {
        GameAudio.Click();

        if (ModeSelect.Instance != null)
        {
            ModeSelect.Instance.Show();
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
