using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 客の絵を差し替えたときに、比率がつぶれないように枠を調整するための道具。
///
/// Unity の Image は「枠の大きさ」と「絵」が別管理です。
/// 絵だけ差し替えると、新しい絵が古い枠に引き伸ばされてつぶれます。
/// このクラスは、差し替えと同時に枠の大きさを絵の比率に合わせ直します。
///
/// Customer / Customer2 / Customer3 から呼ばれます。
/// このファイル自体はシーンに置きません（static なので置けません）。
/// </summary>
public static class CustomerSpriteFit
{
    public enum FitMode
    {
        /// <summary>何もしない。今までどおり枠に引き伸ばす</summary>
        なにもしない,

        /// <summary>高さをそろえる。背丈が変わらないので、人物には基本これ</summary>
        高さをそろえる,

        /// <summary>幅をそろえる。横幅を決め打ちしたいとき</summary>
        幅をそろえる,

        /// <summary>見た目の面積をそろえる。縦長と横長が混ざるときに無難</summary>
        面積をそろえる,
    }

    /// <summary>
    /// 絵を差し替えて、比率が崩れないように枠を合わせ直す。
    /// </summary>
    /// <param name="image">対象の Image</param>
    /// <param name="sprite">差し替える絵</param>
    /// <param name="baseSize">基準にする枠の大きさ（差し替え前の大きさ）</param>
    /// <param name="mode">何をそろえるか</param>
    /// <param name="scale">追加の倍率。1 でそのまま</param>
    /// <param name="offset">追加の位置調整</param>
    /// <param name="keepFeetOnGround">下端（足元）の位置を保つか</param>
    public static void Apply(
        Image image,
        Sprite sprite,
        Vector2 baseSize,
        FitMode mode,
        float scale = 1f,
        Vector2 offset = default,
        bool keepFeetOnGround = true)
    {
        if (image == null || sprite == null) return;

        RectTransform rt = image.rectTransform;

        Vector2 sizeBefore = rt.rect.size;

        image.sprite = sprite;

        if (mode == FitMode.なにもしない)
        {
            rt.anchoredPosition += offset;
            return;
        }

        // 絵そのものの縦横比
        float spriteW = sprite.rect.width;
        float spriteH = sprite.rect.height;
        if (spriteW <= 0f || spriteH <= 0f) return;

        float aspect = spriteW / spriteH;

        // 基準サイズが取れていなければ、今の大きさを使う
        if (baseSize.x <= 0f || baseSize.y <= 0f) baseSize = sizeBefore;
        if (baseSize.x <= 0f || baseSize.y <= 0f) return;

        float w, h;

        switch (mode)
        {
            case FitMode.高さをそろえる:
                h = baseSize.y;
                w = h * aspect;
                break;

            case FitMode.幅をそろえる:
                w = baseSize.x;
                h = w / aspect;
                break;

            case FitMode.面積をそろえる:
                float area = baseSize.x * baseSize.y;
                h = Mathf.Sqrt(area / aspect);
                w = h * aspect;
                break;

            default:
                return;
        }

        if (scale <= 0f) scale = 1f;
        w *= scale;
        h *= scale;

        rt.sizeDelta = new Vector2(w, h);

        // pivot が中央（0.5）だと、高さが変わったぶん足元が浮き沈みします。
        // その差を打ち消して、下端の位置を保ちます。
        if (keepFeetOnGround)
        {
            float heightDiff = h - sizeBefore.y;
            float pivotY = rt.pivot.y;
            rt.anchoredPosition += new Vector2(0f, heightDiff * pivotY);
        }

        rt.anchoredPosition += offset;
    }

    /// <summary>
    /// Start のタイミングで基準サイズを控えるための小道具。
    /// アンカーが引き伸ばし設定になっていると rect.size が意図とずれるので、
    /// そのときは警告を出します。
    /// </summary>
    public static Vector2 CaptureBaseSize(RectTransform rt, string ownerName)
    {
        if (rt == null) return Vector2.zero;

        bool stretched =
            !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x) ||
            !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);

        if (stretched)
        {
            Debug.LogWarning(
                $"{ownerName}: アンカーが引き伸ばし設定になっています。" +
                "客の絵の大きさ調整は、アンカーを1点（中央や下中央）にしてください。");
        }

        return rt.rect.size;
    }
}
