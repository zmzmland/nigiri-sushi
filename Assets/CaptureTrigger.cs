using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム画面で Space を押すと、Python 側に「今のフレームで判定して」と伝える。
///
/// フルスクリーンのビルドでは、プレイヤーは OpenCV のカメラウィンドウに
/// 触れない。ゲーム側でも Space を受け付けられるようにするための橋渡し。
/// Python 側は capture_trigger.txt を監視しつつ、自分のウィンドウでの
/// Space も従来どおり受け付けるので、どちらからでも判定できる。
///
/// 使い方: Game Scene と Game Scene 2 の適当な空 GameObject にアタッチする。
/// </summary>
public class CaptureTrigger : MonoBehaviour
{
    [Tooltip("判定結果が返ってこないときに、再撮影を許可するまでの秒数")]
    public float processingTimeout = 6f;

    [Tooltip("撮影後、この秒数は次の撮影を受け付けない（連打防止）")]
    public float cooldown = 0.5f;

    private float processingStartTime = -1f;
    private float lastTriggerTime = -999f;

    void Start()
    {
        ResultData.isProcessing = false;
        GamePaths.SafeDelete(GamePaths.TriggerPath);
    }

    void Update()
    {
        // 判定待ちが長引いたら解除する。
        // Python が落ちていても、二度と撮影できない状態にはしない。
        if (ResultData.isProcessing &&
            processingStartTime >= 0f &&
            Time.time - processingStartTime > processingTimeout)
        {
            Debug.LogWarning("判定がタイムアウトしました。再撮影を許可します。");
            ResultData.isProcessing = false;
            processingStartTime = -1f;
        }

        if (Keyboard.current == null) return;
        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

        if (Time.time - lastTriggerTime < cooldown) return;

        if (ResultData.isProcessing)
        {
            Debug.Log("前回の判定待ちのため、撮影をスキップしました");
            return;
        }

        if (GamePaths.SafeWrite(GamePaths.TriggerPath, "1"))
        {
            ResultData.isProcessing = true;
            processingStartTime = Time.time;
            lastTriggerTime = Time.time;
            Debug.Log("撮影トリガーを送信しました");
        }
    }

    void OnDisable()
    {
        // シーンを抜けるときは必ず解除する。
        // 残ったままだと次のシーンで撮影できなくなる。
        ResultData.isProcessing = false;
    }
}
