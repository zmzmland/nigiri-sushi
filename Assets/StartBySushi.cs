using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// タイトル画面で「まな板にマグロを置く」とゲームが始まる仕掛け。
///
/// Python が 0.2 秒ごとに書き出す detect.txt（いま見えているネタの一覧）を
/// 読み、指定したネタが一定時間見え続けたら開始します。
///
/// キーボードにもマウスにも触らずに始められるので、
/// 「寿司を置く」という体験がタイトルから始まります。
///
/// ★ 置き方
///   タイトルシーン（SampleScene）の空の GameObject に付けるだけです。
///   ModeSelect と同じ GameObject に付けても構いません。
///   表示は OnGUI で出すので、Canvas への配置は不要です。
///
/// ★ 止めたいとき
///   Inspector の Enabled を外すか、Enable を外してください。
/// </summary>
public class StartBySushi : MonoBehaviour
{
    [Header("開始の条件")]
    [Tooltip("この機能を使うか")]
    public bool enableSushiStart = true;

    [Tooltip("置くと開始になるネタ。YOLO のクラス名と同じ綴りで")]
    public string triggerClass = "maguro";

    [Tooltip("この秒数、見え続けたら開始する")]
    public float holdSeconds = 1.2f;

    [Tooltip("detect.txt を読む間隔（秒）")]
    public float pollInterval = 0.15f;

    [Header("開始したときの動き")]
    [Tooltip("入れるとモード選択を開く。外すと直接ゲームを始める")]
    public bool openModeSelect = true;

    [Tooltip("モード選択を使わないときに進むシーン")]
    public string nextSceneName = "Game Scene";

    [Header("画面の案内")]
    public bool showGuide = true;

    [Tooltip("待っているときの案内")]
    public string waitingText = "まな板に「マグロ」を置くと始まります";

    [Tooltip("見えているときの案内")]
    public string detectedText = "マグロを確認しました";

    [Tooltip("画像認識が動いていないときの案内")]
    public string offlineText = "画像認識の準備中です（「始め」でも開始できます）";

    [Tooltip("画面の下端からの距離")]
    public float bottomMargin = 90f;

    // --- 内部状態 ---
    private float lastPoll = -999f;
    private bool seeing = false;      // いま対象が見えているか
    private float seeingSince = -1f;  // 見え始めた時刻
    private bool pythonAlive = false;
    private bool started = false;

    void Update()
    {
        if (!enableSushiStart || started) return;

        // モード選択が開いている間は反応しない
        if (ModeSelect.Instance != null && ModeSelect.Instance.IsOpen)
        {
            seeing = false;
            seeingSince = -1f;
            return;
        }

        if (Time.time - lastPoll < pollInterval) return;
        lastPoll = Time.time;

        pythonAlive =
            GamePaths.SecondsSinceWrite(GamePaths.HeartbeatPath) <= 8.0;

        if (!pythonAlive)
        {
            seeing = false;
            seeingSince = -1f;
            return;
        }

        bool found = IsTargetVisible();

        if (found)
        {
            if (!seeing)
            {
                seeing = true;
                seeingSince = Time.time;
            }
            else if (Time.time - seeingSince >= holdSeconds)
            {
                Begin();
            }
        }
        else
        {
            seeing = false;
            seeingSince = -1f;
        }
    }

    /// <summary>detect.txt に対象のネタが載っているか。</summary>
    private bool IsTargetVisible()
    {
        string text = GamePaths.SafeRead(GamePaths.DetectPath);
        if (string.IsNullOrEmpty(text)) return false;

        foreach (string line in text.Split('\n'))
        {
            if (line.Trim() == triggerClass) return true;
        }
        return false;
    }

    private void Begin()
    {
        started = true;
        GameAudio.Click();
        Debug.Log($"[StartBySushi] {triggerClass} を検知して開始します");

        if (openModeSelect && ModeSelect.Instance != null)
        {
            ModeSelect.Instance.Show();
            started = false;          // 戻ってきたらまた反応できるように
            seeing = false;
            seeingSince = -1f;
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    // =====================================================
    //  画面の案内（Canvas を作らなくてよいよう OnGUI で描く）
    // =====================================================
    void OnGUI()
    {
        if (!showGuide || !enableSushiStart) return;
        if (ModeSelect.Instance != null && ModeSelect.Instance.IsOpen) return;

        string message;
        float progress = 0f;

        if (!pythonAlive)
        {
            message = offlineText;
        }
        else if (seeing)
        {
            message = detectedText;
            progress = holdSeconds <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - seeingSince) / holdSeconds);
        }
        else
        {
            message = waitingText;
        }

        float w = Mathf.Min(760f, Screen.width - 40f);
        float h = 54f;
        float x = (Screen.width - w) / 2f;
        float y = Screen.height - bottomMargin;

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // 進み具合のバー
        if (progress > 0f)
        {
            GUI.color = new Color(1f, 0.85f, 0.4f, 0.85f);
            GUI.DrawTexture(new Rect(x, y + h - 6f, w * progress, 6f), Texture2D.whiteTexture);
        }

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
        };
        style.normal.textColor = pythonAlive ? Color.white : new Color(1f, 0.8f, 0.6f);

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h - 6f), message, style);

        GUI.color = prev;
    }
}
