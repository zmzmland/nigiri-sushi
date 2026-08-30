using System.Collections.Generic;
using UnityEngine;

/// <summary>遊ぶモード。タイトルの選択画面で決まります。</summary>
public enum GameModeId
{
    日本語,
    英語,
}

/// <summary>注文の見せ方。</summary>
public enum OrderStyle
{
    イラスト,
    カタカナ,
    漢字,
    英語,
}

/// <summary>
/// いま選ばれているモードと、その設定。
///
/// static なのでシーンをまたいでも残ります。
/// タイトルで選ばれなかった場合は「日本語」で動きます。
///
/// 【モードの中身】
///   日本語 … 日本語で注文（既定はカタカナ）。注文票が判定まで残る
///   英語   … 英語で注文。注文票が判定まで残る
///
/// 日本語モードの表記（カタカナ / 漢字 / イラスト）は、
/// タイトルの ModeSelect の Inspector で切り替えられます。
///
/// 【ネタを増やすとき】
///   下の Kanji / English の表に1行足してください。
///   表に無いネタは、スプライト名がそのまま出ます。
/// </summary>
public static class GameMode
{
    public static GameModeId Current = GameModeId.日本語;

    /// <summary>
    /// 日本語モードで注文をどう見せるか。
    /// タイトルの ModeSelect の Inspector から変えられます。
    /// カタカナ / 漢字 / イラスト から選べます。
    /// </summary>
    public static OrderStyle JapaneseStyle = OrderStyle.カタカナ;

    // =====================================================
    //  モードごとの設定
    // =====================================================
    /// <summary>注文をどう見せるか。</summary>
    public static OrderStyle Style
    {
        get
        {
            return Current == GameModeId.英語 ? OrderStyle.英語 : JapaneseStyle;
        }
    }

    /// <summary>注文票を出すか（判定まで残るか）。どちらのモードでも出します。</summary>
    public static bool KeepOrderBoard = true;

    /// <summary>番付を分けるための名前。ranking_日本語.json のように使われます。</summary>
    public static string RankingKey => Current.ToString();

    /// <summary>画面に出すモード名。</summary>
    public static string DisplayName
    {
        get
        {
            return Current == GameModeId.英語 ? "ENGLISH" : "日本語";
        }
    }

    // =====================================================
    //  ネタの名前
    // =====================================================
    //  キーは Unity のスプライト名（= YOLO のクラス名）。
    //  ここを変えても認識には影響しません。表示だけの話です。

    private static readonly Dictionary<string, string> Kanji = new Dictionary<string, string>
    {
        { "maguro", "鮪"   },
        { "salmon", "鮭"   },
        { "ika",    "烏賊" },
        { "tai",    "鯛"   },
        { "tamago", "玉子" },
        { "ebi",    "海老" },
        { "tako",   "蛸"   },
        { "uni",    "雲丹" },
        { "ikura",  "いくら" },   // イクラに定まった漢字は無いので、かな表記
        { "hotate", "帆立" },
        { "anago",  "穴子" },
        { "aji",    "鯵"   },
        { "natto",  "納豆" },
        { "makizusi", "巻寿司" },
    };

    private static readonly Dictionary<string, string> Kana = new Dictionary<string, string>
    {
        { "maguro", "マグロ" },
        { "salmon", "サーモン" },
        { "ika",    "イカ"   },
        { "tai",    "タイ"   },
        { "tamago", "タマゴ" },
        { "ebi",    "エビ"   },
        { "tako",   "タコ"   },
        { "uni",    "ウニ"   },
        { "ikura",  "イクラ" },
        { "hotate", "ホタテ" },
        { "anago",  "アナゴ" },
        { "aji",    "アジ"   },
        { "natto",  "ナットウ" },
        { "makizusi", "マキズシ" },
    };

    private static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        { "maguro", "TUNA"      },
        { "salmon", "SALMON"    },
        { "ika",    "SQUID"     },
        { "tai",    "SEA BREAM" },
        { "tamago", "EGG"       },
        { "ebi",    "SHRIMP"    },
        { "tako",   "OCTOPUS"   },
        { "uni",    "SEA URCHIN"},
        { "ikura",  "SALMON ROE"},
        { "hotate", "SCALLOP"   },
        { "anago",  "CONGER EEL"},
        { "aji",    "HORSE MACKEREL" },
        { "natto",  "NATTO"     },
        { "makizusi", "SUSHI ROLL" },
    };

    /// <summary>
    /// スプライト名を、いまのモードの表記に直す。
    /// 表に無ければスプライト名をそのまま返します。
    /// </summary>
    public static string LabelFor(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return "";

        Dictionary<string, string> table =
            Style == OrderStyle.英語    ? English :
            Style == OrderStyle.漢字    ? Kanji :
            Style == OrderStyle.カタカナ ? Kana : null;

        if (table == null) return spriteName;

        return table.TryGetValue(spriteName, out string s) ? s : spriteName;
    }

    /// <summary>スプライトから表記を得る。</summary>
    public static string LabelFor(Sprite sprite)
    {
        return sprite == null ? "" : LabelFor(sprite.name);
    }
}
