using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
            $"提供数 : {ResultData.correctCount} / {ResultData.totalOrders} 貫",

            $"売上 : {ResultData.score:N0}円" +
            $"　（{ResultData.PricePerPiece}円 × {ResultData.correctCount}貫）",

            $"総対応時間 : {minutes}分 {seconds}秒",

            $"スピードボーナス : +{ResultData.timeBonusYen:N0}円",
        };

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

        // 売上に応じた音。段の区切りは GameAudio の Inspector で変えられます
        GameAudio.Result(final);
    }

    private string BuildTotal(int value, bool showRank)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("合計");
        sb.Append($"{value:N0}円");

        if (showRank && ResultData.lastRank > 0)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.Append($"{ResultData.lastRank}位");

            if (ResultData.lastRank <= highlightRank) sb.Append("　★番付入り★");
        }

        return sb.ToString();
    }

    // =====================================================
    //  合計テキストが無ければ作る
    // =====================================================
    private void EnsureTotalText()
    {
        if (totalText != null) return;

        Canvas canvas = scoreText != null
            ? scoreText.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();

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
