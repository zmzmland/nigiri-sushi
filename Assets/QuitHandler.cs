using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 展示用の終了処理。ESC を長押しするとアプリを終了する。
///
/// フルスクリーンのビルドには終了手段が要る。Cmd+Q でも閉じられるが、
/// 来場者が誤って押す事故を避けたいので「長押し」にしてある。
///
/// 使い方: どのシーンでも効くよう、タイトルシーン（SampleScene）の
/// 空 GameObject にアタッチし、DontDestroyOnLoad で持ち回る。
/// </summary>
public class QuitHandler : MonoBehaviour
{
    [Tooltip("ESC をこの秒数押し続けると終了する")]
    public float holdSeconds = 2f;

    [Tooltip("押している間、画面に進捗を表示する")]
    public bool showIndicator = true;

    private static QuitHandler instance;
    private float heldFor = 0f;

    void Awake()
    {
        // シーンをまたいで1つだけ生き残らせる
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.isPressed)
        {
            heldFor += Time.unscaledDeltaTime;

            if (heldFor >= holdSeconds)
            {
                Debug.Log("アプリを終了します");
                Quit();
            }
        }
        else
        {
            heldFor = 0f;
        }
    }

    void OnGUI()
    {
        if (!showIndicator || heldFor <= 0.15f) return;

        float ratio = Mathf.Clamp01(heldFor / holdSeconds);
        float barW = 220f;
        float barH = 6f;
        float x = (Screen.width - barW) / 2f;
        float y = Screen.height - 70f;

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(new Rect(x, y, barW, barH), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x, y, barW * ratio, barH), Texture2D.whiteTexture);

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
        };
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(x, y - 24f, barW, 20f), "終了しています…", style);

        GUI.color = Color.white;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
