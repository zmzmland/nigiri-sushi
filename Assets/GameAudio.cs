using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 売上に応じて変わるリザルトの「段」。
/// 鳴らす音と、画面に出す称号の両方をここで決めます。
/// Min Score 以上なら、その段になります。
/// 上から順に判定するので、金額の大きいものを上に並べてください。
/// </summary>
[System.Serializable]
public class ResultTier
{
    [Tooltip("段の名前。表示には使いません。分かりやすさのためだけ")]
    public string name = "";

    [Tooltip("この金額以上ならこの段")]
    public int minScore = 0;

    [Tooltip("鳴らす音。空なら Resources/Audio から自動で読み込みます")]
    public AudioClip clip;

    [Tooltip("clip が空のときに Resources/Audio から探す名前")]
    public string clipName = "";

    // ------ ここから称号 ------

    [Tooltip("リザルトの最後に出す称号。空なら何も出しません")]
    [TextArea(1, 3)]
    public string title = "";

    [Tooltip("称号の文字色")]
    public Color titleColor = Color.white;
}

/// <summary>シーン名と、そこで流す BGM の対応。</summary>
[System.Serializable]
public class SceneBgm
{
    public string sceneName;
    public AudioClip clip;
}

/// <summary>
/// 効果音と BGM をまとめて面倒を見る。
///
/// 【置き方】
///   タイトルシーン（SampleScene）に空の GameObject を1つ作り、
///   このスクリプトを付けるだけです。DontDestroyOnLoad で
///   シーンをまたいで生き残るので、他のシーンには何も要りません。
///
/// 【音の入れ方】
///   Assets/Resources/Audio/ に、この名前で置けば自動で読み込みます。
///
///     se_click / se_order / se_countdown / se_judge
///     se_correct / se_wrong / se_customer / se_rankin
///     bgm_title / bgm_game / bgm_result
///
///   Inspector に直接ドラッグして入れることもできます。
///   入れたものが優先されます。
///
/// 【無くても動きます】
///   音が1つも無くても、このスクリプトが無くても、ゲームは普通に動きます。
///   呼び出し側は全部 null チェック済みです。
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio I { get; private set; }

    [Header("音量")]
    [Range(0f, 1f)] public float bgmVolume = 0.35f;
    [Range(0f, 1f)] public float seVolume  = 0.8f;

    [Tooltip("BGM を切り替えるときのフェード秒数")]
    public float bgmFadeTime = 0.6f;


    // 音は「チェック」と「音源」が対になっています。
    // チェックを外すと、その音だけ鳴らなくなります（音源は消えません）。
    // 音源が空なら Resources/Audio から名前で自動的に読み込みます。

    [Header("ボタン")]
    [Tooltip("「始め」「営業再開」を押したとき")]
    public bool useClick = true;
    public AudioClip seClick;

    [Header("客が来たとき")]
    public bool useCustomer = true;
    public AudioClip seCustomer;

    [Header("注文が出るとき")]
    [Tooltip("吹き出しが出るたび／リザルトの明細1行ごと")]
    public bool useOrder = true;
    public AudioClip seOrder;

    [Header("自動判定のカウントダウン")]
    [Tooltip("3・2・1 の数字が変わるたび")]
    public bool useCountdown = true;
    public AudioClip seCountdown;

    [Header("判定の瞬間")]
    [Tooltip("拍子木")]
    public bool useJudge = true;
    public AudioClip seJudge;

    [Header("判定のあとの正解音・不正解音")]
    [Tooltip("既定はオフ。入れると判定の0.35秒後に鳴ります")]
    public bool playResultChime = false;
    public AudioClip seCorrect;
    public AudioClip seWrong;

    [Header("リザルトの売上に応じた音")]
    [Tooltip("下の Result Tiers で段を決めます")]
    public bool useResult = true;

    [Header("番付入り")]
    [Tooltip("いまは使っていません。売上の音と重なるため")]
    public bool useRankIn = true;
    public AudioClip seRankIn;

    [Header("BGM")]
    [Tooltip("外すと BGM が一切鳴りません")]
    public bool useBgm = true;
    public AudioClip bgmTitle;
    public AudioClip bgmGame;
    public AudioClip bgmResult;

    [Tooltip("シーンごとの BGM。空のままなら既定の割り当てを使います")]
    public List<SceneBgm> bgmTable = new List<SceneBgm>();

    [Header("リザルトの段（売上で音と称号が変わる）")]
    [Tooltip("上から順に「この金額以上か」を見ます。空のままなら既定の3段階を使います。\n" +
             "Title に文字を入れると、リザルトの最後にその称号が出ます")]
    public List<ResultTier> resultTiers = new List<ResultTier>();

    private AudioSource bgmSource;
    private AudioSource seSource;
    private AudioClip currentBgm;
    private Coroutine fading;

    // =====================================================
    //  初期化
    // =====================================================
    void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
        DontDestroyOnLoad(gameObject);

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = bgmVolume;

        seSource = gameObject.AddComponent<AudioSource>();
        seSource.loop = false;
        seSource.playOnAwake = false;

        LoadMissingClips();
        BuildDefaultTable();
        BuildDefaultResultTiers();

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyBgmFor(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>Inspector が空の項目だけ Resources から読む。</summary>
    private void LoadMissingClips()
    {
        seClick     = seClick     ? seClick     : Load("se_click");
        seOrder     = seOrder     ? seOrder     : Load("se_order");
        seCountdown = seCountdown ? seCountdown : Load("se_countdown");
        seJudge     = seJudge     ? seJudge     : Load("se_judge");
        seCorrect   = seCorrect   ? seCorrect   : Load("se_correct");
        seWrong     = seWrong     ? seWrong     : Load("se_wrong");
        seCustomer  = seCustomer  ? seCustomer  : Load("se_customer");
        seRankIn    = seRankIn    ? seRankIn    : Load("se_rankin");

        bgmTitle  = bgmTitle  ? bgmTitle  : Load("bgm_title");
        bgmGame   = bgmGame   ? bgmGame   : Load("bgm_game");
        bgmResult = bgmResult ? bgmResult : Load("bgm_result");
    }

    private static AudioClip Load(string name)
    {
        return Resources.Load<AudioClip>("Audio/" + name);
    }

    private void BuildDefaultTable()
    {
        if (bgmTable != null && bgmTable.Count > 0) return;

        bgmTable = new List<SceneBgm>
        {
            new SceneBgm { sceneName = "SampleScene",  clip = bgmTitle  },
            new SceneBgm { sceneName = "Game Scene",   clip = bgmGame   },
            new SceneBgm { sceneName = "WaitScene",    clip = bgmGame   },
            new SceneBgm { sceneName = "Game Scene 2", clip = bgmGame   },
            new SceneBgm { sceneName = "Game Scene 3", clip = bgmGame   },
            new SceneBgm { sceneName = "ResultScene",  clip = bgmResult },
        };
    }

    /// <summary>
    /// リザルトの段が未設定なら、既定の3段階を作る。
    ///
    /// 3面で 3+4+5 = 12貫、500円 ずつで 6,000円。
    /// そこにスピードボーナス最大 3,000円 を足して満点は 9,000円です。
    /// 注文数を変えたら、この金額も Inspector で調整してください。
    /// </summary>
    private void BuildDefaultResultTiers()
    {
        if (resultTiers == null || resultTiers.Count == 0)
            resultTiers = DefaultTiers();

        NormalizeTiers(resultTiers);
    }

    /// <summary>
    /// 空欄を既定値で補う。
    ///
    /// 称号の項目は後から足したので、それ以前に保存されたシーンでは
    /// Title が空のまま入っています。そのままだと何も出ないので、
    /// 金額が近い既定の段から文言と色を借りてきます。
    ///
    /// Inspector に文字を書けば、当然そちらが優先されます。
    /// </summary>
    private static void NormalizeTiers(List<ResultTier> list)
    {
        if (list == null) return;

        List<ResultTier> fallback = DefaultTiers();

        for (int i = 0; i < list.Count; i++)
        {
            ResultTier tr = list[i];
            if (tr == null) continue;

            if (tr.clip == null && !string.IsNullOrEmpty(tr.clipName))
                tr.clip = Load(tr.clipName);

            if (!string.IsNullOrEmpty(tr.title)) continue;

            // 称号が空 → 既定の段から借りる。
            // 同じ名前があればそれを、無ければ同じ並び順のものを使います。
            // （金額で探すと、しきい値を変えたときにずれるため）
            ResultTier src = FindByName(fallback, tr.name);
            if (src == null && fallback.Count > 0)
                src = fallback[Mathf.Min(i, fallback.Count - 1)];

            if (src == null) continue;

            tr.title = src.title;

            // 色も一緒に借りる。
            // 白のままなら「まだ触っていない」とみなします
            bool untouched =
                tr.titleColor.a <= 0.01f ||
                (Mathf.Approximately(tr.titleColor.r, 1f) &&
                 Mathf.Approximately(tr.titleColor.g, 1f) &&
                 Mathf.Approximately(tr.titleColor.b, 1f));

            if (untouched) tr.titleColor = src.titleColor;

            if (tr.clip == null && !string.IsNullOrEmpty(src.clipName))
                tr.clip = Load(src.clipName);
        }
    }

    private static ResultTier FindByName(List<ResultTier> list, string name)
    {
        if (list == null || string.IsNullOrEmpty(name)) return null;

        foreach (ResultTier tr in list)
        {
            if (tr != null && tr.name == name) return tr;
        }
        return null;
    }

    /// <summary>
    /// 既定の3段階。Inspector が空のときと、
    /// GameAudio がシーンに無いとき（Result だけを単体で再生したとき）に使います。
    ///
    /// 称号の文言・金額はここではなく Inspector で変えてください。
    /// ここを書き換えても、すでに設定済みのシーンには反映されません。
    /// </summary>
    public static List<ResultTier> DefaultTiers()
    {
        // 色について:
        //   リザルトの背景はクリーム色と薄い青波なので、
        //   明るい色は沈んで読めません。すべて濃い色にしてあります。
        //   赤 → 橙 → 緑 → 藍 → 墨 と、暖色から寒色へ下がる並びです。
        return new List<ResultTier>
        {
            new ResultTier {
                name = "銀座", minScore = 13000, clipName = "se_result_high",
                title = "銀座の名店からスカウトが来た！",
                titleColor = new Color(0.776f, 0.157f, 0.157f),  // 朱赤 #C62828
            },
            new ResultTier {
                name = "行列", minScore = 10500, clipName = "se_result_high",
                title = "行列ができる名店になった！",
                titleColor = new Color(0.796f, 0.396f, 0.078f),  // 柿色 #CB6514
            },
            new ResultTier {
                name = "評判", minScore = 8000, clipName = "se_result_mid",
                title = "近所で評判の寿司屋だ",
                titleColor = new Color(0.106f, 0.427f, 0.169f),  // 深緑 #1B6D2B
            },
            new ResultTier {
                name = "常連", minScore = 5500, clipName = "se_result_mid",
                title = "常連さんがついてきたね",
                titleColor = new Color(0.122f, 0.306f, 0.475f),  // 藍  #1F4E79
            },
            new ResultTier {
                name = "修業", minScore = 0, clipName = "se_result_low",
                title = "修業はこれからだ！",
                titleColor = new Color(0.216f, 0.255f, 0.286f),  // 墨  #374149
            },
        };
    }

    /// <summary>
    /// 売上がどの段に当たるかを返す。当たらなければ null。
    /// 音のオン・オフ（Use Result）とは無関係に働きます。
    /// </summary>
    public static ResultTier TierFor(int score)
    {
        List<ResultTier> list =
            (I != null && I.resultTiers != null && I.resultTiers.Count > 0)
                ? I.resultTiers
                : DefaultTiers();

        foreach (ResultTier tr in list)
        {
            if (tr == null) continue;
            if (score >= tr.minScore) return tr;
        }

        return null;
    }

    /// <summary>売上に対応する称号。無ければ空文字。</summary>
    public static string TitleFor(int score)
    {
        ResultTier tr = TierFor(score);
        return tr == null ? "" : tr.title;
    }

    // =====================================================
    //  BGM
    // =====================================================
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyBgmFor(scene.name);
    }

    private void ApplyBgmFor(string sceneName)
    {
        AudioClip clip = null;

        if (useBgm)
        {
            foreach (SceneBgm b in bgmTable)
            {
                if (b != null && b.sceneName == sceneName) { clip = b.clip; break; }
            }
        }

        // 同じ曲なら鳴らし直さない。
        // 1面→待機→2面→3面 でぶつ切りにならないようにするため。
        if (clip == currentBgm) return;

        currentBgm = clip;

        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(SwitchBgm(clip));
    }

    private IEnumerator SwitchBgm(AudioClip next)
    {
        float t = 0f;
        float from = bgmSource.volume;

        // フェードアウト
        if (bgmSource.isPlaying && bgmFadeTime > 0f)
        {
            while (t < bgmFadeTime)
            {
                t += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(from, 0f, t / bgmFadeTime);
                yield return null;
            }
        }

        bgmSource.Stop();

        if (next == null)
        {
            bgmSource.clip = null;
            bgmSource.volume = bgmVolume;
            yield break;
        }

        bgmSource.clip = next;
        bgmSource.volume = 0f;
        bgmSource.Play();

        // フェードイン
        t = 0f;
        while (t < bgmFadeTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t / bgmFadeTime);
            yield return null;
        }

        bgmSource.volume = bgmVolume;
    }

    // =====================================================
    //  効果音（呼び出し口）
    // =====================================================
    private void PlayOne(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || seSource == null) return;
        seSource.PlayOneShot(clip, seVolume * volumeScale);
    }

    private IEnumerator PlayLater(AudioClip clip, float delay, float volumeScale)
    {
        yield return new WaitForSecondsRealtime(delay);
        PlayOne(clip, volumeScale);
    }

    // --- static な入口。GameAudio が無くても安全に何も起きない ---

    public static void Click()        { if (I != null && I.useClick)     I.PlayOne(I.seClick); }
    public static void Order()        { if (I != null && I.useOrder)     I.PlayOne(I.seOrder); }
    public static void Countdown()    { if (I != null && I.useCountdown) I.PlayOne(I.seCountdown, 0.7f); }
    public static void Judge()        { if (I != null && I.useJudge)     I.PlayOne(I.seJudge); }
    public static void CustomerCome() { if (I != null && I.useCustomer)  I.PlayOne(I.seCustomer); }
    public static void RankIn()       { if (I != null && I.useRankIn)    I.PlayOne(I.seRankIn); }

    /// <summary>
    /// リザルトで、売上に応じた音を鳴らす。
    /// どの段に当たったかは Console にも出します（調整の目安に）。
    /// </summary>
    public static void Result(int score)
    {
        if (I == null || !I.useResult) return;

        ResultTier hit = TierFor(score);
        if (hit == null) return;

        Debug.Log($"[Audio] 売上 {score:N0}円 → 「{hit.name}」／称号「{hit.title}」");
        I.PlayOne(hit.clip);
    }

    /// <summary>
    /// 判定のときの音。拍子木を鳴らします。
    ///
    /// 正解音・不正解音は既定でオフにしてあります。
    /// 鳴らしたくなったら Inspector の Play Result Chime を入れてください。
    /// </summary>
    public static void JudgeResult(int correct, int total)
    {
        if (I == null) return;

        if (I.useJudge) I.PlayOne(I.seJudge);

        if (!I.playResultChime) return;

        bool good = total <= 0 || correct * 2 >= total;
        AudioClip clip = good ? I.seCorrect : I.seWrong;

        if (clip != null) I.StartCoroutine(I.PlayLater(clip, 0.35f, 1f));
    }
}
