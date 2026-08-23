using UnityEngine;
using System.Collections.Generic;


public static class ResultData
{
    public static int score = 0;

    public static int correctCount = 0;

    public static int totalOrders = 0;

    // タイム
    public static float clearTime = 0f;

    public static float scene1Time = 0f;
    public static float scene2Time = 0f;

    //タイムボーナス
    public static float timeBonus = 1f;

    // 最終点数
    public static int finalScore = 0;

    public static bool isProcessing = false;
}
