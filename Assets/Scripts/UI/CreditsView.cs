using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// クレジットの画面（段階6）。タイトルの CREDITS ボタンで開く。
    /// 見た目は遊び方・記録の画面と同じネオンの板。いちばん上に「produced by 夜中のBBQ」、その下に音源の作者（<see cref="UIText.CreditsSectionLines"/>）。
    /// 長いので、上下になぞって（ホイールで）送る。右の細い棒で、今どのあたりかが分かる。
    /// 中身はコードで組み立てる。点滅はさせない。ゲームの進行には関わらない。
    /// </summary>
    public class CreditsView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("投球が始まったら閉じる（通しの確認のとき）。空なら同じシーンから探す。")]
        [SerializeField] private ThrowSequencer sequencer;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("クレジットの画面を置く入れ物（画面全体）。")]
        [SerializeField] private RectTransform root;

        [Header("タイトルの CREDITS ボタン")]
        [Tooltip("位置を合わせる相手（タイトルの始めるボタン）。CREDITS ボタンはこの下に置く。空ならボタンを作らない。")]
        [SerializeField] private RectTransform startButton;

        [Tooltip("始めるボタンに対する CREDITS ボタンの大きさ（幅・高さの割合）。RECORDS・HOW TO PLAY と揃える。")]
        [SerializeField] private Vector2 buttonScale = new Vector2(0.390625f, 0.45f);

        [Tooltip("始めるボタンと CREDITS ボタンの間（ピクセル）。")]
        [SerializeField] private float buttonGap = 8f;

        [Tooltip("CREDITS ボタンの横のずらし（始めるボタンの幅に対する割合。+ で右）。3つ並べたときの右。")]
        [SerializeField] private float buttonShiftX = 0.4296875f;

        [Tooltip("CREDITS ボタンの文字の大きさ（いちばん大きいとき）。")]
        [SerializeField] private float buttonFontSize = 34f;

        [Header("文字")]
        [Tooltip("「夜中のBBQ」の文字の大きさ。")]
        [SerializeField] private float producerSize = 64f;

        [Tooltip("節の見出し（MUSIC など）の文字の大きさ。")]
        [SerializeField] private float headingSize = 40f;

        [Tooltip("左の札（LANE 01 など）の文字の大きさ。")]
        [SerializeField] private float labelSize = 26f;

        [Tooltip("本文の文字の大きさ。")]
        [SerializeField] private float bodySize = 30f;

        [Tooltip("節の説明（札の無い行）の文字の大きさ。")]
        [SerializeField] private float noteSize = 26f;

        [Tooltip("本文の行間（TextMeshPro の行間の調整）。")]
        [SerializeField] private float bodyLineSpacing = 12f;

        [Tooltip("左の札の幅（本文の入れ物の幅に対する割合）。")]
        [Range(0.1f, 0.5f)]
        [SerializeField] private float labelWidth = 0.2f;

        [Tooltip("左の札を下げる量（ピクセル）。本文（普通の文字）の1行目と高さを揃える。")]
        [SerializeField] private float labelTopOffset = 13f;

        [Tooltip("行と行の間（ピクセル）。")]
        [SerializeField] private float rowGap = 10f;

        [Tooltip("節と節の間（ピクセル）。")]
        [SerializeField] private float sectionGap = 56f;

        [Tooltip("本文の色。説明書らしく淡々と白で出す。")]
        [SerializeField] private Color bodyColor = new Color(0.92f, 0.92f, 0.96f);

        [Tooltip("節の説明の色（本文より少し淡く）。")]
        [SerializeField] private Color noteColor = new Color(0.72f, 0.74f, 0.82f);

        [Header("見た目")]
        [Tooltip("開くときの動きの時間（秒）。")]
        [SerializeField] private float openSeconds = 0.3f;

        [Tooltip("板の下に敷く濃い色（後ろが透けないように）。")]
        [SerializeField] private Color solidColor = new Color(0.01f, 0.01f, 0.04f, 0.95f);

        [Tooltip("なぞって送るときの速さ（ホイール1目盛りで動く量）。")]
        [SerializeField] private float scrollSensitivity = 60f;

        /// <summary>ネオンにするボタン1つぶん。</summary>
        private struct NeonButton
        {
            public Image frame;
            public Image glow;
            public TMP_Text label;
            public float offset;
        }

        private readonly List<NeonButton> _buttons = new List<NeonButton>();
        private readonly List<TMP_Text> _headings = new List<TMP_Text>();
        private CanvasGroup _group;
        private RectTransform _panel;
        private RectTransform _content;
        private ScrollRect _scroll;
        private TMP_Text _title;
        private TMP_Text _producer;
        private float _openTime;
        private bool _open;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _open;

        /// <summary>開いた・閉じたときに知らせる（あいさつの声・DJ の番組を止めるのに使う）。</summary>
        public event System.Action Opened;
        public event System.Action Closed;

        /// <summary>中身の高さと、見える高さ（確かめるとき用）。</summary>
        public float ContentHeight => _content != null ? _content.rect.height : 0f;
        public float ViewportHeight => _scroll != null ? ((RectTransform)_scroll.transform).rect.height : 0f;

        /// <summary>送った位置（1 がいちばん上、0 がいちばん下。確かめるとき用）。</summary>
        public float ScrollPosition
        {
            get => _scroll != null ? _scroll.verticalNormalizedPosition : 1f;
            set
            {
                if (_scroll != null)
                {
                    _scroll.StopMovement();
                    _scroll.verticalNormalizedPosition = Mathf.Clamp01(value);
                }
            }
        }

        private void Awake()
        {
            if (sequencer == null)
            {
                sequencer = FindFirstObjectByType<ThrowSequencer>();
            }
            if (root == null || skin == null)
            {
                return;
            }

            _group = root.GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = root.gameObject.AddComponent<CanvasGroup>();
            }
            SetVisible(false);
            BuildTitleButton();
        }

        private void OnEnable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted += Close;
            }
        }

        private void OnDisable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted -= Close;
            }
            if (_open)
            {
                Close();
            }
        }

        /// <summary>開く（いつもいちばん上から）。</summary>
        public void Open()
        {
            if (root == null || skin == null)
            {
                return;
            }
            if (_panel == null)
            {
                Build();
            }
            bool wasOpen = _open;
            // 先に出してから並べ直す（隠れたままだと並べ直しが効かない）
            _open = true;
            _openTime = Time.unscaledTime;
            SetVisible(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            ScrollPosition = 1f;
            if (!wasOpen)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>閉じる。</summary>
        public void Close()
        {
            bool wasOpen = _open;
            _open = false;
            SetVisible(false);
            if (wasOpen)
            {
                Closed?.Invoke();
            }
        }

        private void SetVisible(bool on)
        {
            if (_group == null)
            {
                return;
            }
            _group.alpha = on ? 1f : 0f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
            if (root.gameObject.activeSelf != on)
            {
                root.gameObject.SetActive(on);
            }
        }

        // ================= 組み立て =================

        /// <summary>タイトルの始めるボタンの下に CREDITS ボタンを作る（1回だけ。RECORDS・HOW TO PLAY と3つ並ぶ）。</summary>
        private void BuildTitleButton()
        {
            if (startButton == null)
            {
                return;
            }
            RectTransform rect = NeonUI.CreateRect(startButton.parent, "CreditsButton", startButton.anchorMin, startButton.anchorMax, Vector2.zero, Vector2.zero);
            rect.pivot = startButton.pivot;
            Vector2 size = startButton.rect.size;
            rect.sizeDelta = new Vector2(size.x * buttonScale.x, size.y * buttonScale.y);
            // 始めるボタンの下の端から、間をあけて下に置く（ピボットの位置を考えて合わせる）
            float startBottom = startButton.anchoredPosition.y - size.y * startButton.pivot.y;
            float height = rect.sizeDelta.y;
            rect.anchoredPosition = new Vector2(startButton.anchoredPosition.x + size.x * buttonShiftX, startBottom - buttonGap - height * (1f - rect.pivot.y));
            rect.SetSiblingIndex(startButton.GetSiblingIndex() + 1);
            CreateButton(rect, UIText.Credits, buttonFontSize, Open);
        }

        /// <summary>画面を作る（1回だけ。中身は決まっているので作り直さない）。</summary>
        private void Build()
        {
            // 後ろを暗くして、下を触れないようにする
            RectTransform dim = NeonUI.CreateStretch(root, "Dim");
            Image dimImage = NeonUI.CreateImage(dim, null, new Color(0f, 0f, 0f, 0.82f), false);
            dimImage.raycastTarget = true;

            _panel = NeonUI.CreateRect(root, "Panel", new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f), Vector2.zero, Vector2.zero);
            NeonUI.CreateImage(NeonUI.CreateRect(_panel, "Solid", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f)), null, solidColor, false);
            NeonUI.CreateImage(NeonUI.CreateStretch(_panel, "Plate"), skin.Panel, skin.PanelColor, true);
            Image frame = NeonUI.CreateImage(NeonUI.CreateRect(_panel, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12f, 12f)), skin.NeonFrame, Color.white, true);
            frame.pixelsPerUnitMultiplier = 1.6f;
            _buttons.Add(new NeonButton { frame = frame, offset = 0.6f });

            _title = Bold(_panel, "Title", new Vector2(0.03f, 0.88f), new Vector2(0.6f, 0.985f), UIText.Credits, 72f, TextAlignmentOptions.Left, true);
            Bold(_panel, "ScrollHint", new Vector2(0.6f, 0.9f), new Vector2(0.97f, 0.97f), UIText.CreditsScrollHint, 26f, TextAlignmentOptions.Right, false).color = noteColor;

            // 流れる入れ物（なぞって・ホイールで上下に送る）
            RectTransform viewport = NeonUI.CreateRect(_panel, "Viewport", new Vector2(0.03f, 0.14f), new Vector2(0.955f, 0.87f), Vector2.zero, Vector2.zero);
            Image viewportImage = NeonUI.CreateImage(viewport, null, new Color(0f, 0f, 0f, 0.001f), false);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = NeonUI.CreateRect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _content.pivot = new Vector2(0.5f, 1f);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = rowGap;
            layout.padding = new RectOffset(0, 0, 8, 24);
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = scrollSensitivity;

            // 右の細い棒（今どのあたりかの目安。つまんで動かすこともできる）
            RectTransform bar = NeonUI.CreateRect(_panel, "Scrollbar", new Vector2(0.962f, 0.14f), new Vector2(0.972f, 0.87f), Vector2.zero, Vector2.zero);
            Image track = NeonUI.CreateImage(bar, null, new Color(1f, 1f, 1f, 0.08f), false);
            track.raycastTarget = true;
            RectTransform handleArea = NeonUI.CreateStretch(bar, "HandleArea");
            RectTransform handle = NeonUI.CreateStretch(handleArea, "Handle");
            Image handleImage = NeonUI.CreateImage(handle, null, new Color(0.6f, 0.9f, 1f, 0.55f), false);
            handleImage.raycastTarget = true;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            FillContent();

            CreateButton(NeonUI.CreateRect(_panel, "CloseButton", new Vector2(0.8f, 0.025f), new Vector2(0.97f, 0.11f), Vector2.zero, Vector2.zero), UIText.RecordsClose, 36f, Close);
        }

        /// <summary>中身を並べる：名義 → 前書き → 節ごとに見出しと行 → 結び。</summary>
        private void FillContent()
        {
            // いちばん上：produced by 夜中のBBQ（タイトルの名義と同じ表記）
            Line(_content, "ProducedBy", UIText.CreditPrefix, skin.RegularFont, skin.RegularPlainMaterial, 30f, noteColor, TextAlignmentOptions.Center, 0f);
            _producer = Line(_content, "Producer", UIText.CreditName, skin.BoldFont, skin.BoldNeonMaterial, producerSize, new Color(1f, 0.6f, 0.3f), TextAlignmentOptions.Center, 0f);
            Space("IntroSpace", 24f);
            Line(_content, "Intro", UIText.CreditsIntro, skin.RegularFont, skin.RegularPlainMaterial, bodySize, bodyColor, TextAlignmentOptions.Center, bodyLineSpacing);

            int sections = Mathf.Min(UIText.CreditsHeadings.Length, UIText.CreditsSectionLines.Length);
            for (int s = 0; s < sections; s++)
            {
                Space("SectionSpace" + (s + 1), sectionGap);
                TMP_Text heading = Line(_content, "Heading" + (s + 1), UIText.CreditsHeadings[s], skin.BoldFont, skin.BoldNeonMaterial, headingSize, Color.white, TextAlignmentOptions.Left, 0f);
                _headings.Add(heading);
                string[] lines = UIText.CreditsSectionLines[s];
                for (int i = 0; i < lines.Length; i++)
                {
                    int tab = lines[i].IndexOf('\t');
                    if (tab < 0)
                    {
                        Line(_content, $"Note{s + 1}_{i + 1}", lines[i], skin.RegularFont, skin.RegularPlainMaterial, noteSize, noteColor, TextAlignmentOptions.Left, bodyLineSpacing);
                    }
                    else
                    {
                        Row($"Row{s + 1}_{i + 1}", lines[i].Substring(0, tab), lines[i].Substring(tab + 1), skin.GetAccent(s * 3 + 1));
                    }
                }
            }

            Space("OutroSpace", sectionGap);
            Line(_content, "Outro", UIText.CreditsOutro, skin.RegularFont, skin.RegularPlainMaterial, bodySize, bodyColor, TextAlignmentOptions.Center, bodyLineSpacing);
        }

        /// <summary>1行（折り返す）。</summary>
        private TMP_Text Line(RectTransform parent, string name, string text, TMP_FontAsset font, Material material, float size, Color color, TextAlignmentOptions alignment, float lineSpacing)
        {
            RectTransform rect = NeonUI.CreateRect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = NeonUI.CreateText(rect, font, material, size, color, alignment);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.lineSpacing = lineSpacing;
            label.text = text;
            return label;
        }

        /// <summary>札と本文を左右に並べた1行。高さは本文に合わせる。</summary>
        private void Row(string name, string labelText, string bodyText, Color accent)
        {
            RectTransform row = NeonUI.CreateRect(_content, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.UpperLeft;
            h.spacing = 16f;

            TMP_Text label = Line(row, "Label", labelText, skin.BoldFont, skin.BoldPlainMaterial, labelSize, accent, TextAlignmentOptions.TopLeft, 0f);
            label.margin = new Vector4(0f, labelTopOffset, 0f, 0f);
            var labelLayout = label.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = labelWidth;
            labelLayout.preferredWidth = 0f;
            labelLayout.minWidth = 0f;

            TMP_Text body = Line(row, "Body", bodyText, skin.RegularFont, skin.RegularPlainMaterial, bodySize, bodyColor, TextAlignmentOptions.TopLeft, bodyLineSpacing);
            var bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleWidth = 1f - labelWidth;
            bodyLayout.preferredWidth = 0f;
            bodyLayout.minWidth = 0f;
        }

        private void Space(string name, float height)
        {
            RectTransform spacer = NeonUI.CreateRect(_content, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = spacer.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
        }

        // ================= 部品 =================

        /// <summary>太い文字（英数字）。neon なら光る太字。</summary>
        private TMP_Text Bold(RectTransform parent, string name, Vector2 min, Vector2 max, string text, float size, TextAlignmentOptions alignment, bool neon)
        {
            RectTransform rect = NeonUI.CreateRect(parent, name, min, max, Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = NeonUI.CreateText(rect, skin.BoldFont, neon ? skin.BoldNeonMaterial : skin.BoldPlainMaterial, size, Color.white, alignment);
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(12f, size);
            label.fontSizeMax = size;
            label.text = text;
            return label;
        }

        /// <summary>ネオンのボタンを作る（光の玉・光る枠・文字・押したときの音）。</summary>
        private Button CreateButton(RectTransform rect, string text, float size, UnityAction onClick)
        {
            RectTransform glowRect = NeonUI.CreateRect(rect, "NeonGlow", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(56f, 56f));
            Image glow = NeonUI.CreateImage(glowRect, skin.Glow, Color.clear, false);
            Image plate = NeonUI.CreateImage(NeonUI.CreateStretch(rect, "Plate"), skin.Panel, new Color(0f, 0f, 0f, 0.55f), true);
            plate.raycastTarget = true;
            Image frame = NeonUI.CreateImage(NeonUI.CreateRect(rect, "NeonFrame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12f, 12f)), skin.NeonFrame, Color.white, true);
            frame.pixelsPerUnitMultiplier = 1.6f;
            TMP_Text label = Bold(rect, "Label", new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f), text, size, TextAlignmentOptions.Center, true);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(onClick);
            rect.gameObject.AddComponent<ButtonSound>();
            _buttons.Add(new NeonButton { frame = frame, glow = glow, label = label, offset = Random.value });
            return button;
        }

        // ================= 動き =================

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            for (int i = _buttons.Count - 1; i >= 0; i--)
            {
                NeonButton b = _buttons[i];
                if (b.frame == null)
                {
                    _buttons.RemoveAt(i);
                    continue;
                }
                if (!b.frame.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Color accent = NeonUI.Hue(now / 6f + b.offset, 0.75f);
                float breath = NeonUI.Breath(2.2f, b.offset);
                b.frame.color = Color.Lerp(accent, Color.white, 0.25f);
                if (b.glow != null)
                {
                    b.glow.color = NeonUI.WithAlpha(accent, 0.18f + 0.14f * breath);
                }
                NeonUI.SetNeonColor(b.label, accent, 0.35f + 0.25f * breath);
            }

            if (!_open || _panel == null)
            {
                return;
            }

            // 開くとき：少し大きいところから縮んで止まる
            float t = (now - _openTime) / Mathf.Max(openSeconds, 0.01f);
            float scale = Mathf.LerpUnclamped(1.06f, 1f, NeonUI.EaseOutBack(t));
            _panel.localScale = new Vector3(scale, scale, 1f);
            _group.alpha = Mathf.Clamp01(t * 2f);

            NeonUI.SetNeonColor(_title, NeonUI.Hue(now * 0.1f, 0.85f), 0.55f + 0.25f * NeonUI.Breath(2f));
            // 名義はタイトルと同じく、夜の炭火のような橙〜赤でゆっくり揺らぐ（点滅させない）
            NeonUI.SetNeonColor(_producer, Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.2f, 0.12f), NeonUI.Breath(3.2f)), 0.5f);
            for (int i = 0; i < _headings.Count; i++)
            {
                NeonUI.SetNeonColor(_headings[i], NeonUI.Hue(now * 0.06f + i * 0.17f, 0.75f), 0.4f);
            }
        }
    }
}
