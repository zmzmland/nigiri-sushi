using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Customer2 : MonoBehaviour
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

    private RectTransform rectTransform;
    private Image image;
    private CanvasGroup bubbleCanvasGroup;

    private List<Sprite> orderHistory = new List<Sprite>();

    private float timer = 0f;
    private bool timerRunning = false;
    private string resultPath;

    void Start()
{
    Debug.Log("Customer2 Start");

    resultPath = GamePaths.ResultPath;

    if (File.Exists(resultPath))
    {
        File.WriteAllText(resultPath, "");
        GamePaths.SafeWrite(GamePaths.OrderPath, "");
        GamePaths.SafeDelete(GamePaths.TriggerPath);
    }

    rectTransform = GetComponent<RectTransform>();
    image = GetComponent<Image>();

    if (speechBubble != null)
    {
        bubbleCanvasGroup =
            speechBubble.GetComponent<CanvasGroup>();

        speechBubble.SetActive(false);

        if (bubbleCanvasGroup != null)
        {
            bubbleCanvasGroup.alpha = 1f;
        }
    }

    StartCoroutine(CustomerFlow());   // ← これを戻す
}


    IEnumerator CustomerFlow()
    {
        Debug.Log("① CustomerFlow開始");
        yield return new WaitForSeconds(1f);

        Vector2 startPos = rectTransform.anchoredPosition;
        Vector2 targetPos = new Vector2(targetX, startPos.y);

        float elapsed = 0f;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;

            rectTransform.anchoredPosition =
                Vector2.Lerp(
                    startPos,
                    targetPos,
                    elapsed / moveTime);

            yield return null;
        }

        rectTransform.anchoredPosition = targetPos;

        Debug.Log("③ 移動完了");

        yield return new WaitForSeconds(0.5f);

        Debug.Log("④ Sprite変更前");

        if (customerSprite != null)
        {
            image.sprite = customerSprite;
        }

       // Game Scene 2 のタイマー開始
        timer = 0f;
        timerRunning = true;

        Debug.Log("⑤ MultipleOrders呼び出し前");

        yield return StartCoroutine(MultipleOrders());

        yield return StartCoroutine(FadeOutBubble());
    }

    IEnumerator MultipleOrders()
    {
        Debug.Log("MultipleOrders Start");
        ResultData.totalOrders += orderCount;

        for (int i = 0; i < orderCount; i++)
        {
            Sprite order;

            do
{
    int index =
        Random.Range(
            0,
            orderSprites.Length);

    order = orderSprites[index];

    int sameCount = 0;

    foreach (Sprite s in orderHistory)
    {
        if (s == order)
        {
            sameCount++;
        }
    }

    if (sameCount < 2)
    {
        break;
    }

}
while (true);


            orderHistory.Add(order);

            if (speechBubble != null)
    {
    speechBubble.SetActive(true);
    }

    if (bubbleCanvasGroup != null)
    {
    bubbleCanvasGroup.alpha = 1f;
    }

    if (orderImage == null)
    {
    Debug.LogError("OrderImage が設定されていません");
    yield break;
    }

    Debug.Log("OrderImage : " + orderImage.name);

    orderImage.sprite = order;

    // 注文を表示
    yield return new WaitForSeconds(showTime);

    // 吹き出しを消す
    if (speechBubble != null)
    {
    speechBubble.SetActive(false);
}

// 次の注文まで待つ
yield return new WaitForSeconds(hideTime);
Debug.Log(orderHistory.Count);
}

SaveOrderFile();
    }

    void Update()
    {
        // タイマー計測
        if (timerRunning)
        {
            timer += Time.deltaTime;
        }

        // Space = リザルトへ
        if (File.Exists(resultPath))
{
    string result =
        File.ReadAllText(resultPath).Trim();

    if (result != "")
{
    int point;

    if (int.TryParse(result, out point))
{
    ResultData.correctCount += point;
    ResultData.score += point * 10;
}

    timerRunning = false;



    File.WriteAllText(resultPath, "");

    ResultData.scene2Time = timer;

    Debug.Log("2面 +" + point + "点  correctCount=" + ResultData.correctCount + " score=" + ResultData.score + " scene2Time=" + timer);

    CalculateFinalScore();


    SceneManager.LoadScene("ResultScene");
}

}

    }

    IEnumerator FadeOutBubble()
    {
        yield return new WaitForSeconds(fadeDelay);

        if (bubbleCanvasGroup == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;

            bubbleCanvasGroup.alpha =
                Mathf.Lerp(
                    1f,
                    0f,
                    elapsed / fadeTime);

            yield return null;
        }

        bubbleCanvasGroup.alpha = 0f;

        if (speechBubble != null)
        {
            speechBubble.SetActive(false);
        }
    }

    void CalculateFinalScore()
{
    float totalTime =
        ResultData.scene1Time +
        ResultData.scene2Time;

    float multiplier = 1.0f;

    if (totalTime <= 40f)
    {
        multiplier = 1.20f;
    }
    else if (totalTime <= 50f)
    {
        multiplier = 1.15f;
    }
    else if (totalTime <= 60f)
    {
        multiplier = 1.10f;
    }
    else if (totalTime <= 70f)
    {
        multiplier = 1.05f;
    }
    else if (totalTime <= 80f)
    {
        multiplier = 1.00f;
    }

    ResultData.timeBonus = multiplier;

    ResultData.finalScore =
    Mathf.RoundToInt(ResultData.score * multiplier);


    Debug.Log("totalTime = " + totalTime);
    Debug.Log("multiplier = " + multiplier);
    Debug.Log("finalScore = " + ResultData.finalScore);
}
void SaveOrderFile()
{
    string path = GamePaths.OrderPath;

    string content = "";

    foreach (Sprite s in orderHistory)
    {
        content += s.name + "\n";
    }

    File.WriteAllText(path, content);

    Debug.Log("order.txt 保存完了");
}

}
