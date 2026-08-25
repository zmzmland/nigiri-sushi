using System.Collections.Generic;
using UnityEngine;

/// <summary>ネタ1つぶんの「名前の画像」。</summary>
[System.Serializable]
public class OrderNameArt
{
    [Tooltip("スプライトのファイル名（maguro など）。YOLO のクラス名と同じもの")]
    public string spriteName;

    [Tooltip("カタカナの画像（マグロ）")]
    public Sprite kana;

    [Tooltip("漢字の画像（鮪）")]
    public Sprite kanji;

    [Tooltip("英語の画像（tuna）")]
    public Sprite english;
}

/// <summary>
/// 注文の名前を「画像」で持つための表。
///
/// 筆文字などは、フォントで出すより画像のほうが確実にきれいです。
/// ここに登録した画像があれば、そちらが優先して使われます。
/// 登録が無いネタは、フォント（TextMeshPro）で文字として出ます。
///
/// 【作り方】
///   1. Project ウィンドウで右クリック
///   2. Create → にぎり寿司 → 注文の文字画像
///   3. できたアセットに、ネタごとの画像を登録する
///   4. 各シーンの客の Inspector の「Name Art」にドラッグする
///
///   1つ作れば3つのシーンで使い回せます。
/// </summary>
[CreateAssetMenu(fileName = "OrderNameArt", menuName = "にぎり寿司/注文の文字画像")]
public class OrderNameArtSet : ScriptableObject
{
    [Tooltip("ネタごとの名前の画像。ネタを増やしたらここに1行足します")]
    public List<OrderNameArt> entries = new List<OrderNameArt>();

    /// <summary>
    /// スプライト名と表記の種類から、対応する画像を返す。
    /// 登録が無ければ null（呼び出し側がフォントで出します）。
    /// </summary>
    public Sprite Find(string spriteName, OrderStyle style)
    {
        if (string.IsNullOrEmpty(spriteName) || entries == null) return null;

        foreach (OrderNameArt e in entries)
        {
            if (e == null || e.spriteName != spriteName) continue;

            switch (style)
            {
                case OrderStyle.カタカナ: return e.kana;
                case OrderStyle.漢字:    return e.kanji;
                case OrderStyle.英語:    return e.english;
                default:                 return null;   // イラストは元の絵を使う
            }
        }
        return null;
    }
}
