using UnityEngine;
using TMPro;

namespace CrazyBowling.UI
{
    /// <summary>
    /// UI の見た目の材料をまとめた設定（段階6）。
    /// フォント・文字のマテリアル・光る部品の画像・レーンごとの差し色を持つ。
    ///
    /// ── 光らせ方 ──────────────────────────────────
    ///
    /// UI の Canvas は Screen Space Overlay なので、画面の Bloom（光のにじみ）が効かない。
    /// 光は「TMP のマテリアルの縁取り・光（Glow）・下敷きの影」と、
    /// 「光のにじみを描いた画像（ネオンの枠・光の玉・光の筋）」で作る。
    ///
    /// 読みやすさのため、どの文字にも暗い縁取りか下敷きの影を付ける。派手さは縁取りと光で足す。
    /// </summary>
    [CreateAssetMenu(fileName = "UISkin", menuName = "CrazyBowling/UI Skin")]
    public class UISkin : ScriptableObject
    {
        [Header("フォント")]
        [Tooltip("太い文字（得点・レーン名・見出し）。英数字と記号だけ焼いてある。")]
        [SerializeField] private TMP_FontAsset boldFont;

        [Tooltip("普通の文字（説明文などの日本語）。")]
        [SerializeField] private TMP_FontAsset regularFont;

        [Header("文字のマテリアル")]
        [Tooltip("光る太字（色の付いた縁取りと光）。色は使う側が実行時に変える。")]
        [SerializeField] private Material boldNeonMaterial;

        [Tooltip("読みやすい太字（暗い縁取りと影）。")]
        [SerializeField] private Material boldPlainMaterial;

        [Tooltip("読みやすい普通の文字（暗い縁取りと影）。説明文は淡々と出す。")]
        [SerializeField] private Material regularPlainMaterial;

        [Header("光る部品（白。色は使う側が付ける）")]
        [SerializeField] private Sprite neonFrame;
        [SerializeField] private Sprite panel;
        [SerializeField] private Sprite glow;
        [SerializeField] private Sprite ray;
        [SerializeField] private Sprite band;
        [SerializeField] private Sprite confetti;
        [SerializeField] private Sprite star;
        [SerializeField] private Sprite ring;

        [Header("色")]
        [Tooltip("レーンごとの差し色（1本目から順に）。得点板・見出し・ボタンの光に使う。")]
        [SerializeField] private Color[] laneAccents =
        {
            new Color(1.00f, 0.62f, 0.15f),
            new Color(0.25f, 0.75f, 1.00f),
            new Color(1.00f, 0.82f, 0.35f),
            new Color(1.00f, 0.25f, 0.85f),
            new Color(1.00f, 0.30f, 0.25f),
            new Color(0.30f, 0.95f, 1.00f),
            new Color(1.00f, 0.45f, 0.50f),
            new Color(0.45f, 1.00f, 0.55f),
            new Color(1.00f, 0.85f, 0.20f),
            new Color(0.30f, 0.60f, 1.00f),
        };

        [Tooltip("レーンが無いとき（タイトルなど）の差し色。")]
        [SerializeField] private Color defaultAccent = new Color(1f, 0.3f, 0.8f);

        [Tooltip("ストライク・スペアの金色。")]
        [SerializeField] private Color gold = new Color(1f, 0.82f, 0.2f);

        [Tooltip("暗い板の色。文字の後ろに敷いて読みやすくする。")]
        [SerializeField] private Color panelColor = new Color(0.02f, 0.02f, 0.06f, 0.78f);

        public TMP_FontAsset BoldFont => boldFont;
        public TMP_FontAsset RegularFont => regularFont;
        public Material BoldNeonMaterial => boldNeonMaterial;
        public Material BoldPlainMaterial => boldPlainMaterial;
        public Material RegularPlainMaterial => regularPlainMaterial;
        public Sprite NeonFrame => neonFrame;
        public Sprite Panel => panel;
        public Sprite Glow => glow;
        public Sprite Ray => ray;
        public Sprite Band => band;
        public Sprite Confetti => confetti;
        public Sprite Star => star;
        public Sprite Ring => ring;
        public Color Gold => gold;
        public Color PanelColor => panelColor;
        public Color DefaultAccent => defaultAccent;

        /// <summary>そのレーン（1から数える）の差し色。範囲外なら既定の色。</summary>
        public Color GetAccent(int laneNumber)
        {
            int index = laneNumber - 1;
            return laneAccents != null && index >= 0 && index < laneAccents.Length
                ? laneAccents[index]
                : defaultAccent;
        }
    }
}
