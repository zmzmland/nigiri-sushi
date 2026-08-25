using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 注文票の見た目の設定。Customer の Inspector に出ます。
///
/// 画像を使いたいときは、Project ウィンドウから
/// Board Sprite / Slot Sprite にドラッグしてください。
/// 何も入れなければ、下の色設定で単色の板が出ます。
/// </summary>
[System.Serializable]
public class OrderBoardSettings
{
    [Tooltip("注文票を出すか。外すと今までどおり吹き出しだけになります")]
    public bool show = true;

    [Tooltip("注文されるたびに1枠ずつ埋める。外すと最初から全部見えます")]
    public bool revealProgressively = true;

    // ---------------------------------------------------------
    [Header("画像（入れなくても動きます）")]

    [Tooltip("札そのものの画像。木札や巻物などをここに入れます")]
    public Sprite boardSprite;

    [Tooltip("寿司1つぶんの枠の画像。皿や升目などをここに入れます")]
    public Sprite slotSprite;

    [Tooltip("画像を9スライスで伸ばす。Sprite Editor で Border を設定した画像だけ有効")]
    public bool sliceImages = false;

    // ---------------------------------------------------------
    [Header("位置と大きさ")]

    [Tooltip("画面の上に置くか、下に置くか")]
    public BoardPosition position = BoardPosition.画面の上;

    [Tooltip("画面の端からの距離")]
    public float margin = 20f;

    [Tooltip("横方向のずらし量。0 で中央")]
    public float offsetX = 0f;

    [Tooltip("中身に合わせて札の大きさを自動で決める。" +
             "決まった形の画像を使うなら外して、下のサイズを指定します")]
    public bool autoSize = true;

    [Tooltip("Auto Size を外したときの札の大きさ")]
    public Vector2 fixedSize = new Vector2(640f, 140f);

    [Tooltip("寿司1つぶんの大きさ（ピクセル）")]
    public float slotSize = 90f;

    [Tooltip("寿司どうしの間隔")]
    public float spacing = 12f;

    [Tooltip("札の内側の余白")]
    public float padding = 14f;

    [Tooltip("枠の画像の内側に寿司を置くときの余白")]
    public float slotInset = 8f;

    // ---------------------------------------------------------
    [Header("色（画像を使わないとき用）")]

    [Tooltip("札の色。Board Sprite を入れているときは無視されます" +
             "（Tint Board を入れると、画像に色を掛けられます）")]
    public Color boardColor = new Color(0.16f, 0.10f, 0.06f, 0.78f);

    [Tooltip("Board Sprite に上の色を掛ける。ふつうは外したままで構いません")]
    public bool tintBoard = false;

    [Tooltip("まだ注文されていない枠の色。Slot Sprite を入れているときは無視されます")]
    public Color emptySlotColor = new Color(1f, 1f, 1f, 0.12f);

    [Tooltip("Slot Sprite に上の色を掛ける")]
    public bool tintSlot = false;
}

public enum BoardPosition
{
    画面の上,
    画面の下,
}

/// <summary>
/// 画面に出る「注文票」。
///
/// 客が言った注文がここに並んで、判定まで消えません。
/// 覚える必要がなくなるので、初めて遊ぶ人・子供・
/// 日本語が分からない人でも成立するようになります。
///
/// ★ シーンに何も置く必要はありません。
///   Customer が実行時にこのパネルを作ります。
///   出したくない場合は Inspector の Order Board → Show を外してください。
///
/// 見た目を変えたいときは、Board Sprite / Slot Sprite に画像を入れてください。
/// </summary>
public class OrderBoard : MonoBehaviour
{
    // 枠の画像（背景）と、その中に入る寿司の画像
    private readonly List<Image> frames = new List<Image>();
    private readonly List<Image> sushi  = new List<Image>();

    private OrderBoardSettings cfg;

    /// <summary>
    /// 注文票を作る。owner は Canvas の下にいる必要があります（客そのものでOK）。
    /// Canvas が見つからなければ null を返します。
    /// </summary>
    public static OrderBoard Create(Component owner, int count, OrderBoardSettings settings)
    {
        if (owner == null || settings == null || !settings.show || count <= 0) return null;

        Canvas canvas = owner.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[OrderBoard] Canvas が見つからないので注文票を出せません");
            return null;
        }

        var go = new GameObject(
            "OrderBoard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(OrderBoard));

        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();   // 一番手前に描く

        var board = go.GetComponent<OrderBoard>();
        board.cfg = settings;

        board.SetupRect(go.GetComponent<RectTransform>());
        board.SetupBackground(go.GetComponent<Image>());
        board.SetupLayout(go.GetComponent<HorizontalLayoutGroup>());

        if (settings.autoSize)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        }

        board.BuildSlots(count);
        return board;
    }

    // -----------------------------------------------------
    //  組み立て
    // -----------------------------------------------------
    private void SetupRect(RectTransform rt)
    {
        bool top = cfg.position == BoardPosition.画面の上;

        float y = top ? 1f : 0f;
        rt.anchorMin = new Vector2(0.5f, y);
        rt.anchorMax = new Vector2(0.5f, y);
        rt.pivot     = new Vector2(0.5f, y);

        rt.anchoredPosition = new Vector2(
            cfg.offsetX,
            top ? -cfg.margin : cfg.margin);

        if (!cfg.autoSize) rt.sizeDelta = cfg.fixedSize;
    }

    private void SetupBackground(Image img)
    {
        img.raycastTarget = false;

        if (cfg.boardSprite != null)
        {
            img.sprite = cfg.boardSprite;
            img.type = cfg.sliceImages ? Image.Type.Sliced : Image.Type.Simple;
            img.color = cfg.tintBoard ? cfg.boardColor : Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = cfg.boardColor;
        }
    }

    private void SetupLayout(HorizontalLayoutGroup layout)
    {
        int p = Mathf.RoundToInt(cfg.padding);
        layout.padding = new RectOffset(p, p, p, p);
        layout.spacing = cfg.spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private void BuildSlots(int count)
    {
        for (int i = 0; i < count; i++)
        {
            // --- 枠（皿・升目など） ---
            var slot = new GameObject(
                $"Slot{i}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));

            slot.transform.SetParent(transform, false);

            var le = slot.GetComponent<LayoutElement>();
            le.preferredWidth  = cfg.slotSize;
            le.preferredHeight = cfg.slotSize;

            var frame = slot.GetComponent<Image>();
            frame.raycastTarget = false;
            frame.preserveAspect = true;

            if (cfg.slotSprite != null)
            {
                frame.sprite = cfg.slotSprite;
                frame.type = cfg.sliceImages ? Image.Type.Sliced : Image.Type.Simple;
                frame.color = cfg.tintSlot ? cfg.emptySlotColor : Color.white;
            }
            else
            {
                frame.sprite = null;
                frame.color = cfg.emptySlotColor;
            }

            frames.Add(frame);

            // --- 中に入る寿司 ---
            var inner = new GameObject(
                "Sushi",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));

            inner.transform.SetParent(slot.transform, false);

            var irt = inner.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(cfg.slotInset, cfg.slotInset);
            irt.offsetMax = new Vector2(-cfg.slotInset, -cfg.slotInset);

            var img = inner.GetComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.enabled = false;          // 注文されるまで出さない

            sushi.Add(img);
        }
    }

    // -----------------------------------------------------
    //  中身を入れる
    // -----------------------------------------------------
    /// <summary>i 番目の枠に寿司を入れる。</summary>
    public void Set(int index, Sprite sprite)
    {
        if (index < 0 || index >= sushi.Count || sprite == null) return;

        sushi[index].sprite = sprite;
        sushi[index].color = Color.white;
        sushi[index].enabled = true;

        // 枠の画像を使っていない場合、埋まった枠は少し明るくする
        if (cfg != null && cfg.slotSprite == null)
        {
            Color c = cfg.emptySlotColor;
            c.a = Mathf.Min(1f, c.a + 0.10f);
            frames[index].color = c;
        }
    }

    /// <summary>まとめて全部入れる。</summary>
    public void FillAll(IList<Sprite> sprites)
    {
        if (sprites == null) return;

        int n = Mathf.Min(sprites.Count, sushi.Count);
        for (int i = 0; i < n; i++) Set(i, sprites[i]);
    }
}
