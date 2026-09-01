using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// タイトルで「始め」を押したときに出るモード選択画面。
///
/// ★ シーンに置くのは、このスクリプトを付けた空の GameObject 1つだけです。
///   ボタンやパネルは実行時に作ります。
///   （GameAudio と同じ GameObject に付けても構いません）
///
/// 使い方:
///   1. SampleScene に空の GameObject を作り、これを付ける
///   2. Inspector の Font に NotoSansJP SDF をドラッグ
///   3. 「始め」ボタンの OnClick が StartButton.StartGame() を
///      呼んでいれば、そのままここが開きます
///
/// 閉じるのは ESC か「もどる」です。
/// </summary>
public class ModeSelect : MonoBehaviour
{
    public static ModeSelect Instance { get; private set; }

    [Header("フォント")]
    [Tooltip("日本語を出すので NotoSansJP SDF をドラッグしてください")]
    public TMP_FontAsset font;

    [Header("見た目")]
    [Tooltip("ボタンの画像。空なら下の色で単色のボタンになります")]
    public Sprite buttonSprite;

    public Color panelColor  = new Color(0f, 0f, 0f, 0.72f);
    public Color buttonColor = new Color(0.42f, 0.27f, 0.13f, 0.95f);
    public Color textColor   = Color.white;

    [Tooltip("ボタン1つの大きさ")]
    public Vector2 buttonSize = new Vector2(520f, 96f);

    [Tooltip("ボタンどうしの間隔")]
    public float spacing = 18f;

    [Header("進む先")]
    public string nextSceneName = "Game Scene";

    [Header("日本語モードの注文の見せ方")]
    [Tooltip("カタカナ＝「マグロ」。漢字＝「鮪」。イラスト＝寿司の絵。" +
             "小さい子も来るならカタカナが読みやすいです")]
    public OrderStyle japaneseStyle = OrderStyle.カタカナ;

    [Header("寿司で選ぶ")]
    [Tooltip("まな板に寿司を置いてモードを選べるようにする")]
    public bool enableSushiSelect = true;

    [Tooltip("これを置くと日本語モード")]
    public string japaneseClass = "salmon";

    [Tooltip("これを置くと英語モード")]
    public string englishClass = "ebi";

    [Tooltip("この秒数、見え続けたら決定する")]
    public float sushiHoldSeconds = 1.2f;

    [Tooltip("detect.txt を読む間隔（秒）")]
    public float sushiPollInterval = 0.15f;

    [Tooltip("寿司が見えているときにボタンを光らせる色")]
    public Color highlightColor = new Color(0.95f, 0.75f, 0.30f, 1f);

    [Header("文言")]
    public string headingText = "ことばをえらぶ";

    public string japaneseMain = "日本語";
    public string japaneseSub  = "サーモンを置く　または クリック";

    public string englishMain = "ENGLISH";
    public string englishSub  = "Place SHRIMP, or click";

    public string backText = "もどる";

    private GameObject panel;

    // 寿司で選ぶための状態
    private Image japaneseButton;
    private Image englishButton;
    private Color buttonBaseColor;
    private string seeing = null;     // いま見えているモード用の寿司
    private float seeingSince = -1f;
    private float lastPoll = -999f;

    /// <summary>いま選択画面が開いているか。</summary>
    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) { Hide(); return; }

        if (enableSushiSelect) WatchSushi();
    }

    // =====================================================
    //  寿司でモードを選ぶ
    // =====================================================
    private void WatchSushi()
    {
        if (Time.time - lastPoll >= sushiPollInterval)
        {
            lastPoll = Time.time;

            // Python が動いていなければ何もしない（クリックで選べます）
            bool alive = GamePaths.SecondsSinceWrite(GamePaths.HeartbeatPath) <= 8.0;
            string found = alive ? FindMoveSushi() : null;

            if (found != seeing)
            {
                seeing = found;
                seeingSince = found == null ? -1f : Time.time;
            }
        }

        float progress = 0f;
        if (seeing != null && seeingSince >= 0f)
        {
            progress = sushiHoldSeconds <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - seeingSince) / sushiHoldSeconds);
        }

        Highlight(japaneseButton, seeing == japaneseClass ? progress : 0f);
        Highlight(englishButton,  seeing == englishClass  ? progress : 0f);

        if (progress >= 1f)
        {
            if (seeing == japaneseClass) Choose(GameModeId.日本語);
            else if (seeing == englishClass) Choose(GameModeId.英語);
        }
    }

    /// <summary>detect.txt から、モード選択に使うネタを探す。</summary>
    private string FindMoveSushi()
    {
        string text = GamePaths.SafeRead(GamePaths.DetectPath);
        if (string.IsNullOrEmpty(text)) return null;

        bool ja = false, en = false;
        foreach (string line in text.Split('\n'))
        {
            string n = line.Trim();
            if (n == japaneseClass) ja = true;
            if (n == englishClass)  en = true;
        }

        // 両方置かれていたら決めない（迷っている状態とみなす）
        if (ja && en) return null;
        if (ja) return japaneseClass;
        if (en) return englishClass;
        return null;
    }

    private void Highlight(Image img, float progress)
    {
        if (img == null) return;
        img.color = Color.Lerp(buttonBaseColor, highlightColor, progress);
    }

    // =====================================================
    //  開く / 閉じる
    // =====================================================
    public void Show()
    {
        if (panel == null) Build();
        if (panel != null) panel.SetActive(true);

        // 前回の残りで即決定されないよう、状態を戻す
        seeing = null;
        seeingSince = -1f;
        lastPoll = -999f;
        Highlight(japaneseButton, 0f);
        Highlight(englishButton, 0f);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Choose(GameModeId id)
    {
        GameMode.JapaneseStyle = japaneseStyle;
        GameMode.Current = id;
        GameAudio.Click();
        Debug.Log($"モード: {GameMode.DisplayName}");
        SceneManager.LoadScene(nextSceneName);
    }

    // =====================================================
    //  組み立て
    // =====================================================
    private void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindAnyCanvas();

        if (canvas == null)
        {
            Debug.LogError("[ModeSelect] Canvas が見つかりません。" +
                           "タイトルシーンの Canvas の下に置くか、Canvas を用意してください。");
            return;
        }

        // --- 全画面の暗幕 ---
        panel = new GameObject("ModeSelectPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup));

        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();

        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        panel.GetComponent<Image>().color = panelColor;

        var vl = panel.GetComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.spacing = spacing;
        vl.childControlWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandWidth = false;
        vl.childForceExpandHeight = false;

        // --- 見出し ---
        BuildHeading(panel.transform);

        // --- モードのボタン ---
        japaneseButton = BuildButton(panel.transform, japaneseMain, japaneseSub,
                                     () => Choose(GameModeId.日本語));

        englishButton = BuildButton(panel.transform, englishMain, englishSub,
                                    () => Choose(GameModeId.英語));

        buttonBaseColor = japaneseButton != null ? japaneseButton.color : buttonColor;

        // --- もどる ---
        BuildButton(panel.transform, backText, "",
                    () => { GameAudio.Click(); Hide(); },
                    heightScale: 0.6f);
    }

    /// <summary>
    /// Canvas の子に置かれていなかったときの保険。
    /// ふつうは GetComponentInParent で見つかるので、ここは通りません。
    /// </summary>
    private Canvas FindAnyCanvas()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindAnyObjectByType<Canvas>();
#else
        return Object.FindObjectOfType<Canvas>();
#endif
    }

    private void BuildHeading(Transform parent)
    {
        var go = new GameObject("Heading", typeof(RectTransform), typeof(TextMeshProUGUI),
                                typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = buttonSize.x;
        le.preferredHeight = 70f;

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = headingText;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 20f;
        text.fontSizeMax = 52f;
    }

    private Image BuildButton(Transform parent, string main, string sub,
                              UnityEngine.Events.UnityAction onClick,
                              float heightScale = 1f)
    {
        var go = new GameObject($"Button_{main}",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(LayoutElement));

        go.transform.SetParent(parent, false);

        var le = go.GetComponent<LayoutElement>();
        le.preferredWidth  = buttonSize.x;
        le.preferredHeight = buttonSize.y * heightScale;

        var img = go.GetComponent<Image>();
        if (buttonSprite != null)
        {
            img.sprite = buttonSprite;
            img.color = Color.white;
        }
        else
        {
            img.color = buttonColor;
        }

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        // --- 主ラベル ---
        var mainGo = new GameObject("Main", typeof(RectTransform), typeof(TextMeshProUGUI));
        mainGo.transform.SetParent(go.transform, false);

        var mrt = mainGo.GetComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0f, string.IsNullOrEmpty(sub) ? 0f : 0.40f);
        mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(12f, 0f);
        mrt.offsetMax = new Vector2(-12f, -4f);

        var mainText = mainGo.GetComponent<TextMeshProUGUI>();
        if (font != null) mainText.font = font;
        mainText.text = main;
        mainText.color = textColor;
        mainText.alignment = TextAlignmentOptions.Center;
        mainText.enableAutoSizing = true;
        mainText.fontSizeMin = 18f;
        mainText.fontSizeMax = 44f;
        mainText.raycastTarget = false;

        if (string.IsNullOrEmpty(sub)) return img;

        // --- 説明 ---
        var subGo = new GameObject("Sub", typeof(RectTransform), typeof(TextMeshProUGUI));
        subGo.transform.SetParent(go.transform, false);

        var srt = subGo.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = new Vector2(1f, 0.40f);
        srt.offsetMin = new Vector2(12f, 6f);
        srt.offsetMax = new Vector2(-12f, 0f);

        var subText = subGo.GetComponent<TextMeshProUGUI>();
        if (font != null) subText.font = font;
        subText.text = sub;
        subText.color = new Color(textColor.r, textColor.g, textColor.b, 0.8f);
        subText.alignment = TextAlignmentOptions.Center;
        subText.enableAutoSizing = true;
        subText.fontSizeMin = 10f;
        subText.fontSizeMax = 20f;
        subText.raycastTarget = false;

        return img;
    }
}
