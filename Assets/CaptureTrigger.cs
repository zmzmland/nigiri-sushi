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

    [Tooltip("画面の下端から、状態表示までの距離（ピクセル）。\n" +
             "注文票と重なるときは、この数字を大きくして上へ逃がしてください")]
    public float statusBottomMargin = 230f;

    [Header("いま見えているネタの表示")]
    [Tooltip("まな板の上で認識できているネタを、画面の下に出し続ける。\n" +
             "「ちゃんと見えているのか分からない」を解消するためのもの")]
    public bool showDetect = true;

    [Tooltip("detect.txt を読む間隔（秒）。Python は0.2秒ごとに書いています")]
    public float detectCheckInterval = 0.15f;

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

    // いまカメラに見えているネタ（Python が detect.txt に0.2秒ごとに書く）
    private readonly System.Collections.Generic.List<string> seenNames =
        new System.Collections.Generic.List<string>();
    private float lastDetectCheck = -999f;
    private int orderCount = 0;

    // 「止まっていない」ことを見せるための明滅。
    // 数字が動かない状態でも、これが動いていれば生きていると分かります。
    private float pulse = 0f;

    void Start()
    {
        ResultData.isProcessing = false;
        GamePaths.SafeDelete(GamePaths.TriggerPath);
    }

    void Update()
    {
        WatchPython();
        WatchCountdown();
        WatchDetect();

        pulse = (pulse + Time.unscaledDeltaTime) % 1f;

        // 判定待ちが長引いたら解除する。
        // Python が落ちていても、二度と撮影できない状態にはしない。
        if (ResultData.isProcessing &&
            processingStartTime >= 0f &&
            Time.time - processingStartTime > processingTimeout)
        {
            Debug.LogWarning("判定がタイムアウトしました。再撮影を許可します。");
            ResultData.isProcessing = false;
            processingStartTime = -1f;
            Flash(GameMode.T("判定が返ってきませんでした。もう一度 Space を押してください", "No result came back. Press Space again"), 4f);
        }

        if (Keyboard.current == null) return;

        HandleOperatorKeys();

        if (!Keyboard.current.spaceKey.wasPressedThisFrame) return;

        // --- 注文がまだ出そろっていない ---
        if (!ResultData.ordersReady)
        {
            Debug.Log("注文がまだ出そろっていないため、撮影を受け付けませんでした");
            Flash(GameMode.T("注文を最後まで聞いてください", "Listen to the whole order"), 1.5f);
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
            Flash(GameMode.T("画像認識が停止しています（係員を呼んでください）", "Camera stopped — please call our staff"), 4f);
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
            Flash(GameMode.T("画像認識が回復しました", "Camera is back"), 2f);
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
        string next = string.IsNullOrEmpty(text) ? "" : text;

        // 数字が変わった瞬間だけ「コッ」と鳴らす
        if (next != countdownText && !string.IsNullOrEmpty(next)) GameAudio.Countdown();

        countdownText = next;
    }

    // =========================================================
    //  いま見えているネタ
    //
    //  寿司を置いてからカウントダウンが始まるまでの数秒、
    //  これまで画面は何も出していませんでした。
    //  プレイヤーにとっては一番不安な時間なので、
    //  「いま何が見えているか」を出し続けます。
    //
    //  認識されていないネタがあれば、その場で気づいて
    //  置き直せる、という効果もあります。
    // =========================================================
    private void WatchDetect()
    {
        if (!showDetect) return;
        if (Time.time - lastDetectCheck < detectCheckInterval) return;
        lastDetectCheck = Time.time;

        seenNames.Clear();

        string text = GamePaths.SafeRead(GamePaths.DetectPath);
        if (!string.IsNullOrEmpty(text))
        {
            foreach (string line in text.Split('\n'))
            {
                string n = line.Trim();
                if (n.Length > 0) seenNames.Add(n);
            }
        }

        orderCount = CountOrderLines();
    }

    /// <summary>order.txt の行数 = いまの注文の貫数。</summary>
    private int CountOrderLines()
    {
        string text = GamePaths.SafeRead(GamePaths.OrderPath);
        if (string.IsNullOrEmpty(text)) return 0;

        int n = 0;
        foreach (string line in text.Split('\n'))
            if (line.Trim().Length > 0) n++;
        return n;
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
            Flash(GameMode.T("強制進行しました", "Skipped"), 2f);
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
        // 画面の大きさに合わせて表示全体を拡大する。
        // OnGUI はピクセルで描くので、これが無いとフルスクリーンで
        // 文字だけ小さいままになります。倍率は UiScale.Extra。
        Matrix4x4 __m = UiScale.Begin();
        try { DrawGui(); }
        finally { UiScale.End(__m); }
    }

    private void DrawGui()
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
            DrawBanner(GameMode.T("画像認識が停止しています", "Camera is not responding"), style, UiScale.H - statusBottomMargin);
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
            DrawBanner(GameMode.T("判定中…", "Checking…"), style, UiScale.H - statusBottomMargin);
            return;
        }

        // --- 一時メッセージ ---
        if (Time.time < flashUntil && !string.IsNullOrEmpty(flashMessage))
        {
            style.normal.textColor = Color.white;
            DrawBanner(flashMessage, style, UiScale.H - statusBottomMargin);
            return;
        }

        // --- いま見えているネタ（ふだんはこれが出ている） ---
        if (showDetect && ResultData.ordersReady) DrawDetect();
    }

    /// <summary>
    /// 画面の下に「認識できている個数」を出し続ける。
    ///
    /// 　●が明滅  … 止まっていない証拠
    /// 　n / m 貫  … あといくつ足りないか
    ///
    /// ★ ネタの名前は出しません。
    ///   名前を出すと、置いた時点で正解か分かってしまい、
    ///   判定の緊張がなくなるためです。
    ///   「合っていたか」は判定のあとに出します。
    /// </summary>
    private void DrawDetect()
    {
        int seen = seenNames.Count;
        bool ready = (orderCount > 0 && seen >= orderCount);

        string body;
        Color tone;

        if (seen == 0)
        {
            body = GameMode.T("まな板を見ています", "Watching the board");
            tone = new Color(0.80f, 0.80f, 0.80f);
        }
        else if (ready)
        {
            body = GameMode.T("そろいました", "All set");
            tone = new Color(0.6f, 1f, 0.65f);
        }
        else
        {
            body = GameMode.T("握りを見ています", "Reading your sushi");
            tone = Color.white;
        }

        float w = Mathf.Min(720f, UiScale.W - 40f);
        float h = 52f;
        float x = (UiScale.W - w) / 2f;
        float y = UiScale.H - statusBottomMargin;

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // 明滅する丸。動いていること自体が「生きている」合図になります
        float a = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(pulse * Mathf.PI));
        GUI.color = new Color(tone.r, tone.g, tone.b, a);
        GUI.DrawTexture(new Rect(x + 16f, y + h / 2f - 6f, 12f, 12f), Texture2D.whiteTexture);

        GUI.color = Color.white;

        var main = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 19,
        };
        main.normal.textColor = tone;
        GUI.Label(new Rect(x + 40f, y, w - 170f, h), body, main);

        var count = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 19,
            fontStyle = FontStyle.Bold,
        };
        count.normal.textColor = tone;
        GUI.Label(new Rect(x, y, w - 16f, h),
                  (orderCount > 0)
                    ? GameMode.T($"{seen} / {orderCount} 貫", $"{seen} / {orderCount} pcs")
                    : GameMode.T($"{seen} 貫", $"{seen} pcs"), count);

        // そろったら、次にやることを出す。
        // 「手を引く」は初めての人がまず気づかない操作です。
        if (ready)
        {
            var note = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
            };
            note.normal.textColor = new Color(1f, 1f, 1f, 0.8f);

            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y - 28f, w, 26f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y - 28f, w, 26f),
                      GameMode.T("手を引くと判定します", "Take your hand away"), note);
        }

        GUI.color = prev;
    }

    /// <summary>
    /// 「握りました　3 2 1」の表示。
    /// 中断できることが伝わるよう、下に一行添える。
    /// </summary>
    private void DrawCountdown(string number)
    {
        float w = Mathf.Min(560f, UiScale.W - 40f);
        float h = 190f;
        float x = (UiScale.W - w) / 2f;
        float y = (UiScale.H - h) / 2f;

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
        GUI.Label(new Rect(x, y + 12f, w, 32f),
                  GameMode.T("へい、お待ち！", "Here you are!"), head);

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
                  GameMode.T("直したいときは、寿司に触れば止まります",
                             "Touch a piece to stop the count"), note);

        GUI.color = prev;
    }

    private void DrawBanner(string text, GUIStyle style, float y)
    {
        float w = Mathf.Min(720f, UiScale.W - 40f);
        float h = 44f;
        float x = (UiScale.W - w) / 2f;

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
