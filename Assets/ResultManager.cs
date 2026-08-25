using UnityEngine;
using TMPro;

public class ResultManager : MonoBehaviour
{
    [Header("表示先")]
    public TextMeshProUGUI scoreText;

    [Header("ランキング")]
    [Tooltip("この順位以内に入ったら「番付入り」と出す")]
    public int highlightRank = 5;

    [Tooltip("売上がこの額以上のときだけランキングに登録する")]
    public int minScoreToRegister = 1;

    void Start()
    {
        // 客1 + 客2 + 客3 の合計時間
        float totalTime =
            ResultData.scene1Time +
            ResultData.scene2Time +
            ResultData.scene3Time;

        int minutes = Mathf.FloorToInt(totalTime / 60);
        int seconds = Mathf.FloorToInt(totalTime % 60);

        // ---- ランキングに登録する ----
        // シーンが読み直されても二重登録しないよう、フラグで守る
        if (!ResultData.scoreRegistered &&
            ResultData.totalOrders > 0 &&
            ResultData.finalScore >= minScoreToRegister)
        {
            ResultData.lastRank = RankingData.Register(
                ResultData.finalScore,
                ResultData.correctCount,
                ResultData.totalOrders);

            ResultData.scoreRegistered = true;

            if (ResultData.lastRank <= highlightRank) GameAudio.RankIn();
        }

        // "N0" で 5000 → "5,000" のように3桁区切りになります
        string uriage = ResultData.score.ToString("N0");
        string bonus  = ResultData.timeBonusYen.ToString("N0");
        string goukei = ResultData.finalScore.ToString("N0");

        var sb = new System.Text.StringBuilder();

        sb.Append("提供数 : ");
        sb.Append(ResultData.correctCount);
        sb.Append(" / ");
        sb.Append(ResultData.totalOrders);
        sb.Append(" 貫");

        sb.Append("\n\n売上 : ");
        sb.Append(uriage);
        sb.Append("円　（");
        sb.Append(ResultData.PricePerPiece);
        sb.Append("円 × ");
        sb.Append(ResultData.correctCount);
        sb.Append("貫）");

        sb.Append("\n総対応時間 : ");
        sb.Append(minutes);
        sb.Append("分 ");
        sb.Append(seconds);
        sb.Append("秒");

        sb.Append("\nスピードボーナス : +");
        sb.Append(bonus);
        sb.Append("円");

        sb.Append("\n\n合計 : ");
        sb.Append(goukei);
        sb.Append("円");

        // ---- 順位 ----
        if (ResultData.lastRank > 0)
        {
            sb.Append("\n\n今回の順位 : ");
            sb.Append(ResultData.lastRank);
            sb.Append("位");

            if (ResultData.lastRank <= highlightRank)
            {
                sb.Append("　★番付入り★");
            }
        }

        scoreText.text = sb.ToString();
    }
}
