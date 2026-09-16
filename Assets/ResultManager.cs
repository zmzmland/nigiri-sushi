using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// リザルト画面。
///
/// 明細を上から1行ずつ出し、最後に画面右側へ合計を大きく出します。
/// 一度に全部出すより、見ている人の視線が止まるので手応えが出ます。
///
/// 【置き方】
///   Score Text … 明細を出す TextMeshPro（今までどおり）
///   Total Text … 合計を出す TextMeshPro（空でよい。空なら実行時に右側へ作ります）
/// </summary>
public class ResultManager : MonoBehaviour
{
    [Header("表示先")]
    [Tooltip("明細（提供数・売上・時間・ボーナス）を出すテキスト")]
    public TextMeshProUGUI scoreText;

    [Tooltip("合計を出すテキスト。空なら実行時に画面右側へ作ります")]
    public TextMeshProUGUI totalText;

    [Header("演出")]
    [Tooltip("最初の1行が出るまでの待ち（秒）")]
    public float startDelay = 0.5f;

    [Tooltip("1行ずつ出す間隔（秒）")]
    public float lineInterval = 0.55f;

    [Tooltip("明細が出そろってから合計を出すまでの待ち（秒）")]
    public float totalDelay = 0.7f;

    [Tooltip("合計を 0 から数え上げる")]
    public bool countUp = true;

    [Tooltip("数え上げにかける時間（秒）")]
    public float countUpTime = 0.9f;

    [Header("合計テキストを自動で作るときの設定")]
    public float totalFontSize = 64f;
    public Color totalColor = Color.white;
    public Vector2 totalSize = new Vector2(520f, 320f);
    public float totalRightMargin = 80f;

    [Header("称号")]
    [Tooltip("合計のあとに「銀座の名店からスカウトが来た！」などを出す")]
    public bool showTitle = true;

    [Tooltip("称号を出すテキスト。空なら実行時に画面下へ作ります")]
    public TextMeshProUGUI titleText;

    [Tooltip("合計が出そろってから称号が出るまでの待ち（秒）")]
    public float titleDelay = 0.6f;

    [Tooltip("称号がポンと出る時間（秒）")]
    public float titlePopTime = 0.45f;

    [Header("称号テキストを自動で作るときの設定")]
    public float titleFontSize = 40f;
    public Vector2 titleSize = new Vector2(760f, 110f);
    public float titleBottomMargin = 36f;


    [Header("タイトルへ自動で戻る")]
    [Tooltip("全部出そろったあと、ひとりでにタイトルへ戻る。\n" +
             "展示では次のお客さんのために必ず戻しておきたいので、既定でオンです")]
    public bool autoReturnToTitle = true;

    [Tooltip("称号が出てから、タイトルへ戻るまでの秒数")]
    public float autoReturnSeconds = 5f;

    [Tooltip("戻る先のシーン名")]
    public string titleSceneName = "SampleScene";

    [Tooltip("あと何秒で戻るかを画面に出す")]
    public bool showReturnCountdown = true;

    [Header("ランキング")]
    [Tooltip("この順位以内に入ったら「番付入り」と出す")]
    public int highlightRank = 5;

    [Tooltip("売上がこの額以上のときだけランキングに登録する")]
    public int minScoreToRegister = 1;

    void Start()
    {
        RegisterRanking();

        if (scoreText != null) scoreText.text = "";
        if (totalText != null) totalText.text = "";
        if (titleText != null) titleText.text = "";

        StartCoroutine(ShowResult());
    }

    // =====================================================
    //  ランキング登録（表示より先に済ませる）
    // =====================================================
    private void RegisterRanking()
    {
        if (ResultData.scoreRegistered) return;
        if (ResultData.totalOrders <= 0) return;
        if (ResultData.finalScore < minScoreToRegister) return;

        ResultData.lastRank = RankingData.Register(
            ResultData.finalScore,
            ResultData.correctCount,
            ResultData.totalOrders);

        ResultData.scoreRegistered = true;
    }

    // =====================================================
    //  演出
    // =====================================================
    private IEnumerator ShowResult()
    {
        float totalTime =
            ResultData.scene1Time +
            ResultData.scene2Time +
            ResultData.scene3Time;

        int minutes = Mathf.FloorToInt(totalTime / 60);
        int seconds = Mathf.FloorToInt(totalTime % 60);

        var lines = new List<string>
        {
            GameMode.T($"提供数 : {ResultData.correctCount} / {ResultData.totalOrders} 貫",
                       $"Served : {ResultData.correctCount} / {ResultData.totalOrders} pcs"),

            GameMode.T($"売上 : {ResultData.score:N0}円" +
                       $"　（{ResultData.PricePerPiece}円 × {ResultData.correctCount}貫）",
                       $"Sales : ¥{ResultData.score:N0}" +
                       $"  (¥{ResultData.PricePerPiece} × {ResultData.correctCount})"),

            GameMode.T($"総対応時間 : {minutes}分 {seconds}秒",
                       $"Total time : {minutes}m {seconds}s"),

            GameMode.T($"スピードボーナス : +{ResultData.timeBonusYen:N0}円",
                       $"Speed bonus : +¥{ResultData.timeBonusYen:N0}"),
        };

        // 全問正解したときだけ、その行を足す。
        // 0円の行を出しても「取れなかった」が目立つだけなので出しません。
        if (ResultData.perfectBonusYen > 0)
        {
            lines.Add(GameMode.T($"全問正解ボーナス : +{ResultData.perfectBonusYen:N0}円",
                                 $"Perfect bonus : +¥{ResultData.perfectBonusYen:N0}"));
        }

        yield return new WaitForSecondsRealtime(startDelay);

        // --- 明細を上から1行ずつ ---
        var sb = new System.Text.StringBuilder();

        foreach (string line in lines)
        {
            sb.AppendLine(line);
            if (scoreText != null) scoreText.text = sb.ToString();

            GameAudio.Order();
            yield return new WaitForSecondsRealtime(lineInterval);
        }

        yield return new WaitForSecondsRealtime(totalDelay);

        // --- 合計を右側に ---
        EnsureTotalText();
        yield return StartCoroutine(ShowTotal());

        // --- 最後に称号 ---
        yield return StartCoroutine(ShowTitle());

        // --- タイトルへ戻る ---
        yield return StartCoroutine(ReturnToTitle());
    }

    /// <summary>
    /// しばらく見せてから、ひとりでにタイトルへ戻る。
    ///
    /// 展示では、遊び終わった画面が残ったままだと次の人が始められません。
    /// 係員が何もしなくても待機状態に戻るようにしておきます。
    /// 「営業再開」を押した場合は、そちらが先にシーンを変えるので
    /// このコルーチンは自動的に止まります。
    /// </summary>
    private IEnumerator ReturnToTitle()
    {
        if (!autoReturnToTitle) yield break;
        if (string.IsNullOrEmpty(titleSceneName)) yield break;

        returnAt = Time.unscaledTime + Mathf.Max(0f, autoReturnSeconds);

        while (Time.unscaledTime < returnAt)
        {
            returnRemain = returnAt - Time.unscaledTime;
            yield return null;
        }

        returnRemain = 0f;
        Debug.Log($"[ResultManager] {titleSceneName} へ戻ります");
        SceneManager.LoadScene(titleSceneName);
    }

    // 残り秒数の表示用
    private float returnAt = -1f;
    private float returnRemain = -1f;

    void OnGUI()
    {
        if (!showReturnCountdown || !autoReturnToTitle) return;
        if (returnRemain <= 0f) return;

        Matrix4x4 __m = UiScale.Begin();
        try
        {
            string text = GameMode.T(
                $"{Mathf.CeilToInt(returnRemain)} 秒後にタイトルへ戻ります",
                $"Back to the title in {Mathf.CeilToInt(returnRemain)}s");

            float w = Mathf.Min(420f, UiScale.W - 40f);
            float h = 34f;
            float x = (UiScale.W - w) / 2f;
            float y = UiScale.H - h - 16f;

            Color prev = GUI.color;

            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
            };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, h), text, style);

            GUI.color = prev;
        }
        finally { UiScale.End(__m); }
    }

    private IEnumerator ShowTotal()
    {
        if (totalText == null) yield break;

        int final = ResultData.finalScore;

        if (countUp && countUpTime > 0f && final > 0)
        {
            float t = 0f;
            while (t < countUpTime)
            {
                t += Time.unscaledDeltaTime;
                int shown = Mathf.RoundToInt(Mathf.Lerp(0f, final, t / countUpTime));
                totalText.text = BuildTotal(shown, showRank: false);
                yield return null;
            }
        }

        totalText.text = BuildTotal(final, showRank: true);
    }

    // =====================================================
    //  称号
    // =====================================================
    private IEnumerator ShowTitle()
    {
        int final = ResultData.finalScore;

        ResultTier tier = GameAudio.TierFor(final);
        string text = GameAudio.TitleOf(tier);

        // 称号を出さない設定・文言が空・テキストを作れない、のどれかなら
        // 音だけ鳴らして終わる（今までと同じ動き）
        if (!showTitle || string.IsNullOrEmpty(text))
        {
            GameAudio.Result(final);
            yield break;
        }

        yield return new WaitForSecondsRealtime(titleDelay);

        EnsureTitleText();

        if (titleText == null)
        {
            GameAudio.Result(final);
            yield break;
        }

        Color target = tier.titleColor;
        target.a = 1f;

        titleText.text = text;
        titleText.color = new Color(target.r, target.g, target.b, 0f);

        RectTransform rt = titleText.rectTransform;

        // 音と称号を同時に出す。ここが一番の見せ場なので重ねます
        GameAudio.Result(final);

        if (titlePopTime <= 0f)
        {
            rt.localScale = Vector3.one;
            titleText.color = target;
            yield break;
        }

        float t = 0f;
        while (t < titlePopTime)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / titlePopTime);

            float s = Mathf.LerpUnclamped(0.65f, 1f, EaseOutBack(p));
            rt.localScale = new Vector3(s, s, 1f);

            titleText.color = new Color(target.r, target.g, target.b, p);
            yield return null;
        }

        rt.localScale = Vector3.one;
        titleText.color = target;
    }

    /// <summary>行き過ぎてから戻る動き。ポンと出た感じになります。</summary>
    private static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = p - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void EnsureTitleText()
    {
        if (titleText != null) return;

        Canvas canvas = FindCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[ResultManager] Canvas が見つからないので称号を出せません");
            return;
        }

        var go = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = titleSize;
        rt.anchoredPosition = new Vector2(0f, titleBottomMargin);

        titleText = go.GetComponent<TextMeshProUGUI>();

        if (scoreText != null && scoreText.font != null) titleText.font = scoreText.font;

        titleText.fontSize = titleFontSize;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.textWrappingMode = TextWrappingModes.Normal;
        titleText.raycastTarget = false;
        titleText.text = "";
    }

    private Canvas FindCanvas()
    {
        if (scoreText != null)
        {
            Canvas c = scoreText.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }

        Canvas mine = GetComponentInParent<Canvas>();
        if (mine != null) return mine;

        return FindAnyObjectByType<Canvas>();
    }

    private string BuildTotal(int value, bool showRank)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine(GameMode.T("合計", "TOTAL"));
        sb.Append(GameMode.Yen(value));

        if (showRank && ResultData.lastRank > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(GameMode.T($"{ResultData.lastRank}位", $"#{ResultData.lastRank}"));

            if (ResultData.lastRank <= highlightRank)
                sb.Append(GameMode.T("　★番付入り★", "  ★ ON THE BOARD ★"));
        }

        return sb.ToString();
    }

    // =====================================================
    //  合計テキストが無ければ作る
    // =====================================================
    private void EnsureTotalText()
    {
        if (totalText != null) return;

        Canvas canvas = FindCanvas();

        if (canvas == null)
        {
            Debug.LogWarning("[ResultManager] Canvas が見つからないので合計を出せません");
            return;
        }

        var go = new GameObject("TotalText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.sizeDelta = totalSize;
        rt.anchoredPosition = new Vector2(-totalRightMargin, 0f);

        totalText = go.GetComponent<TextMeshProUGUI>();

        // 明細と同じフォントを使う（別途ドラッグしなくて済むように）
        if (scoreText != null && scoreText.font != null) totalText.font = scoreText.font;

        totalText.fontSize = totalFontSize;
        totalText.color = totalColor;
        totalText.alignment = TextAlignmentOptions.Right;
        totalText.textWrappingMode = TextWrappingModes.NoWrap;
        totalText.raycastTarget = false;
        totalText.text = "";
    }
}
