using UnityEngine;
using TMPro;

public class ResultManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText;

    void Start()
    {
        // 客1 + 客2 の合計時間
        float totalTime =
            ResultData.scene1Time +
            ResultData.scene2Time;

        int minutes =
            Mathf.FloorToInt(totalTime / 60);

        int seconds =
            Mathf.FloorToInt(totalTime % 60);

        scoreText.text =
            "正解数 : " +
            ResultData.correctCount +
            " / " +
            ResultData.totalOrders +

            "\n\n基本点 : " +
            ResultData.score +

            "\n総対応時間 : " +
            minutes + "分 " +
            seconds + "秒" +

            "\nタイムボーナス : " +
            ResultData.timeBonus.ToString("F2") +

            "\n\n最終点 : " +
            ResultData.finalScore +
            " 点";
    }
}