using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// タイトル画面に上位ランキングを表示する。
///
/// 使い方:
///   1. タイトルシーン（SampleScene）の Canvas に
///      右クリック → UI → Text - TextMeshPro でテキストを作る
///   2. 「腕前」の下に置き、フォントを NotoSansJP SDF にする
///   3. 空の GameObject を作ってこのスクリプトを付け、
///      Inspector の Ranking Text にそのテキストをドラッグする
///
/// 係員用:
///   Ctrl + Shift + R を長押し（2秒）でランキングを全消しします。
///   展示の開始前にリセットする用です。誤爆しないよう長押しにしてあります。
/// </summary>
public class RankingBoard : MonoBehaviour
{
    [Header("表示先")]
    [Tooltip("ランキングを流し込む TextMeshPro のテキスト")]
    public TextMeshProUGUI rankingText;

    [Header("表示設定")]
    [Tooltip("何位まで表示するか")]
    public int showCount = 5;

    [Tooltip("見出しの文字。空にすると見出しを出しません")]
    public string heading = "番付";

    [Tooltip("見出しにモード名を付ける（「板前 の番付」のように）")]
    public bool showModeInHeading = true;

    [Tooltip("順位を漢数字にする（一位・二位…）。外すと 1位・2位…")]
    public bool useKanjiNumbers = true;

    [Tooltip("記録が1件も無いときに出す文字")]
    public string emptyMessage = "まだ記録がありません";

    [Header("係員用")]
    [Tooltip("Ctrl+Shift+R の長押しでランキングを消せるようにする")]
    public bool allowOperatorReset = true;

    [Tooltip("消去するまでの長押し秒数")]
    public float resetHoldSeconds = 2f;

    private static readonly string[] Kanji =
        { "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };

    private float resetHeldFor = 0f;

    void Start()
    {
        if (rankingText == null)
        {
            Debug.LogError("[RankingBoard] Ranking Text が設定されていません。" +
                           "Inspector にテキストをドラッグしてください。");
            return;
        }

        Refresh();
    }

    void Update()
    {
        if (!allowOperatorReset) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        bool combo =
            (kb.leftCtrlKey.isPressed  || kb.rightCtrlKey.isPressed) &&
            (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) &&
            kb.rKey.isPressed;

        if (!combo)
        {
            resetHeldFor = 0f;
            return;
        }

        resetHeldFor += Time.unscaledDeltaTime;

        if (resetHeldFor >= resetHoldSeconds)
        {
            resetHeldFor = 0f;
            RankingData.ClearAll();   // 3モードまとめて消す
            Refresh();
        }
    }

    /// <summary>ランキングを読み直して表示を作り直す。</summary>
    public void Refresh()
    {
        if (rankingText == null) return;

        RankingList list = RankingData.Load();

        var sb = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(heading))
        {
            sb.AppendLine(showModeInHeading
                ? $"{GameMode.DisplayName} の{heading}"
                : heading);
        }

        if (list.entries.Count == 0)
        {
            sb.Append(emptyMessage);
            rankingText.text = sb.ToString();
            return;
        }

        int n = Mathf.Min(showCount, list.entries.Count);

        for (int i = 0; i < n; i++)
        {
            RankingEntry e = list.entries[i];

            string rank = useKanjiNumbers && i < Kanji.Length
                ? Kanji[i] + "位"
                : (i + 1) + "位";

            sb.Append(rank);
            sb.Append("　");
            sb.Append(e.score.ToString("N0"));
            sb.Append("円");

            if (i < n - 1) sb.AppendLine();
        }

        rankingText.text = sb.ToString();
    }

    void OnGUI()
    {
        // 長押し中だけ進捗を出す（QuitHandler と同じ見た目）
        if (!allowOperatorReset || resetHeldFor <= 0.15f) return;

        float ratio = Mathf.Clamp01(resetHeldFor / resetHoldSeconds);
        float barW = 220f;
        float barH = 6f;
        float x = (Screen.width - barW) / 2f;
        float y = Screen.height - 110f;

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(x, y, barW, barH), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y, barW * ratio, barH), Texture2D.whiteTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y - 24f, barW, 20f), "ランキングを消去しています…", style);

        GUI.color = Color.white;
    }
}
