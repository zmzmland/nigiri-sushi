using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 3面（Game Scene 3）の客。
///
/// Customer2.cs をベースにした3面専用版です。2面との違いはこの3点だけです。
///   * タイムを ResultData.scene3Time に記録する（1面・2面のタイムを消さない）
///   * 最終スコアを 1面 + 2面 + 3面 の合計時間で計算する
///   * 判定が終わったら ResultScene へ進む
///
/// 4人目を足したくなったら、このファイルを複製して
/// scene3Time → scene4Time に読み替えてください。
/// </summary>
public class Customer3 : MonoBehaviour
{
    [Header("Move Settings")]
    public float targetX = 0f;
    public float moveTime = 1f;

    [Header("Sprite Settings")]
    [Tooltip("正面を向いたときの絵")]
    public Sprite customerSprite;

    [Tooltip("正面の絵に差し替えるとき、比率がつぶれないように枠を合わせ直す")]
    public CustomerSpriteFit.FitMode fitMode = CustomerSpriteFit.FitMode.高さをそろえる;

    [Tooltip("正面の絵だけ大きさを微調整したいとき（1 でそのまま）")]
    public float frontScale = 1f;

    [Tooltip("正面の絵だけ位置を微調整したいとき")]
    public Vector2 frontOffset = Vector2.zero;

    [Tooltip("足元の位置を保つ（絵の高さが変わっても浮き沈みしない）")]
    public bool keepFeetOnGround = true;

    [Header("Speech Bubble")]
    public GameObject speechBubble;
    public Image orderImage;

    [Header("Order Settings")]
    public Sprite[] orderSprites;
    public int orderCount = 5;
    public float showTime = 2f;
    public float hideTime = 0.5f;

    [Header("Fade Settings")]
    public float fadeDelay = 2f;
    public float fadeTime = 1.5f;

    [Header("Result File Watch Settings")]
    public float resultCheckInterval = 0.2f;

    [Tooltip("判定を受け付け始めてからこの秒数、結果が来なければ警告を出す")]
    public float noResultWarnAfter = 90f;

    [Header("Speed Bonus Table")]
    [Tooltip("1面+2面+3面の合計時間がこの値以下なら、下の金額をボーナスにする（昇順で並べること）")]
    public float[] timeThresholds = { 70f, 85f, 100f, 115f, 130f };

    [Tooltip("上の各段階でもらえるボーナス（円）。ここが最終結果になります")]
    public int[] timeBonusAmounts = { 3000, 2000, 1000, 500, 0 };

    [Header("Scene Flow")]
    public string nextSceneName = "ResultScene";

    private RectTransform rectTransform;
    private Image image;
    private CanvasGroup bubbleCanvasGroup;

    // 差し替え前（横向きのとき）の枠の大きさ
    private Vector2 baseSize;

    private readonly List<Sprite> orderHistory = new List<Sprite>();

    private float timerStart = -1f;
    private float elapsedTime = 0f;
    private bool ordersReady = false;
    private bool judged = false;

    // 判定待ちが長引いていないかの監視用
    private float readySince = -1f;
    private bool warnedLongWait = false;

    private Coroutine resultWatchCoroutine;

    void Start()
    {
        // 3面では ResultData をリセットしない（1面・2面のスコアを引き継ぐ）
        GamePaths.SafeWrite(GamePaths.ResultPath, "");
        GamePaths.SafeWrite(GamePaths.OrderPath, "");
        GamePaths.SafeDelete(GamePaths.TriggerPath);
        ResultData.isProcessing = false;
        ResultData.ordersReady = false;

        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        baseSize = CustomerSpriteFit.CaptureBaseSize(rectTransform, name);

        if (speechBubble == null)
        {
            Debug.LogError("SpeechBubble が設定されていません");
        }
        else
        {
            bubbleCanvasGroup = speechBubble.GetComponent<CanvasGroup>();
            speechBubble.SetActive(false);
            if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1f;
            else Debug.LogError("SpeechBubble に CanvasGroup が付いていません");
        }

        StartCoroutine(CustomerFlow());
        resultWatchCoroutine = StartCoroutine(WatchResultFile());
    }

    IEnumerator CustomerFlow()
    {
        yield return new WaitForSeconds(1f);

        Vector2 startPos  = rectTransform.anchoredPosition;
        Vector2 targetPos = new Vector2(targetX, startPos.y);

        float t = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime;
            rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t / moveTime);
            yield return null;
        }
        rectTransform.anchoredPosition = targetPos;

        yield return new WaitForSeconds(0.5f);

        // 横向き → 正面。絵の比率が違っても、つぶれないように枠を合わせ直す
        if (customerSprite != null)
        {
            CustomerSpriteFit.Apply(
                image, customerSprite, baseSize,
                fitMode, frontScale, frontOffset, keepFeetOnGround);
        }

        timerStart = Time.time;

        yield return StartCoroutine(MultipleOrders());

        ordersReady = true;

        yield return StartCoroutine(FadeOutBubble());
    }

    IEnumerator MultipleOrders()
    {
        if (orderSprites == null || orderSprites.Length == 0)
        {
            Debug.LogError("orderSprites が設定されていません");
            ordersReady = true;
            yield break;
        }

        if (orderImage == null)
        {
            Debug.LogError("OrderImage が設定されていません");
            ordersReady = true;
            yield break;
        }

        ResultData.totalOrders += orderCount;

        for (int i = 0; i < orderCount; i++)
        {
            Sprite order = PickNextOrder();
            orderHistory.Add(order);

            if (speechBubble != null) speechBubble.SetActive(true);
            if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1f;
            orderImage.sprite = order;

            yield return new WaitForSeconds(showTime);

            if (speechBubble != null) speechBubble.SetActive(false);

            yield return new WaitForSeconds(hideTime);
        }

        SaveOrderFile();
    }

    /// <summary>直前2件と同じものは選ばない。試行上限つきなのでフリーズしない。</summary>
    private Sprite PickNextOrder()
    {
        const int maxAttempts = 50;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Sprite candidate = orderSprites[Random.Range(0, orderSprites.Length)];

            int n = orderHistory.Count;
            bool wouldBeThreeInARow =
                n >= 2 &&
                orderHistory[n - 1] == candidate &&
                orderHistory[n - 2] == candidate;

            if (!wouldBeThreeInARow) return candidate;
        }

        return orderSprites[Random.Range(0, orderSprites.Length)];
    }

    IEnumerator WatchResultFile()
    {
        var wait = new WaitForSeconds(resultCheckInterval);

        while (!judged)
        {
            yield return wait;

            // CaptureTrigger と状態を共有する（撮影を受け付けてよいかの判断に使う）
            ResultData.ordersReady = ordersReady;

            if (!ordersReady)
            {
                // ★ 注文提示中に判定された結果は、ここで確実に捨てる。
                string stale = GamePaths.SafeRead(GamePaths.ResultPath);
                if (!string.IsNullOrEmpty(stale))
                {
                    Debug.LogWarning($"注文提示中の判定結果を破棄しました: \"{stale}\"");
                    GamePaths.SafeWrite(GamePaths.ResultPath, "");
                }
                continue;
            }

            if (readySince < 0f) readySince = Time.time;
            WarnIfWaitingTooLong();

            string result = GamePaths.SafeRead(GamePaths.ResultPath);
            if (string.IsNullOrEmpty(result)) continue;

            if (!int.TryParse(result, out int point))
            {
                Debug.LogWarning($"result.txt の内容が数値ではありません: \"{result}\"");
                GamePaths.SafeWrite(GamePaths.ResultPath, "");
                continue;
            }

            judged = true;
            ResultData.ordersReady = false;   // 遷移中に撮影されないようにする

            point = Mathf.Clamp(point, 0, orderCount);

            ResultData.correctCount += point;
            ResultData.score        += point * ResultData.PricePerPiece;

            elapsedTime = (timerStart < 0f) ? 0f : Time.time - timerStart;
            ResultData.scene3Time   = elapsedTime;   // scene1Time / scene2Time は触らない
            ResultData.isProcessing = false;

            Debug.Log($"3面 正解 {point}/{orderCount}  time={elapsedTime:F1}s");

            CalculateFinalScore();

            GamePaths.SafeWrite(GamePaths.ResultPath, "");
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }
    }

    /// <summary>判定待ちが長引いたら一度だけ警告する（Python 停止の検知）。</summary>
    private void WarnIfWaitingTooLong()
    {
        if (warnedLongWait || readySince < 0f) return;
        if (Time.time - readySince < noResultWarnAfter) return;

        warnedLongWait = true;
        Debug.LogWarning(
            $"{noResultWarnAfter:F0} 秒たっても判定結果が来ていません。\n" +
            "  画像認識（Python）が動いているか確認してください。\n" +
            "  復旧できない場合は Ctrl+Shift+N でこの面を飛ばせます。");
    }

    IEnumerator FadeOutBubble()
    {
        yield return new WaitForSeconds(fadeDelay);
        if (bubbleCanvasGroup == null) yield break;

        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            bubbleCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            yield return null;
        }

        bubbleCanvasGroup.alpha = 0f;
        if (speechBubble != null) speechBubble.SetActive(false);
    }

    void CalculateFinalScore()
    {
        float totalTime =
            ResultData.scene1Time +
            ResultData.scene2Time +
            ResultData.scene3Time;

        int bonus = 0;
        int n = Mathf.Min(timeThresholds.Length, timeBonusAmounts.Length);
        for (int i = 0; i < n; i++)
        {
            if (totalTime <= timeThresholds[i])
            {
                bonus = timeBonusAmounts[i];
                break;
            }
        }

        ResultData.timeBonusYen = bonus;
        ResultData.finalScore   = ResultData.score + bonus;

        Debug.Log($"totalTime={totalTime:F1}  bonus=+{bonus}円  合計={ResultData.finalScore}円");
    }

    void SaveOrderFile()
    {
        var sb = new System.Text.StringBuilder();
        foreach (Sprite s in orderHistory) sb.AppendLine(s.name);

        GamePaths.SafeWrite(GamePaths.OrderPath, sb.ToString());
        Debug.Log($"order.txt 保存: {GamePaths.OrderPath}");
    }

    void OnDestroy()
    {
        if (resultWatchCoroutine != null) StopCoroutine(resultWatchCoroutine);
    }
}
