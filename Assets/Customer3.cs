using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

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

    [Header("Speed Bonus")]
    [Tooltip("1貫あたりの持ち時間（秒）。" +
             "基準 = この値 × 注文数。貫数が増える面ほど基準も伸びます。" +
             "基準より早く終えた分だけがお金になります")]
    public float secondsPerPiece = 8f;

    [Tooltip("1秒早いごとにもらえる金額（円）")]
    public int yenPerSecond = 100;

    [Tooltip("この面を全問正解したときの追加ボーナス（円）。0 で無効")]
    public int perfectBonus = 1000;

    [Tooltip("直前の客が注文したネタを、この客の注文から外す。\n" +
             "ネタの種類が足りないときは自動で解除されます")]
    public bool avoidPreviousCustomer = true;

    [Header("注文の文字の大きさ（吹き出し）")]
    [Tooltip("吹き出しに文字で注文を出すときの大きさ。\n" +
             "自動調整の下限と上限です。吹き出しに収まる範囲で\n" +
             "できるだけ大きく表示されます。\n" +
             "下限と上限を同じ値にすると、サイズが固定になります")]
    public float orderFontMin = 14f;

    public float orderFontMax = 90f;

    [Header("判定結果の表示")]
    [Tooltip("判定のあと、この面の正解数を出す秒数。0 にすると出しません。\n" +
             "認識中はネタ名を隠しているので、ここが結果を知る唯一の場面です")]
    public float judgeDisplaySeconds = 2.8f;

    [Tooltip("札の上に出す絵。WaitScene で使っていた「へいお待ち」の画像を\n" +
             "そのままドラッグできます。空なら文字で「へい、お待ち！」と出します")]
    public Sprite judgeArt;

    [Tooltip("その絵の高さ（ピクセル）")]
    public float judgeArtHeight = 70f;

    [Tooltip("絵の後ろに明るい札を敷く。\n" +
             "hey!.png のような黒い筆文字は、暗い背景だと消えてしまうため")]
    public bool judgeArtPlate = true;

    [Tooltip("その札の色。生成りの紙のような色にしてあります")]
    public Color judgeArtPlateColor = new Color(0.96f, 0.93f, 0.86f, 1f);

    [Header("経過時間の表示")]
    [Tooltip("画面の上に、経過秒数と残りボーナスを出す")]
    public bool showTimer = true;

    [Tooltip("画面の上端からの距離")]
    public float timerTopMargin = 14f;

    [Header("Scene Flow")]
    public string nextSceneName = "ResultScene";

    private RectTransform rectTransform;
    private Image image;
    private CanvasGroup bubbleCanvasGroup;

    // 差し替え前（横向きのとき）の枠の大きさ
    private Vector2 baseSize;

    private readonly List<Sprite> orderHistory = new List<Sprite>();


    // 判定結果の表示用（この面ぶん）
    private bool  judgeShowing = false;
    private int   judgePoint = 0;
    private int   judgeSpeedYen = 0;
    private int   judgePerfectYen = 0;

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
        // 3面では ResultData をリセットしない（1面・2面のスコアを引き継ぐ）
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
        PickOrders();

        // 注文票を出すかどうかは、この面の Order Board → Lifetime で決まります
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

        // 「注文が終わったら消える」設定なら、ここで札を下げる
        if (board != null && orderBoard.lifetime == OrderBoardLifetime.注文が終わったら消える)
        {
            board.FadeOutAndHide();
        }
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
        orderText.textWrappingMode = TextWrappingModes.Normal;
        // 大きさは Inspector の「注文の文字の大きさ（吹き出し）」で変えられます
        orderText.enableAutoSizing = (orderFontMax > orderFontMin);
        orderText.fontSizeMin = orderFontMin;
        orderText.fontSizeMax = Mathf.Max(orderFontMin, orderFontMax);
        if (!orderText.enableAutoSizing) orderText.fontSize = orderFontMin;
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
            orderText.text = GameMode.WrapForDisplay(GameMode.LabelFor(order));
            if (orderImage != null) orderImage.enabled = false;
        }
        else if (orderImage != null)
        {
            orderImage.enabled = true;
            orderImage.sprite = order;
        }
    }

    /// <summary>
    /// この客の注文をまとめて決める。
    ///
    /// 同じネタが2回出ないよう、種類の中から重複なしで選びます。
    /// ネタの種類が注文数より少ないときだけ重複を許し、警告を出します。
    /// </summary>
    private void PickOrders()
    {
        orderHistory.Clear();

        // 同じスプライトが複数登録されていても1種として数える
        var kinds = new List<Sprite>();
        foreach (Sprite s in orderSprites)
        {
            if (s != null && !kinds.Contains(s)) kinds.Add(s);
        }

        if (kinds.Count == 0)
        {
            Debug.LogError("orderSprites に有効なスプライトがありません");
            return;
        }

        // --- 直前の客と同じネタを避ける ---
        // 「さっきも同じのを頼まれた」が続くと、単調に見えてしまうため。
        // ただし種類が足りないときは、避けるより注文が成立するほうを優先します。
        var candidates = new List<Sprite>(kinds);

        if (avoidPreviousCustomer && ResultData.lastOrderNames.Count > 0)
        {
            var fresh = new List<Sprite>();
            foreach (Sprite s in kinds)
            {
                if (!ResultData.lastOrderNames.Contains(s.name)) fresh.Add(s);
            }

            if (fresh.Count >= orderCount)
            {
                candidates = fresh;
            }
            else
            {
                Debug.LogWarning(
                    $"直前の客のネタを除くと {fresh.Count} 種しか残らず、" +
                    $"注文数 {orderCount} に足りません。今回は重複を許します。" +
                    "Order Sprites を増やすか Order Count を減らしてください。");
            }
        }

        var pool = new List<Sprite>(candidates);
        bool warned = false;

        for (int i = 0; i < orderCount; i++)
        {
            if (pool.Count == 0)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning(
                        $"ネタが {kinds.Count} 種しかないのに注文数が {orderCount} です。" +
                        "同じネタが複数回出ます。Order Sprites を増やすか Order Count を減らしてください。");
                }
                pool.AddRange(kinds);
            }

            int k = Random.Range(0, pool.Count);
            orderHistory.Add(pool[k]);
            pool.RemoveAt(k);
        }

        // 次の客のために、今回のネタを覚えておく
        ResultData.lastOrderNames.Clear();
        foreach (Sprite s in orderHistory) ResultData.lastOrderNames.Add(s.name);
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

            ResultData.correctCount += point;
            ResultData.score        += point * ResultData.PricePerPiece;

            elapsedTime = (timerStart < 0f) ? 0f : Time.time - timerStart;
            ResultData.scene3Time   = elapsedTime;   // scene1Time / scene2Time は触らない
            ResultData.isProcessing = false;

            Debug.Log($"3面 正解 {point}/{orderCount}  time={elapsedTime:F1}s");

            CalculateFinalScore(point);

            GamePaths.SafeWrite(GamePaths.ResultPath, "");
            // この面の結果を見せてから次へ。
            // 認識中はネタ名を隠しているので、
            // 「合っていたか」を知る場面はここだけです。
            yield return ShowJudgeResult(point);

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
    /// この面の売上を確定する。
    ///
    /// スピードボーナスは「基準時間より早く終えた秒数 × 単価」です。
    /// 段階式ではなく連続式なので、1秒の差がそのまま点差になります。
    /// （以前は「30秒以内なら一律1000円」だったため、
    ///   15秒で終える人も25秒の人も同額になっていました）
    ///
    /// 基準時間は 1貫あたりの秒数 × 注文数。
    /// 注文が増える面ほど基準も伸びるので、面をまたいで公平です。
    ///
    /// 面ごとに足していくので、最後の面が全部を計算し直す必要はありません。
    /// </summary>
    void CalculateFinalScore(int point)
    {
        float limit = secondsPerPiece * Mathf.Max(1, orderCount);
        float saved = limit - elapsedTime;

        int speed = (saved <= 0f) ? 0 : Mathf.RoundToInt(saved * yenPerSecond);

        // 全問正解のごほうび。惜しい人との差をはっきりさせる
        int perfect = (orderCount > 0 && point >= orderCount) ? perfectBonus : 0;

        judgeSpeedYen   = speed;
        judgePerfectYen = perfect;

        ResultData.timeBonusYen    += speed;
        ResultData.perfectBonusYen += perfect;

        ResultData.finalScore =
            ResultData.score +
            ResultData.timeBonusYen +
            ResultData.perfectBonusYen;

        Debug.Log(
            $"3面 ボーナス  速さ +{speed}円（{elapsedTime:F1}秒 / 基準 {limit:F0}秒）" +
            $"  全問正解 +{perfect}円  合計 {ResultData.finalScore}円");
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

    // =====================================================
    //  経過時間の表示
    //
    //  時計が見えないと、人は速くなろうとしません。
    //  いま何秒か、あといくらもらえるかを常に見せます。
    //
    //  Canvas に何も置かなくてよいよう OnGUI で描いています。
    // =====================================================
    void OnGUI()
    {
        // 画面の大きさに合わせて表示全体を拡大する。
        // OnGUI はピクセルで描くので、これが無いとフルスクリーンで
        // 文字だけ小さいままになります。倍率は UiScale.Extra。
        Matrix4x4 __m = UiScale.Begin();
        try { DrawGui(); }
        finally { UiScale.End(__m); }
    }

    private void DrawGui()
    {
        // 判定結果が出ている間は、それだけを見せる
        if (judgeShowing) { DrawJudgeResult(); return; }

        if (!showTimer || timerStart < 0f) return;

        float t = judged ? elapsedTime : (Time.time - timerStart);
        float limit = Mathf.Max(0.01f, secondsPerPiece * Mathf.Max(1, orderCount));
        float saved = limit - t;

        int yen = (saved <= 0f) ? 0 : Mathf.RoundToInt(saved * yenPerSecond);
        float remain = Mathf.Clamp01(saved / limit);

        float w = Mathf.Min(340f, UiScale.W - 40f);
        float h = 58f;
        float x = (UiScale.W - w) / 2f;
        float y = timerTopMargin;

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

        // 残りボーナスのバー。減っていくのが見えると人は急ぎます
        GUI.color = (yen > 0)
            ? new Color(1f, 0.84f, 0.35f, 0.9f)
            : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        GUI.DrawTexture(new Rect(x, y + h - 7f, w * remain, 7f), Texture2D.whiteTexture);

        GUI.color = Color.white;

        var timeStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 30,
            fontStyle = FontStyle.Bold,
        };
        timeStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(x + 16f, y, w * 0.5f, h - 7f),
                  GameMode.T($"{t:F1}秒", $"{t:F1}s"), timeStyle);

        var yenStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 22,
        };
        yenStyle.normal.textColor = (yen > 0)
            ? new Color(1f, 0.9f, 0.55f)
            : new Color(0.7f, 0.7f, 0.7f);
        GUI.Label(new Rect(x + w * 0.42f, y, w * 0.58f - 16f, h - 7f),
                  (yen > 0) ? "+" + GameMode.Yen(yen)
                            : GameMode.T("ボーナスなし", "No bonus"), yenStyle);

        GUI.color = prev;
    }


    // =====================================================
    //  判定結果を見せる
    //
    //  認識中はネタの名前を隠しているので、
    //  「合っていたか」を知る場面がここしかありません。
    //  少し止めて、必ず目に入るようにします。
    // =====================================================
    private IEnumerator ShowJudgeResult(int point)
    {
        if (judgeDisplaySeconds <= 0f) yield break;

        judgePoint = point;
        judgeShowing = true;

        yield return new WaitForSecondsRealtime(judgeDisplaySeconds);

        judgeShowing = false;
    }

    /// <summary>
    /// 判定結果の札。画面の中央に出します。
    ///
    /// もともと WaitScene が受け持っていた「へいお待ち」を、
    /// この札に統合しました。シーンを切り替えないぶんテンポが良く、
    /// 正解数と同時に見せられます。
    /// </summary>
    private void DrawJudgeResult()
    {
        bool perfect = (orderCount > 0 && judgePoint >= orderCount);

        bool hasArt = (judgeArt != null && judgeArt.texture != null);
        float artH = hasArt ? judgeArtHeight : 0f;

        float w = Mathf.Min(520f, UiScale.W - 40f);
        float h = 210f + artH;
        float x = (UiScale.W - w) / 2f;
        float y = (UiScale.H - h) / 2f;

        Color prev = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float cursor = y + 12f;

        // --- へいお待ちの絵（あれば）---
        if (hasArt)
        {
            Rect tr = judgeArt.textureRect;
            var uv = new Rect(tr.x / judgeArt.texture.width,
                              tr.y / judgeArt.texture.height,
                              tr.width / judgeArt.texture.width,
                              tr.height / judgeArt.texture.height);

            float aspect = (tr.height > 0f) ? tr.width / tr.height : 1f;
            float artW = Mathf.Min(w - 48f, artH * aspect);
            float artX = x + (w - artW) / 2f;

            // 黒い筆文字は暗い背景では見えないので、明るい札を敷く
            if (judgeArtPlate)
            {
                Color keep = GUI.color;
                GUI.color = judgeArtPlateColor;
                GUI.DrawTexture(
                    new Rect(artX - 14f, cursor - 8f, artW + 28f, artH + 16f),
                    Texture2D.whiteTexture);
                GUI.color = keep;
            }

            GUI.DrawTextureWithTexCoords(
                new Rect(artX, cursor, artW, artH),
                judgeArt.texture, uv);

            cursor += artH + (judgeArtPlate ? 18f : 6f);
        }
        else
        {
            var head = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 26,
            };
            head.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(x, cursor, w, 34f),
                      GameMode.T("へい、お待ち！", "Here you are!"), head);
            cursor += 38f;
        }

        // --- 正解数 ---
        var big = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 56,
            fontStyle = FontStyle.Bold,
        };
        big.normal.textColor = perfect
            ? new Color(1f, 0.85f, 0.40f)
            : Color.white;
        GUI.Label(new Rect(x, cursor, w, 70f),
                  GameMode.T($"{judgePoint} / {orderCount} 貫",
                             $"{judgePoint} / {orderCount} pcs"), big);
        cursor += 74f;

        // --- 内訳 ---
        var money = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
        };
        money.normal.textColor = new Color(1f, 0.93f, 0.70f);

        var sb = new System.Text.StringBuilder();
        sb.Append(GameMode.T("売上 +", "Sales +") + GameMode.Yen(judgePoint * ResultData.PricePerPiece));
        if (judgeSpeedYen > 0)   sb.Append(GameMode.T("　　速さ +", "   Speed +") + GameMode.Yen(judgeSpeedYen));
        if (judgePerfectYen > 0) sb.Append(GameMode.T("　　全問 +", "   Perfect +") + GameMode.Yen(judgePerfectYen));

        GUI.Label(new Rect(x, cursor, w, 30f), sb.ToString(), money);

        var total = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 17,
        };
        total.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(x, y + h - 40f, w, 26f),
                  GameMode.T("ここまでの売上　", "Total so far  ") + GameMode.Yen(ResultData.finalScore), total);

        GUI.color = prev;
    }

}
