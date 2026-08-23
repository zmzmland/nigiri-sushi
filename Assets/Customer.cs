using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Customer : MonoBehaviour
{
    [Header("Move Settings")]
    public float targetX = 0f;
    public float moveTime = 1f;

    [Header("Sprite Settings")]
    public Sprite customerSprite;

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
    [Tooltip("外部プロセスが result.txt を書き込むまでのポーリング間隔（秒）")]
    public float resultCheckInterval = 0.2f;

    [Header("Score Bonus Table")]
    [Tooltip("経過時間がこの値以下ならボーナス倍率を適用（昇順で並べること）")]
    public float[] timeThresholds = { 30f, 40f, 50f, 60f, 70f };
    public float[] timeMultipliers = { 1.20f, 1.15f, 1.10f, 1.05f, 1.00f };

    private RectTransform rectTransform;
    private Image image;
    private CanvasGroup bubbleCanvasGroup;
    private readonly List<Sprite> orderHistory = new List<Sprite>();
    private float timer = 0f;
    private bool timerRunning = false;
    private string resultPath;
    private string orderPath;
    private Coroutine resultWatchCoroutine;

    void Start()
    {
        // 共有フォルダのパスは GamePaths に一元化されている（~/TGS_ImageSearch）。
        // Python 側（ImageSearch/main.py）も同じ場所を見ているので、
        // 場所を変えるときは GamePaths.cs と main.py の BASE_DIR を両方直すこと。
        resultPath = GamePaths.ResultPath;
        orderPath  = GamePaths.OrderPath;

        ResultData.score = 0;
        ResultData.correctCount = 0;
        ResultData.totalOrders = 0;
        ResultData.scene1Time = 0f;

        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        if (speechBubble != null)
        {
            bubbleCanvasGroup = speechBubble.GetComponent<CanvasGroup>();
            speechBubble.SetActive(false);

            if (bubbleCanvasGroup != null)
            {
                bubbleCanvasGroup.alpha = 1f;
            }
            else
            {
                Debug.LogError("SpeechBubbleにCanvasGroupが付いていません");
            }
        }
        else
        {
            Debug.LogError("SpeechBubbleが設定されていません");
        }

        SafeWriteAllText(resultPath, "");
        SafeWriteAllText(orderPath, "");                   
        GamePaths.SafeDelete(GamePaths.TriggerPath);      

        StartCoroutine(CustomerFlow());
        resultWatchCoroutine = StartCoroutine(WatchResultFile());
    }

    // =========================
    // メインの流れ
    // =========================
    IEnumerator CustomerFlow()
    {
        yield return new WaitForSeconds(1f);

        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 targetPos = new Vector2(targetX, startPos.y);

        float elapsed = 0f;
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            rectTransform.anchoredPosition =
                Vector2.Lerp(startPos, targetPos, elapsed / moveTime);
            yield return null;
        }

        rectTransform.anchoredPosition = targetPos;

        yield return new WaitForSeconds(0.5f);
        if (customerSprite != null)
        {
            image.sprite = customerSprite;
        }

        timer = 0f;
        timerRunning = true;

        yield return StartCoroutine(MultipleOrders());
        yield return StartCoroutine(FadeOutBubble());
    }

    // =========================
    // 注文（同じものが3連続しない）
    // =========================
    IEnumerator MultipleOrders()
    {
        if (orderSprites == null || orderSprites.Length == 0)
        {
            Debug.LogError("orderSprites が設定されていません");
            yield break;
        }

        ResultData.totalOrders = orderCount;

        const int maxAttemptsPerOrder = 100;

        for (int i = 0; i < orderCount; i++)
        {
            Sprite order = orderSprites[Random.Range(0, orderSprites.Length)];
            int attempts = 0;

            while (attempts < maxAttemptsPerOrder)
            {
                int index = Random.Range(0, orderSprites.Length);
                Sprite candidate = orderSprites[index];

                int sameCount = 0;
                foreach (Sprite s in orderHistory)
                {
                    if (s == candidate)
                    {
                        sameCount++;
                    }
                }

                order = candidate;
                attempts++;

                if (sameCount < 2)
                {
                    break;
                }
            }

            orderHistory.Add(order);

            speechBubble.SetActive(true);
            bubbleCanvasGroup.alpha = 1f;
            orderImage.sprite = order;

            yield return new WaitForSeconds(showTime);

            speechBubble.SetActive(false);

            yield return new WaitForSeconds(hideTime);
        }

        SaveOrderFile();
    }

    // =========================
    // result.txt の監視（Updateではなくコルーチンでポーリング）
    // =========================
    IEnumerator WatchResultFile()
    {
        var wait = new WaitForSeconds(resultCheckInterval);

        while (true)
        {
            if (timerRunning)
            {
                timer += resultCheckInterval;
            }

            string result = SafeReadAllText(resultPath);

            if (!string.IsNullOrEmpty(result))
            {
                if (int.TryParse(result.Trim(), out int point))
                {
                    ResultData.correctCount = point;
                    ResultData.score = point * 10;
                }
                else
                {
                    Debug.LogWarning($"result.txt の内容が数値ではありません: \"{result}\"");
                }

                timerRunning = false;
                ResultData.scene1Time = timer;
                Debug.Log("scene1Time = " + ResultData.scene1Time);

                CalculateFinalScore();

                SafeWriteAllText(resultPath, "");
                SceneManager.LoadScene("WaitScene");
                yield break;
            }

            yield return wait;
        }
    }

    // =========================
    // フェード
    // =========================
    IEnumerator FadeOutBubble()
    {
        yield return new WaitForSeconds(fadeDelay);

        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            bubbleCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
            yield return null;
        }

        bubbleCanvasGroup.alpha = 0f;
        speechBubble.SetActive(false);
    }

    // =========================
    // 最終スコア計算（テーブル駆動）
    // =========================
    void CalculateFinalScore()
    {
        float multiplier = 1.0f;

        for (int i = 0; i < timeThresholds.Length; i++)
        {
            if (timer <= timeThresholds[i])
            {
                multiplier = timeMultipliers[i];
                break;
            }
        }

        ResultData.timeBonus = multiplier;
        ResultData.finalScore = Mathf.RoundToInt(ResultData.score * multiplier);
    }

    // =========================
    // 注文履歴の保存
    // =========================
    void SaveOrderFile()
    {
        var sb = new System.Text.StringBuilder();
        foreach (Sprite s in orderHistory)
        {
            sb.AppendLine(s.name);
        }

        SafeWriteAllText(orderPath, sb.ToString());
    }

    // =========================
    // 例外に強いファイルIOヘルパー
    // =========================
    string SafeReadAllText(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }
            return File.ReadAllText(path).Trim();
        }
        catch (IOException)
        {
            // 外部プロセスが書き込み中でロックされている場合など。
            // 次のポーリングで再試行するので無視してよい。
            return null;
        }
    }

    void SafeWriteAllText(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
        }
        catch (IOException e)
        {
            Debug.LogError($"{path} への書き込みに失敗しました: {e.Message}");
        }
    }

    void OnDestroy()
    {
        if (resultWatchCoroutine != null)
        {
            StopCoroutine(resultWatchCoroutine);
        }
    }
}