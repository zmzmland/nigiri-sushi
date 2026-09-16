using UnityEngine;

/// <summary>
/// OnGUI で描いている表示を、画面の大きさに合わせて拡大する。
///
/// 【なぜ必要か】
///   Canvas の UI は Canvas Scaler が画面に合わせて拡大してくれますが、
///   OnGUI はピクセル単位でそのまま描かれます。
///   そのため、800x600 で作った表示をフルスクリーン（例 1920x1080）で
///   出すと、周りだけ大きくなって文字は小さいまま、という状態になります。
///
/// 【使い方】
///   void OnGUI()
///   {
///       Matrix4x4 m = UiScale.Begin();
///       try  { ...描画... }        // Screen.width の代わりに UiScale.W
///       finally { UiScale.End(m); }
///   }
///
/// 【大きさを変えたい】
///   Extra の数字を変えてください。1.0 で従来どおり、1.5 で1.5倍です。
///   実行中に Ctrl+Shift+D のデバッグパネルからも変えられます。
/// </summary>
public static class UiScale
{
    /// <summary>この高さを基準に作られている、とみなす。</summary>
    public const float ReferenceHeight = 600f;

    /// <summary>全体をさらに何倍にするか。1.0 で従来どおり。</summary>
    public static float Extra = 1.25f;

    /// <summary>いま適用されている倍率。</summary>
    public static float Factor
    {
        get
        {
            if (Screen.height <= 0) return Mathf.Max(0.1f, Extra);
            return Mathf.Max(0.1f, (Screen.height / ReferenceHeight) * Extra);
        }
    }

    /// <summary>拡大後の座標系での画面の幅。Screen.width の代わりに使います。</summary>
    public static float W => Screen.width / Factor;

    /// <summary>拡大後の座標系での画面の高さ。Screen.height の代わりに使います。</summary>
    public static float H => Screen.height / Factor;

    /// <summary>拡大を始める。戻り値を End に渡してください。</summary>
    public static Matrix4x4 Begin()
    {
        Matrix4x4 old = GUI.matrix;
        float k = Factor;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(k, k, 1f));
        return old;
    }

    /// <summary>拡大を終える。</summary>
    public static void End(Matrix4x4 old)
    {
        GUI.matrix = old;
    }
}
