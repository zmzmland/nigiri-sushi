using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// ゲーム画面で Space を押すと、Python 側に「今のフレームで判定して」と伝える。
///
/// フルスクリーンのビルドでは、プレイヤーは OpenCV のカメラウィンドウに
/// 触れない。ゲーム側でも Space を受け付けられるようにするための橋渡し。
///
/// 【今回の追加】
///   * 注文がまだ出そろっていない間は撮影させない。
///     以前は提示中に Space を押すと Python が判定して result.txt を書き、
///     注文が出そろった瞬間にそれを拾って即座に次の面へ飛んでいた。
///   * heartbeat.txt を見て Python が生きているか監視する。
///     落ちていれば画面に出し、係員が復旧できるようにする。
///   * 判定中であることを画面に出す（連打防止）。
///   * 係員用の強制進行キー（Ctrl+Shift+N）。
///     Python が復旧できないときに、その面を 0 貫として先へ進める。
///
/// 使い方: 各ゲームシーンの空 GameObject にアタッチする。
///         Inspector の設定は不要（表示は OnGUI で出す）。
/// </summary>
public class CaptureTrigger : MonoBehaviour
{
    [Header("撮影")]
    [Tooltip("判定結果が返ってこないときに、再撮影を許可するまでの秒数")]
    public float processingTimeout = 6f;

    [Tooltip("撮影後、この秒数は次の撮影を受け付けない（連打防止）")]
    public float cooldown = 0.5f;

    [Header("画像認識の監視")]
    [Tooltip("heartbeat.txt がこの秒数以上更新されなければ、停止とみなす")]
    public float heartbeatTimeout = 8f;

    [Tooltip("画面にメッセージを出す")]
    public bool showStatus = true;

    private float processingStartTime = -1f;
    private float lastTriggerTime = -999f;

    private bool pythonAlive = true;
    private bool warnedDead = false;

    // 自動判定のカウントダウン（Python が countdown.txt に書く）
    private string countdownText = "";
    private float lastCountdownCheck = -999f;

    // 画面に一時的に出すメッセージ
    private string flashMessage = "";
    private float flashUntil = -1f;

    void Start()
    {
        ResultData.isProcessing = false;
        GamePaths.SafeDelete(GamePaths.TriggerPath);
    }

    void Update()
    {
        WatchPython();
        WatchCountdown();

        // 判定待ちが長引いたら解除する。
        // Python が落ちていても、二度と撮影できない状態にはしない。
        if (ResultData.isProcessing &&
            processingStartTime >= 0f &&
            Time.time - processingStartTime > processingTimeout)
        {
            Debug.LogWarning("判定がタイムアウトしました。再撮影を許可します。");
            ResultData.isProcessing = false;
            processingStartTime = -1f;
            Flash("判定が返ってきませんでした。もう一度 Space を押してください", 4f);
        }

        if (Keyboard.current == null) return;

        HandleOperatorKeys();

        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

        // --- 注文がまだ出そろっていない ---
        if (!ResultData.ordersReady)
        {
            Debug.Log("注文がまだ出そろっていないため、撮影を受け付けませんでした");
            Flash("注文を最後まで聞いてください", 1.5f);
            return;
        }

        if (Time.time - lastTriggerTime < cooldown) return;

        if (ResultData.isProcessing)
        {
            Debug.Log("前回の判定待ちのため、撮影をスキップしました");
            return;
        }

        if (!pythonAlive)
        {
            Debug.LogWarning("画像認識が動いていないため、撮影できません");
            Flash("画像認識が停止しています（係員を呼んでください）", 4f);
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

    // =========================================================
    //  Python が生きているかの監視
    // =========================================================
    private void WatchPython()
    {
        double age = GamePaths.SecondsSinceWrite(GamePaths.HeartbeatPath);
        bool alive = age <= heartbeatTimeout;

        if (alive == pythonAlive) return;

        pythonAlive = alive;

        if (!alive)
        {
            if (!warnedDead)
            {
                warnedDead = true;
                Debug.LogError(
                    "画像認識（Python）からの応答が止まりました。\n" +
                    "  ターミナルで main.py が動いているか確認してください。\n" +
                    "  復旧できない場合は Ctrl+Shift+N でこの面を飛ばせます。");
            }
        }
        else
        {
            warnedDead = false;
            Debug.Log("画像認識との接続が回復しました");
            Flash("画像認識が回復しました", 2f);
        }
    }

    // =========================================================
    //  自動判定のカウントダウン
    // =========================================================
    private void WatchCountdown()
    {
        if (Time.time - lastCountdownCheck < 0.1f) return;
        lastCountdownCheck = Time.time;

        string text = GamePaths.SafeRead(GamePaths.CountdownPath);
        countdownText = string.IsNullOrEmpty(text) ? "" : text;
    }

    // =========================================================
    //  係員用のキー
    // =========================================================
    private void HandleOperatorKeys()
    {
        var kb = Keyboard.current;

        bool ctrl  = kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed;
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        if (!ctrl || !shift) return;

        // Ctrl+Shift+N : この面を 0 貫として先へ進める
        if (kb.nKey.wasPressedThisFrame)
        {
            Debug.LogWarning("[係員] 強制進行: この面を 0 貫として先へ進めます");
            ResultData.isProcessing = false;
            GamePaths.SafeWrite(GamePaths.ResultPath, "0");
            Flash("強制進行しました", 2f);
        }
    }

    private void Flash(string message, float seconds)
    {
        flashMessage = message;
        flashUntil = Time.time + seconds;
    }

    // =========================================================
    //  画面表示（Canvas を作らなくてよいよう OnGUI で描く）
    // =========================================================
    void OnGUI()
    {
        if (!showStatus) return;

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
        };

        // --- 画像認識が停止している（最優先で出す） ---
        if (!pythonAlive)
        {
            style.normal.textColor = new Color(1f, 0.5f, 0.5f);
            DrawBanner("画像認識が停止しています", style, Screen.height - 120f);
            return;
        }

        // --- 自動判定のカウントダウン ---
        if (!string.IsNullOrEmpty(countdownText))
        {
            DrawCountdown(countdownText);
            return;
        }

        // --- 判定中 ---
        if (ResultData.isProcessing)
        {
            style.normal.textColor = Color.white;
            DrawBanner("判定中…", style, Screen.height - 120f);
            return;
        }

        // --- 一時メッセージ ---
        if (Time.time < flashUntil && !string.IsNullOrEmpty(flashMessage))
        {
            style.normal.textColor = Color.white;
            DrawBanner(flashMessage, style, Screen.height - 120f);
        }
    }

    /// <summary>
    /// 「握りました　3 2 1」の表示。
    /// 中断できることが伝わるよう、下に一行添える。
    /// </summary>
    private void DrawCountdown(string number)
    {
        float w = Mathf.Min(560f, Screen.width - 40f);
        float h = 190f;
        float x = (Screen.width - w) / 2f;
        float y = (Screen.height - h) / 2f;

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        var head = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 26,
        };
        head.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y + 12f, w, 32f), "へい、お待ち！", head);

        var big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 90,
            fontStyle = FontStyle.Bold,
        };
        big.normal.textColor = new Color(1f, 0.85f, 0.4f);
        GUI.Label(new Rect(x, y + 44f, w, 100f), number, big);

        var note = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
        };
        note.normal.textColor = new Color(1f, 1f, 1f, 0.75f);
        GUI.Label(new Rect(x, y + h - 36f, w, 24f),
                  "直したいときは、寿司に触れば止まります", note);

        GUI.color = prev;
    }

    private void DrawBanner(string text, GUIStyle style, float y)
    {
        float w = Mathf.Min(720f, Screen.width - 40f);
        float h = 44f;
        float x = (Screen.width - w) / 2f;

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), text, style);

        GUI.color = prev;
    }

    void OnDisable()
    {
        // シーンを抜けるときは必ず解除する。
        // 残ったままだと次のシーンで撮影できなくなる。
        ResultData.isProcessing = false;
    }
}
