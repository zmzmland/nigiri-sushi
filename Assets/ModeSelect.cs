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

    [Header("見習いモードの注文の見せ方")]
    [Tooltip("イラスト＝寿司の絵。カタカナ＝「マグロ」の文字。" +
             "文字画像を用意したなら カタカナ にできます")]
    public OrderStyle apprenticeStyle = OrderStyle.イラスト;

    [Header("文言")]
    public string headingText = "腕前をえらぶ";

    public string apprenticeMain = "みならい";
    public string apprenticeSub  = "え で ちゅうもん　ずっと でてる";

    public string chefMain = "板前";
    public string chefSub  = "漢字で注文　おぼえて にぎる";

    public string englishMain = "ENGLISH";
    public string englishSub  = "Orders in English, stays on screen";

    public string backText = "もどる";

    private GameObject panel;

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
        if (kb != null && kb.escapeKey.wasPressedThisFrame) Hide();
    }

    // =====================================================
    //  開く / 閉じる
    // =====================================================
    public void Show()
    {
        if (panel == null) Build();
        if (panel != null) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Choose(GameModeId id)
    {
        GameMode.ApprenticeStyle = apprenticeStyle;
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
        BuildButton(panel.transform, apprenticeMain, apprenticeSub,
                    () => Choose(GameModeId.見習い));

        BuildButton(panel.transform, chefMain, chefSub,
                    () => Choose(GameModeId.板前));

        BuildButton(panel.transform, englishMain, englishSub,
                    () => Choose(GameModeId.English));

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

    private void BuildButton(Transform parent, string main, string sub,
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

        if (string.IsNullOrEmpty(sub)) return;

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
    }
}
