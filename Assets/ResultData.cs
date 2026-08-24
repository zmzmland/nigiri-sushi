using UnityEngine;

public static class ResultData
{
    // =====================================================
    //  ★ 寿司の値段はここだけ変えれば全体に反映されます
    // =====================================================
    /// <summary>寿司1貫の値段（円）。</summary>
    public const int PricePerPiece = 500;

    /// <summary>売上（円）。正解した貫数 × PricePerPiece。</summary>
    public static int score = 0;

    public static int correctCount = 0;

    public static int totalOrders = 0;

    // タイム
    public static float clearTime = 0f;

    public static float scene1Time = 0f;
    public static float scene2Time = 0f;
    public static float scene3Time = 0f;

    /// <summary>スピードボーナス（円）。売上に足される加算額。</summary>
    public static int timeBonusYen = 0;

    /// <summary>最終的な売上（円）。score + timeBonusYen。</summary>
    public static int finalScore = 0;

    /// <summary>今回の結果をランキングに登録済みか（二重登録を防ぐ）。</summary>
    public static bool scoreRegistered = false;

    /// <summary>今回が何位だったか（1 始まり）。0 は未登録。</summary>
    public static int lastRank = 0;

    // 撮影トリガー送信済みで、Python の判定待ちかどうか。
    public static bool isProcessing = false;

    /// <summary>
    /// 注文が全部出そろって、判定を受け付けてよい状態か。
    /// 注文提示中に Space を押されると、Python が判定して result.txt を
    /// 書いてしまい、注文が出そろった瞬間にそれを拾って即遷移する
    /// 事故が起きていました。撮影そのものを止めるためのフラグです。
    /// </summary>
    public static bool ordersReady = false;

    // WaitScene の次にどこへ行くか。
    // WaitScene を「1面→2面」と「2面→3面」で使い回すための行き先です。
    public static string nextAfterWait = "Game Scene 2";

    /// <summary>ゲーム開始時（Game Scene の Customer.Start）に呼ぶ。</summary>
    public static void ResetAll()
    {
        score = 0;
        correctCount = 0;
        totalOrders = 0;
        clearTime = 0f;
        scene1Time = 0f;
        scene2Time = 0f;
        scene3Time = 0f;
        timeBonusYen = 0;
        finalScore = 0;
        scoreRegistered = false;
        lastRank = 0;
        isProcessing = false;
        ordersReady = false;
        nextAfterWait = "Game Scene 2";
    }
}
