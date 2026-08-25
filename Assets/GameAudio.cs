using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 売上に応じて鳴らすリザルトの音。
/// Min Score 以上なら、その段の音が鳴ります。
/// 上から順に判定するので、金額の大きいものを上に並べてください。
/// </summary>
[System.Serializable]
public class ResultTier
{
    [Tooltip("段の名前。表示には使いません。分かりやすさのためだけ")]
    public string name = "";

    [Tooltip("この金額以上ならこの音")]
    public int minScore = 0;

    [Tooltip("鳴らす音。空なら Resources/Audio から自動で読み込みます")]
    public AudioClip clip;

    [Tooltip("clip が空のときに Resources/Audio から探す名前")]
    public string clipName = "";
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

    [Tooltip("判定のあとに正解音・不正解音を鳴らす。" +
             "オフにすると拍子木だけになります")]
    public bool playResultChime = false;

    [Header("効果音（空なら Resources/Audio から自動で読み込み）")]
    public AudioClip seClick;
    public AudioClip seOrder;
    public AudioClip seCountdown;
    public AudioClip seJudge;
    public AudioClip seCorrect;
    public AudioClip seWrong;
    public AudioClip seCustomer;
    public AudioClip seRankIn;

    [Header("BGM")]
    public AudioClip bgmTitle;
    public AudioClip bgmGame;
    public AudioClip bgmResult;

    [Tooltip("シーンごとの BGM。空のままなら既定の割り当てを使います")]
    public List<SceneBgm> bgmTable = new List<SceneBgm>();

    [Header("リザルトの音（売上で変わる）")]
    [Tooltip("上から順に「この金額以上か」を見ます。空のままなら既定の3段階を使います")]
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
    /// 3面 × 3貫 × 500円 = 4,500円 に、スピードボーナス最大 3,000円 を足して
    /// 満点は 7,500円。それを目安に区切っています。
    /// 注文数を変えたら、この金額も Inspector で調整してください。
    /// </summary>
    private void BuildDefaultResultTiers()
    {
        if (resultTiers != null && resultTiers.Count > 0)
        {
            // 手で設定されている場合も、空の clip は名前から補う
            foreach (ResultTier tr in resultTiers)
            {
                if (tr != null && tr.clip == null && !string.IsNullOrEmpty(tr.clipName))
                    tr.clip = Load(tr.clipName);
            }
            return;
        }

        resultTiers = new List<ResultTier>
        {
            new ResultTier { name = "大入り",   minScore = 6000, clip = Load("se_result_high") },
            new ResultTier { name = "まずまず", minScore = 3000, clip = Load("se_result_mid")  },
            new ResultTier { name = "これから", minScore = 0,    clip = Load("se_result_low")  },
        };
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

        foreach (SceneBgm b in bgmTable)
        {
            if (b != null && b.sceneName == sceneName) { clip = b.clip; break; }
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

    public static void Click()        { if (I != null) I.PlayOne(I.seClick); }
    public static void Order()        { if (I != null) I.PlayOne(I.seOrder); }
    public static void Countdown()    { if (I != null) I.PlayOne(I.seCountdown, 0.7f); }
    public static void Judge()        { if (I != null) I.PlayOne(I.seJudge); }
    public static void CustomerCome() { if (I != null) I.PlayOne(I.seCustomer); }
    public static void RankIn()       { if (I != null) I.PlayOne(I.seRankIn); }

    /// <summary>
    /// リザルトで、売上に応じた音を鳴らす。
    /// どの段に当たったかは Console にも出します（調整の目安に）。
    /// </summary>
    public static void Result(int score)
    {
        if (I == null || I.resultTiers == null) return;

        ResultTier hit = null;

        foreach (ResultTier tr in I.resultTiers)
        {
            if (tr == null) continue;
            if (score >= tr.minScore) { hit = tr; break; }
        }

        if (hit == null) return;

        Debug.Log($"[Audio] 売上 {score:N0}円 → 「{hit.name}」");
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

        I.PlayOne(I.seJudge);

        if (!I.playResultChime) return;

        bool good = total <= 0 || correct * 2 >= total;
        AudioClip clip = good ? I.seCorrect : I.seWrong;

        if (clip != null) I.StartCoroutine(I.PlayLater(clip, 0.35f, 1f));
    }
}
