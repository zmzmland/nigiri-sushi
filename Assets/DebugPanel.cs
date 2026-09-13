// =====================================================================
//  DebugPanel.cs  —  開発用のデバッグパネル
//
//  【何のためのもの】
//    「リザルトの称号を見たいだけなのに3面通さないといけない」
//    「画像認識を動かさないと1歩も進めない」
//    こういう待ち時間を全部なくすためのものです。
//
//  【使い方】
//    Ctrl + Shift + D でパネルが開きます。もう一度押すと閉じます。
//    シーンに何も置く必要はありません。自動で現れます。
//
//    F1 でも開きますが、Mac では F1 が画面の明るさに割り当てられていて
//    アプリまで届かないことがあります。Ctrl + Shift + D を使ってください。
//    （Ctrl+Shift+N で面を飛ばす、Ctrl+Shift+R で番付リセット、と同じ並びです）
//
//  【本番には入りません】
//    ファイル全体が #if UNITY_EDITOR || DEVELOPMENT_BUILD で囲んであります。
//    Development Build のチェックを外してビルドすれば、
//    このクラスは存在ごと消えます。TGS 用のビルドでは必ず外してください。
// =====================================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class DebugPanel : MonoBehaviour
{
    // -----------------------------------------------------------------
    //  自動で出現させる（シーンに置く手間をなくす）
    // -----------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindAnyObjectByType<DebugPanel>() != null) return;

        var go = new GameObject("DebugPanel");
        go.AddComponent<DebugPanel>();
        DontDestroyOnLoad(go);
    }

    // -----------------------------------------------------------------
    //  設定
    // -----------------------------------------------------------------
    private static readonly string[] Scenes =
    {
        "SampleScene", "Game Scene", "WaitScene",
        "Game Scene 2", "Game Scene 3", "ResultScene",
    };

    /// <summary>称号ごとの見本。実際に出そうな内訳にしてあります。</summary>
    private class Sample
    {
        public string label;
        public int correct, total, speed, perfect;
        public int Final => correct * ResultData.PricePerPiece + speed + perfect;
    }

    private static readonly List<Sample> Samples = new List<Sample>
    {
        new Sample { label = "銀座（最上位）", correct = 12, total = 12, speed = 4500, perfect = 3000 },
        new Sample { label = "行列",           correct = 12, total = 12, speed = 2000, perfect = 3000 },
        new Sample { label = "評判",           correct = 11, total = 12, speed = 2000, perfect = 1000 },
        new Sample { label = "常連",           correct =  9, total = 12, speed = 1500, perfect =    0 },
        new Sample { label = "修業（最下位）", correct =  6, total = 12, speed =    0, perfect =    0 },
    };

    // -----------------------------------------------------------------
    //  状態
    // -----------------------------------------------------------------
    private bool open = false;
    private Vector2 scroll;

    private bool fakeHeartbeat = false;   // Python がいるふりをする
    private float lastFakeBeat = -99f;

    private bool registerRanking = false; // 見本を番付に載せるか
    private string customScore = "9000";

    private string message = "";
    private float messageUntil = -1f;

    // -----------------------------------------------------------------
    //  更新
    // -----------------------------------------------------------------
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Ctrl + Shift + D が本命。
        // Mac では F1 が OS に横取りされるため、押せないことがあります。
        bool ctrl  = kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed;
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

        if ((ctrl && shift && kb.dKey.wasPressedThisFrame) ||
            kb.f1Key.wasPressedThisFrame)
        {
            open = !open;
        }

        // Python の代わりに heartbeat を書く。
        // これがあると、画像認識を起動しなくても
        // 「寿司を置いて開始」や警告まわりの動作を確認できます。
        if (fakeHeartbeat && Time.unscaledTime - lastFakeBeat > 1f)
        {
            lastFakeBeat = Time.unscaledTime;
            GamePaths.SafeWrite(GamePaths.HeartbeatPath, System.DateTime.Now.ToString());
        }
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 3f;
        Debug.Log($"[DebugPanel] {text}");
    }

    // -----------------------------------------------------------------
    //  描画
    // -----------------------------------------------------------------
    private void OnGUI()
    {
        if (!open)
        {
            // 閉じているときは右下に小さく出しておく
            var hint = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 12,
            };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(Screen.width - 210f, Screen.height - 22f, 200f, 18f),
                      "Ctrl+Shift+D : デバッグ", hint);
            return;
        }

        float w = Mathf.Min(420f, Screen.width - 20f);
        float h = Screen.height - 20f;
        var area = new Rect(10f, 10f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));
        scroll = GUILayout.BeginScrollView(scroll);

        Title("デバッグパネル　（Ctrl+Shift+D で閉じる）");

        DrawStatus();
        DrawResultJump();
        DrawSceneJump();
        DrawFakeRecognition();
        DrawMode();
        DrawRanking();

        if (Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(message))
        {
            GUILayout.Space(8f);
            var s = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            s.normal.textColor = new Color(0.6f, 1f, 0.6f);
            GUILayout.Label("→ " + message, s);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // =================================================================
    //  各セクション
    // =================================================================

    /// <summary>いまの状態。何が起きているか分からない時間をなくします。</summary>
    private void DrawStatus()
    {
        Section("いまの状態");

        double beat = GamePaths.SecondsSinceWrite(GamePaths.HeartbeatPath);
        string python = (beat <= 8.0) ? $"動いている（{beat:F0}秒前）" : "止まっている";

        string order = GamePaths.SafeRead(GamePaths.OrderPath);
        string detect = GamePaths.SafeRead(GamePaths.DetectPath);

        Row("シーン", SceneManager.GetActiveScene().name);
        Row("モード", $"{GameMode.Current}　表示 {GameMode.Style}");
        Row("画像認識", python);
        Row("判定受付", ResultData.ordersReady ? "受付中" : "まだ");
        Row("提供数", $"{ResultData.correctCount} / {ResultData.totalOrders} 貫");
        Row("売上", $"{ResultData.score:N0}円");
        Row("速さ / 全問", $"+{ResultData.timeBonusYen:N0}円 / +{ResultData.perfectBonusYen:N0}円");
        Row("合計", $"{ResultData.finalScore:N0}円");
        Row("order.txt", Flatten(order));
        Row("detect.txt", Flatten(detect));
    }

    /// <summary>称号の確認。ここが一番使うはずです。</summary>
    private void DrawResultJump()
    {
        Section("リザルトを見る（3面通さずに）");

        registerRanking = GUILayout.Toggle(registerRanking, " 番付にも登録する（ふだんは外す）");

        foreach (Sample s in Samples)
        {
            if (GUILayout.Button($"{s.label}　{s.Final:N0}円　（{s.correct}/{s.total}貫）"))
            {
                JumpToResult(s.correct, s.total, s.speed, s.perfect);
            }
        }

        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("好きな金額", GUILayout.Width(70f));
        customScore = GUILayout.TextField(customScore, GUILayout.Width(80f));
        if (GUILayout.Button("この金額で見る"))
        {
            if (int.TryParse(customScore, out int yen))
            {
                // 売上は12貫ぶんに固定し、残りを速さボーナスとして入れる
                int baseScore = 12 * ResultData.PricePerPiece;
                if (yen >= baseScore) JumpToResult(12, 12, yen - baseScore, 0);
                else
                {
                    int pieces = Mathf.Clamp(yen / ResultData.PricePerPiece, 0, 12);
                    JumpToResult(pieces, 12, yen - pieces * ResultData.PricePerPiece, 0);
                }
            }
            else Say("数字を入れてください");
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSceneJump()
    {
        Section("シーンに飛ぶ");

        int perRow = 2;
        for (int i = 0; i < Scenes.Length; i += perRow)
        {
            GUILayout.BeginHorizontal();
            for (int k = 0; k < perRow && i + k < Scenes.Length; k++)
            {
                string name = Scenes[i + k];
                if (GUILayout.Button(name))
                {
                    // 1面に戻るときは今回の記録を捨てる
                    if (name == "Game Scene" || name == "SampleScene") ResultData.ResetAll();
                    SceneManager.LoadScene(name);
                    Say($"{name} に移動しました");
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    /// <summary>画像認識を動かさずにゲームを進めるための仕掛け。</summary>
    private void DrawFakeRecognition()
    {
        Section("画像認識のふり（Python なしで進める）");

        bool next = GUILayout.Toggle(fakeHeartbeat, " 動いていることにする（heartbeat を書く）");
        if (next != fakeHeartbeat)
        {
            fakeHeartbeat = next;
            if (!fakeHeartbeat) GamePaths.SafeDelete(GamePaths.HeartbeatPath);
            Say(fakeHeartbeat ? "heartbeat を書き始めました" : "heartbeat を止めました");
        }

        int count = CountOrders();
        GUILayout.Label($"　いまの注文数 : {count} 貫", Small());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("全問正解")) SendResult(count);
        if (GUILayout.Button("1つ間違い")) SendResult(Mathf.Max(0, count - 1));
        if (GUILayout.Button("全問不正解")) SendResult(0);
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);
        GUILayout.Label("　まな板に見えているネタ（タイトル・モード選択用）", Small());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("maguro")) SendDetect("maguro");
        if (GUILayout.Button("salmon")) SendDetect("salmon");
        if (GUILayout.Button("ebi")) SendDetect("ebi");
        if (GUILayout.Button("消す")) SendDetect("");
        GUILayout.EndHorizontal();
    }

    private void DrawMode()
    {
        Section("モード");

        GUILayout.BeginHorizontal();
        foreach (GameModeId id in System.Enum.GetValues(typeof(GameModeId)))
        {
            bool now = GameMode.Current == id;
            if (GUILayout.Button(now ? $"[ {id} ]" : id.ToString()))
            {
                GameMode.Current = id;
                Say($"モードを {id} にしました");
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawRanking()
    {
        Section("番付");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("いまのモードを消す"))
        {
            RankingData.Clear();
            Say($"{GameMode.Current} の番付を消しました");
        }
        if (GUILayout.Button("全モード消す"))
        {
            RankingData.ClearAll();
            Say("すべての番付を消しました");
        }
        GUILayout.EndHorizontal();
    }

    // =================================================================
    //  動作
    // =================================================================

    private void JumpToResult(int correct, int total, int speed, int perfect)
    {
        ResultData.ResetAll();

        ResultData.correctCount    = correct;
        ResultData.totalOrders     = total;
        ResultData.score           = correct * ResultData.PricePerPiece;
        ResultData.timeBonusYen    = Mathf.Max(0, speed);
        ResultData.perfectBonusYen = Mathf.Max(0, perfect);
        ResultData.finalScore      = ResultData.score
                                   + ResultData.timeBonusYen
                                   + ResultData.perfectBonusYen;

        // それらしい時間を入れておく（総対応時間の行のため）
        ResultData.scene1Time = 15f;
        ResultData.scene2Time = 20f;
        ResultData.scene3Time = 25f;

        // 登録済みということにすると ResultManager は番付に載せません
        ResultData.scoreRegistered = !registerRanking;

        SceneManager.LoadScene("ResultScene");
        Say($"{ResultData.finalScore:N0}円 でリザルトへ");
    }

    /// <summary>order.txt の行数 = いまの注文数。</summary>
    private int CountOrders()
    {
        string text = GamePaths.SafeRead(GamePaths.OrderPath);
        if (string.IsNullOrEmpty(text)) return 0;

        int n = 0;
        foreach (string line in text.Split('\n'))
            if (!string.IsNullOrWhiteSpace(line)) n++;
        return n;
    }

    /// <summary>Python の代わりに判定結果を書く。</summary>
    private void SendResult(int correct)
    {
        if (!ResultData.ordersReady)
        {
            Say("まだ注文が出そろっていません。少し待ってから押してください");
            return;
        }

        GamePaths.SafeWrite(GamePaths.ResultPath, correct.ToString());
        Say($"{correct} 貫正解として送りました");
    }

    private void SendDetect(string name)
    {
        GamePaths.SafeWrite(GamePaths.DetectPath, name);
        Say(string.IsNullOrEmpty(name) ? "まな板を空にしました" : $"{name} が見えていることにしました");
    }

    // =================================================================
    //  描画の小道具
    // =================================================================

    private static void Title(string text)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
        s.normal.textColor = new Color(1f, 0.85f, 0.4f);
        GUILayout.Label(text, s);
    }

    private static void Section(string text)
    {
        GUILayout.Space(10f);
        var s = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        s.normal.textColor = new Color(0.65f, 0.85f, 1f);
        GUILayout.Label("■ " + text, s);
    }

    private static void Row(string label, string value)
    {
        GUILayout.BeginHorizontal();
        var l = Small();
        l.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        GUILayout.Label(label, l, GUILayout.Width(90f));

        var v = Small();
        v.normal.textColor = Color.white;
        GUILayout.Label(value, v);
        GUILayout.EndHorizontal();
    }

    private static GUIStyle Small()
    {
        return new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
    }

    /// <summary>複数行を1行にまとめて表示する。</summary>
    private static string Flatten(string text)
    {
        if (string.IsNullOrEmpty(text)) return "（空）";

        string one = text.Replace("\r", "").Replace("\n", " / ").Trim();
        if (one.Length > 42) one = one.Substring(0, 42) + "…";
        return one;
    }
}

#endif
