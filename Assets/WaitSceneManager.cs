using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 面と面のあいだの待機シーン。
///
/// 3面に増やしたので、WaitScene は
///   1面 → 待機 → 2面
///   2面 → 待機 → 3面
/// の2回使われます。行き先が2通りあるので、直前の客が
/// ResultData.nextAfterWait に書いた場所へ進みます。
///
/// 万一そこが空だったときは fallbackScene へ逃がします。
/// </summary>
public class WaitSceneManager : MonoBehaviour
{
    [Header("Wait Settings")]
    public float waitSeconds = 3f;

    [Tooltip("行き先が決まっていなかったときの逃げ先")]
    public string fallbackScene = "Game Scene 2";

    IEnumerator Start()
    {
        yield return new WaitForSeconds(waitSeconds);

        string next = ResultData.nextAfterWait;

        if (string.IsNullOrEmpty(next))
        {
            Debug.LogWarning($"nextAfterWait が空でした。{fallbackScene} へ進みます。");
            next = fallbackScene;
        }

        Debug.Log($"WaitScene → {next}");
        SceneManager.LoadScene(next);
    }
}
