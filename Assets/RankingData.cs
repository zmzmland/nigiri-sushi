using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// ランキング1件分。JsonUtility で保存するので [Serializable] が要ります。
/// </summary>
[Serializable]
public class RankingEntry
{
    public int score;      // 最終売上（円）
    public int correct;    // 正解した貫数
    public int total;      // 注文された貫数
    public string date;    // 記録した日時（"2026-08-24 18:30"）
}

/// <summary>
/// JsonUtility は配列を直接扱えないので、リストを包むための入れ物。
/// </summary>
[Serializable]
public class RankingList
{
    public List<RankingEntry> entries = new List<RankingEntry>();
}

/// <summary>
/// ランキングの読み書き。
///
/// 保存先は ~/TGS_ImageSearch/ranking.json です。
/// PlayerPrefs ではなくファイルにしているのは、
///   * 展示機で中身をすぐ確認できる
///   * 消したいときにファイルを捨てるだけでよい
///   * ビルドしたアプリと Unity Editor で同じ記録を共有できる
/// ためです。
/// </summary>
public static class RankingData
{
    /// <summary>ファイルに残しておく件数。表示件数とは別です。</summary>
    public const int MaxKeep = 20;

    // -----------------------------------------------------
    //  読み込み
    // -----------------------------------------------------
    public static RankingList Load()
    {
        try
        {
            if (!File.Exists(GamePaths.RankingPath)) return new RankingList();

            string json = File.ReadAllText(GamePaths.RankingPath);
            if (string.IsNullOrWhiteSpace(json)) return new RankingList();

            RankingList list = JsonUtility.FromJson<RankingList>(json);
            if (list == null || list.entries == null) return new RankingList();

            // 念のため毎回並べ直す（手で編集されていても壊れないように）
            list.entries = list.entries
                .Where(e => e != null)
                .OrderByDescending(e => e.score)
                .Take(MaxKeep)
                .ToList();

            return list;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Ranking] 読み込みに失敗しました: {e.Message}");
            return new RankingList();
        }
    }

    // -----------------------------------------------------
    //  保存
    // -----------------------------------------------------
    public static void Save(RankingList list)
    {
        try
        {
            string json = JsonUtility.ToJson(list, true);

            // 書き込み中に落ちても壊れないよう、一時ファイル経由で置き換える
            string tmp = GamePaths.RankingPath + ".tmp";
            File.WriteAllText(tmp, json);

            if (File.Exists(GamePaths.RankingPath)) File.Delete(GamePaths.RankingPath);
            File.Move(tmp, GamePaths.RankingPath);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ranking] 保存に失敗しました: {e.Message}");
        }
    }

    // -----------------------------------------------------
    //  記録する
    // -----------------------------------------------------
    /// <summary>
    /// today の結果を登録して、何位だったかを返します（1 始まり）。
    /// 圏外だった場合は MaxKeep+1 以降の数字が返ります。
    /// </summary>
    public static int Register(int score, int correct, int total)
    {
        RankingList list = Load();

        var entry = new RankingEntry
        {
            score = score,
            correct = correct,
            total = total,
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        };

        list.entries.Add(entry);

        // OrderByDescending は安定ソートなので、同点なら先に記録したほうが上に残る
        list.entries = list.entries
            .OrderByDescending(e => e.score)
            .ToList();

        int rank = list.entries.IndexOf(entry) + 1;

        if (list.entries.Count > MaxKeep)
        {
            list.entries = list.entries.Take(MaxKeep).ToList();
        }

        Save(list);

        Debug.Log($"[Ranking] {score}円 を登録しました（{rank}位）");
        return rank;
    }

    // -----------------------------------------------------
    //  全消し（展示の開始前などに使う）
    // -----------------------------------------------------
    /// <summary>3つのモードすべての番付を消す。</summary>
    public static void ClearAll()
    {
        foreach (GameModeId id in System.Enum.GetValues(typeof(GameModeId)))
        {
            ClearFile(GamePaths.RankingPathFor(id.ToString()));
        }
        Debug.LogWarning("[Ranking] すべてのモードの番付を消去しました");
    }

    /// <summary>いま選ばれているモードの番付だけ消す。</summary>
    public static void Clear()
    {
        ClearFile(GamePaths.RankingPath);
        Debug.LogWarning($"[Ranking] {GameMode.DisplayName} の番付を消去しました");
    }

    private static void ClearFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ranking] 消去に失敗しました: {e.Message}");
        }
    }
}
