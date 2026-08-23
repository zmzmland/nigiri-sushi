using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// 1面のデバッグ補助。
///
/// 【変更点】
///   * Space → F9 に変更。Space は判定キーとして予約するため。
///     旧コードは Space で 2面へ飛んでいたため、判定も採点もされずに
///     シーンが進んでしまう事故のもとだった。
///   * #if UNITY_EDITOR で囲み、ビルドには一切含まれないようにした。
///
/// F9        : 判定結果を偽装して次のシーンへ（通常の採点処理を通る）
/// Shift+F9  : 強制的に次のシーンへ（採点なし。演出だけ確認したいとき）
/// </summary>
public class Scene1Manager : MonoBehaviour
{
#if UNITY_EDITOR

    [Header("Debug")]
    [Tooltip("F9 を押したときに result.txt へ書き込む正解数")]
    public int debugCorrectCount = 3;

    [Tooltip("Shift+F9 で強制遷移する先")]
    public string forceNextScene = "Game Scene 2";

    void Start()
    {
        Debug.Log("[Debug] F9 = 判定を偽装 / Shift+F9 = 強制遷移");
    }

    void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.f9Key.wasPressedThisFrame) return;

        bool shift = Keyboard.current.leftShiftKey.isPressed ||
                     Keyboard.current.rightShiftKey.isPressed;

        if (shift)
        {
            // 採点を一切せずに飛ばす。演出の確認用。
            Debug.LogWarning($"[Debug] 強制遷移 → {forceNextScene}（採点なし）");
            SceneManager.LoadScene(forceNextScene);
            return;
        }

        // Python が結果を書いたのと同じ状態を作る。
        // Customer.WatchResultFile が拾って、通常どおり採点・遷移する。
        GamePaths.SafeWrite(GamePaths.ResultPath, debugCorrectCount.ToString());
        Debug.Log($"[Debug] 判定を偽装: {debugCorrectCount} 点を result.txt へ書き込み");
    }

#endif
}
