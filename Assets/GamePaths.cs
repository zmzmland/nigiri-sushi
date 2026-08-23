using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Unity と Python が共有するファイルの置き場所を 1 箇所に集約する。
/// Editor / ビルド後 / Mac / Windows のどれでも同じ絶対パスに解決されるよう、
/// ユーザーのホームディレクトリを基準にする。
///
/// Python 側も同じ場所を見るようにすること:
///     BASE_DIR = Path.home() / "TGS_ImageSearch"
/// </summary>
public static class GamePaths
{
    public const string FolderName = "TGS_ImageSearch";

    public static readonly string BaseDir;

    public static string ResultPath  => Path.Combine(BaseDir, "result.txt");
    public static string OrderPath   => Path.Combine(BaseDir, "order.txt");
    public static string TriggerPath => Path.Combine(BaseDir, "capture_trigger.txt");

    static GamePaths()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // UserProfile が空になる環境向けのフォールバック
        if (string.IsNullOrEmpty(home))
        {
            home = Application.persistentDataPath;
        }

        BaseDir = Path.Combine(home, FolderName);

        try
        {
            Directory.CreateDirectory(BaseDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GamePaths] 共有フォルダの作成に失敗しました: {BaseDir} / {e.Message}");
        }

        Debug.Log($"[GamePaths] BaseDir = {BaseDir}");
    }

    // ---------------------------------------------------------
    // 例外に強い IO ヘルパー（外部プロセスと同じファイルを触るため）
    // ---------------------------------------------------------
    public static string SafeRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path).Trim();
        }
        catch (IOException)
        {
            // Python 側が書き込み中でロックされている。次のポーリングで再試行。
            return null;
        }
    }

    public static bool SafeWrite(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
            return true;
        }
        catch (IOException e)
        {
            Debug.LogError($"[GamePaths] 書き込み失敗: {path} / {e.Message}");
            return false;
        }
    }

    public static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[GamePaths] 削除失敗: {path} / {e.Message}");
        }
    }
}
