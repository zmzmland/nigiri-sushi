using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 2面（Game Scene 2）の客。
/// 修正点:
///   * ResultData.correctCount = +point → += point （単項プラスの書き間違い）
///   * scene1Time を上書きしていたのを削除（1面のタイムが消えていた）
///   * ファイル IO を Update から外し、コルーチンのポーリングに変更
///   * do-while(true) の無限ループ（種類が少ないと Unity がフリーズする）を撤去
///   * ファイルパスを GamePaths に統一
///   * 注文提示が終わるまで result.txt を受け付けないようにした
/// </summary>
public class Customer2 : MonoBehaviour
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

    [Header("Order Display")]
    [Tooltip("注文の名前を画像で出すための表。Project で作った " +
             "「注文の文字画像」アセットをドラッグしてください")]
    public OrderNameArtSet nameArt;

    [Tooltip("画像が無いネタを文字で出すときのフォント。" +
             "NotoSansJP SDF をドラッグしてください")]
    public TMP_FontAsset orderFont;

    [Header("Order Board")]
    [Tooltip("画面上に注文票を出す。覚えなくても遊べるようになります")]
    public OrderBoardSettings orderBoard = new OrderBoardSettings();

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
    [Tooltip("1面+2面の合計時間がこの値以下なら、下の金額をボーナスにする（昇順で並べること）")]
    public float[] timeThresholds = { 50f, 60f, 75f, 90f, 105f };

    [Tooltip("上の各段階でもらえるボーナス（円）")]
    public int[] timeBonusAmounts = { 2000, 1500, 1000, 500, 0 };

    [Header("Scene Flow")]
    // ★3面化に伴い、行き先は ResultScene から WaitScene に変わりました。
    //   既存シーンには古い値 "ResultScene" が保存されているので、
    //   Unity の Inspector で手動で "WaitScene" に直してください。
    public string nextSceneName = "WaitScene";

    [Tooltip("WaitScene のあとに進む面")]
    public string sceneAfterWait = "Game Scene 3";

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

    // 実行時に生成する注文票
    private OrderBoard board;

    // 吹き出しに文字を出すとき用（漢字・英語モード）
    private TextMeshProUGUI orderText;

    private Coroutine resultWatchCoroutine;

    void Start()
    {
        // 2面では ResultData をリセットしない（1面のスコアを引き継ぐ）
        GamePaths.SafeWrite(GamePaths.ResultPath, "");
        GamePaths.SafeWrite(GamePaths.OrderPath, "");
        GamePaths.SafeDelete(GamePaths.TriggerPath);
        ResultData.isProcessing = false;
        ResultData.ordersReady = false;

        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        baseSize = CustomerSpriteFit.CaptureBaseSize(rectTransform, name);

        PrepareOrderDisplay();

        if (speechBubble == null)
        {
            Debug.LogError("SpeechBubble が設定されていません");
        }
        else
        {
            bubbleCanvasGroup = speechBubble.GetComponent<CanvasGroup>();
            speechBubble.SetActive(false);
            if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1f;
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

        GameAudio.CustomerCome();

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

        // 先に注文を全部決めてしまう。注文票に一度に並べられるようにするため。
        for (int i = 0; i < orderCount; i++) orderHistory.Add(PickNextOrder());

        // 板前モードは注文票を出さない（覚える勝負）
        if (GameMode.KeepOrderBoard)
        {
            board = OrderBoard.Create(this, orderCount, orderBoard);
            if (board != null && !orderBoard.revealProgressively) board.FillAll(orderHistory);
        }

        for (int i = 0; i < orderCount; i++)
        {
            Sprite order = orderHistory[i];

            if (board != null && orderBoard.revealProgressively) board.Set(i, order);

            if (speechBubble != null) speechBubble.SetActive(true);
            if (bubbleCanvasGroup != null) bubbleCanvasGroup.alpha = 1f;
            ShowOrder(order);
            GameAudio.Order();

            yield return new WaitForSeconds(showTime);

            if (speechBubble != null) speechBubble.SetActive(false);

            yield return new WaitForSeconds(hideTime);
        }

        SaveOrderFile();
    }

    // =========================
    // 注文の見せ方（イラスト / 漢字 / 英語）
    // =========================
    /// <summary>
    /// モードに応じて、吹き出しの中身を絵にするか文字にするかを決める。
    /// 文字モードのときは、絵の代わりに TextMeshPro を作って重ねる。
    /// </summary>
    private void PrepareOrderDisplay()
    {
        // 注文票にも同じものを使わせる（別々にドラッグしなくて済むように）
        if (orderBoard != null)
        {
            if (orderBoard.labelFont == null) orderBoard.labelFont = orderFont;
            if (orderBoard.nameArt == null)   orderBoard.nameArt   = nameArt;
        }

        if (GameMode.Style == OrderStyle.イラスト || orderImage == null) return;

        // 文字画像は横長なので、つぶれないように枠に合わせる
        orderImage.preserveAspect = true;

        var go = new GameObject("OrderText", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(orderImage.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        orderText = go.GetComponent<TextMeshProUGUI>();

        if (orderFont != null) orderText.font = orderFont;
        else Debug.LogWarning("Order Font が未設定です。日本語が □ になるなら " +
                              "NotoSansJP SDF を入れてください");

        orderText.color = Color.black;   // 吹き出しは白なので黒文字
        orderText.alignment = TextAlignmentOptions.Center;
        orderText.enableAutoSizing = true;
        orderText.fontSizeMin = 14f;
        orderText.fontSizeMax = 90f;
        orderText.raycastTarget = false;
        orderText.text = "";
    }

    /// <summary>
    /// 吹き出しに1件ぶんの注文を出す。
    /// 優先順は「名前の画像」→「寿司のイラスト」→「フォントの文字」。
    /// </summary>
    private void ShowOrder(Sprite order)
    {
        Sprite art = (nameArt != null && GameMode.Style != OrderStyle.イラスト)
            ? nameArt.Find(order.name, GameMode.Style)
            : null;

        // 1) 名前の画像がある（筆文字など）
        if (art != null)
        {
            if (orderImage != null) { orderImage.enabled = true; orderImage.sprite = art; }
            if (orderText != null) orderText.text = "";
            return;
        }

        // 2) イラストモード
        if (GameMode.Style == OrderStyle.イラスト)
        {
            if (orderImage != null) { orderImage.enabled = true; orderImage.sprite = order; }
            return;
        }

        // 3) 画像が無い文字モード → フォントで出す
        if (orderText != null)
        {
            orderText.text = GameMode.LabelFor(order);
            if (orderImage != null) orderImage.enabled = false;
        }
        else if (orderImage != null)
        {
            orderImage.enabled = true;
            orderImage.sprite = order;
        }
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

            GameAudio.JudgeResult(point, orderCount);

            // ★ここが元コードのバグ。= +point ではなく += point。
            ResultData.correctCount += point;
            ResultData.score        += point * ResultData.PricePerPiece;

            elapsedTime = (timerStart < 0f) ? 0f : Time.time - timerStart;
            ResultData.scene2Time   = elapsedTime;   // scene1Time は触らない
            ResultData.isProcessing = false;

            Debug.Log($"2面 正解 {point}/{orderCount}  time={elapsedTime:F1}s");

            CalculateFinalScore();

            GamePaths.SafeWrite(GamePaths.ResultPath, "");

            // WaitScene に「次はここへ」と伝えてから移る
            ResultData.nextAfterWait = sceneAfterWait;

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

    /// <summary>
    /// 2面時点の暫定スコア。3面が終わったときに Customer3 が
    /// 3面合計で計算し直すので、ここの値は最終結果には残りません。
    /// </summary>
    void CalculateFinalScore()
    {
        float totalTime = ResultData.scene1Time + ResultData.scene2Time;

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
