using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// リザルト画面からタイトル画面へ戻るボタン。
///
/// 使い方:
///   1. ボタンの GameObject にこのスクリプトを付ける
///   2. Button コンポーネントの OnClick に、その GameObject を登録し
///      TitleButton → GoToTitle() を選ぶ
///
/// ResultData は static なのでシーンを移動しても値が残る。
/// タイトルへ戻る時点でリセットしないと、次のプレイに前回の得点が
/// 持ち越されてしまうため、ここで初期化している。
/// </summary>
public class TitleButton : MonoBehaviour
{
    [Tooltip("戻り先のシーン名。Build Settings に登録されている必要があります")]
    public string titleSceneName = "SampleScene";

    public void GoToTitle()
    {
        ResetScore();

        Debug.Log($"タイトルへ戻ります: {titleSceneName}");
        SceneManager.LoadScene(titleSceneName);
    }

    /// <summary>もう一度プレイする場合はこちらを OnClick に登録する。</summary>
    public void Retry()
    {
        ResetScore();

        Debug.Log("最初からやり直します");
        SceneManager.LoadScene("Game Scene");
    }

    private void ResetScore()
    {
        // 以前はここで項目を1つずつ消していましたが、面が増えるたびに
        // 書き忘れる（実際 scene3Time が漏れていた）ので、
        // ResultData 側の ResetAll に一本化しました。
        ResultData.ResetAll();

        // 前回の判定結果が残っていると、次のプレイ開始直後に
        // 誤って拾ってしまうので消しておく。
        GamePaths.SafeWrite(GamePaths.ResultPath, "");
        GamePaths.SafeWrite(GamePaths.OrderPath, "");
        GamePaths.SafeDelete(GamePaths.TriggerPath);
    }
}
