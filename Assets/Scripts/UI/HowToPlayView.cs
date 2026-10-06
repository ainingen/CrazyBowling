using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 遊び方の画面のページ分けの決まり（段階6）。MonoBehaviour に依らない（EditMode テストあり）。
    /// </summary>
    public static class HowToPlayRules
    {
        /// <summary>全部のページ数（節が0でも1ページ）。</summary>
        public static int PageCount(int sectionCount, int sectionsPerPage)
        {
            int per = Mathf.Max(sectionsPerPage, 1);
            return Mathf.Max(1, (Mathf.Max(sectionCount, 0) + per - 1) / per);
        }

        /// <summary>ページ番号を範囲に収める（0から数える）。</summary>
        public static int ClampPage(int page, int sectionCount, int sectionsPerPage)
        {
            return Mathf.Clamp(page, 0, PageCount(sectionCount, sectionsPerPage) - 1);
        }

        /// <summary>そのページに載せる節の最初の番号と数。</summary>
        public static void SectionRange(int page, int sectionCount, int sectionsPerPage, out int first, out int count)
        {
            int per = Mathf.Max(sectionsPerPage, 1);
            page = ClampPage(page, sectionCount, sectionsPerPage);
            first = page * per;
            count = Mathf.Clamp(sectionCount - first, 0, per);
        }
    }

    /// <summary>
    /// 遊び方の画面（段階6）。タイトルの HOW TO PLAY ボタンで開く。
    /// 見た目は記録の画面（<see cref="RecordsView"/>）と同じネオンの板。文面は <see cref="UIText.HowToPlayHeadings"/>・<see cref="UIText.HowToPlayBodies"/>。
    /// 文章が長いので、読みやすい大きさと行間で、ページに分けて ◀ ▶ で送る。ページに入りきらないときは上下になぞって（ホイールで）動かせる。
    /// 中身はコードで組み立てる。点滅はさせない。ゲームの進行には関わらない。
    /// </summary>
    public class HowToPlayView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("投球が始まったら閉じる（通しの確認のとき）。空なら同じシーンから探す。")]
        [SerializeField] private ThrowSequencer sequencer;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("遊び方の画面を置く入れ物（画面全体）。")]
        [SerializeField] private RectTransform root;

        [Header("タイトルの HOW TO PLAY ボタン")]
        [Tooltip("位置を合わせる相手（タイトルの始めるボタン）。HOW TO PLAY ボタンはこの下に置く。空ならボタンを作らない。")]
        [SerializeField] private RectTransform startButton;

        [Tooltip("始めるボタンに対する HOW TO PLAY ボタンの大きさ（幅・高さの割合）。RECORDS ボタンと揃える。")]
        [SerializeField] private Vector2 buttonScale = new Vector2(0.48f, 0.45f);

        [Tooltip("始めるボタンと HOW TO PLAY ボタンの間（ピクセル）。")]
        [SerializeField] private float buttonGap = 8f;

        [Tooltip("HOW TO PLAY ボタンの横のずらし（始めるボタンの幅に対する割合。+ で右）。RECORDS ボタンと左右に並べる。")]
        [SerializeField] private float buttonShiftX = 0.26f;

        [Tooltip("HOW TO PLAY ボタンの文字の大きさ（いちばん大きいとき）。RECORDS ボタンと揃える。")]
        [SerializeField] private float buttonFontSize = 34f;

        [Tooltip("RECORDS と HOW TO PLAY の段の後ろに敷く暗い下敷きの色。ボタンのすき間から後ろの文字（カーブの表示）が覗かないように。")]
        [SerializeField] private Color buttonRowBackColor = new Color(0.01f, 0.01f, 0.04f, 1f);

        [Tooltip("下敷きの幅（始めるボタンの幅に対する割合）。RECORDS・HOW TO PLAY・CREDITS の3つの端から端まで。")]
        [SerializeField] private float buttonRowBackWidthScale = 1.25f;

        [Header("文字")]
        [Tooltip("1ページに載せる節の数。")]
        [Min(1)]
        [SerializeField] private int sectionsPerPage = 2;

        [Tooltip("節の見出し（【投げかた】など）の文字の大きさ。")]
        [SerializeField] private float headingSize = 44f;

        [Tooltip("本文の文字の大きさ。")]
        [SerializeField] private float bodySize = 34f;

        [Tooltip("本文の行間（TextMeshPro の行間の調整。0 で詰めない標準、大きいほど広い）。")]
        [SerializeField] private float bodyLineSpacing = 28f;

        [Tooltip("英語版（CB_LANG_EN）で、見出しと本文の文字の大きさに掛ける倍率（段階6。英語化）。英語は日本語より長く、1ページ目が枠からはみ出したため。日本語版には効かない。")]
        [Range(0.6f, 1f)]
        [SerializeField] private float englishTextScale = 0.88f;

        [Tooltip("英語版（CB_LANG_EN）の本文の行間（段階6。英語化）。日本語版は bodyLineSpacing のまま。")]
        [SerializeField] private float englishBodyLineSpacing = 18f;

        [Tooltip("本文の段落（改行）の間。")]
        [SerializeField] private float bodyParagraphSpacing = 18f;

        [Tooltip("見出しと本文の間（ピクセル）。")]
        [SerializeField] private float headingGap = 10f;

        [Tooltip("節と節の間（ピクセル）。")]
        [SerializeField] private float sectionGap = 44f;

        [Tooltip("本文の色。説明書らしく淡々と白で出す。")]
        [SerializeField] private Color bodyColor = new Color(0.92f, 0.92f, 0.96f);

        [Header("見た目")]
        [Tooltip("端のページで押せない ◀ ▶ の濃さ（0〜1）。")]
        [Range(0f, 1f)]
        [SerializeField] private float disabledButtonAlpha = 0.15f;

        [Tooltip("開くときの動きの時間（秒）。")]
        [SerializeField] private float openSeconds = 0.3f;

        [Tooltip("板の下に敷く濃い色（後ろが透けないように）。")]
        [SerializeField] private Color solidColor = new Color(0.01f, 0.01f, 0.04f, 0.95f);

        /// <summary>ネオンにするボタン1つぶん。</summary>
        private struct NeonButton
        {
            public Image frame;
            public Image glow;
            public TMP_Text label;
            public float offset;
        }

        private readonly List<NeonButton> _buttons = new List<NeonButton>();
        private CanvasGroup _group;
        private RectTransform _panel;
        private RectTransform _content;
        private ScrollRect _scroll;
        private TMP_Text _title;
        private TMP_Text _pageLabel;
        private TMP_Text _pageBottom;
        private Button _prev;
        private Button _next;
        private float _openTime;
        private bool _open;
        private int _page;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _open;

        /// <summary>今のページ（0から数える。確かめるとき用）。</summary>
        public int Page => _page;

        /// <summary>全部のページ数。</summary>
        public int PageCount => HowToPlayRules.PageCount(SectionCount, sectionsPerPage);

        private static int SectionCount => Mathf.Min(UIText.HowToPlayHeadings.Length, UIText.HowToPlayBodies.Length);

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
        }

        /// <summary>開く（いつも1ページ目から）。</summary>
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
            // 先に出してからページを組む（隠れたままだと並べ直しが効かない）
            _open = true;
            _openTime = Time.unscaledTime;
            SetVisible(true);
            ShowPage(0);
        }

        /// <summary>閉じる。</summary>
        public void Close()
        {
            _open = false;
            SetVisible(false);
        }

        /// <summary>次のページ。</summary>
        public void NextPage()
        {
            ShowPage(_page + 1);
        }

        /// <summary>前のページ。</summary>
        public void PrevPage()
        {
            ShowPage(_page - 1);
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

        /// <summary>タイトルの始めるボタンの下に HOW TO PLAY ボタンを作る（1回だけ。RECORDS ボタンと左右に並ぶ）。</summary>
        private void BuildTitleButton()
        {
            if (startButton == null)
            {
                return;
            }
            RectTransform rect = NeonUI.CreateRect(startButton.parent, "HowToPlayButton", startButton.anchorMin, startButton.anchorMax, Vector2.zero, Vector2.zero);
            rect.pivot = startButton.pivot;
            Vector2 size = startButton.rect.size;
            rect.sizeDelta = new Vector2(size.x * buttonScale.x, size.y * buttonScale.y);
            // 始めるボタンの下の端から、間をあけて下に置く（ピボットの位置を考えて合わせる）
            float startBottom = startButton.anchoredPosition.y - size.y * startButton.pivot.y;
            float height = rect.sizeDelta.y;
            rect.anchoredPosition = new Vector2(startButton.anchoredPosition.x + size.x * buttonShiftX, startBottom - buttonGap - height * (1f - rect.pivot.y));
            rect.SetSiblingIndex(startButton.GetSiblingIndex() + 1);
            CreateButton(rect, UIText.HowToPlay, buttonFontSize, Open);

            // 段の後ろの下敷き（ボタンの段の端から端まで。すき間から後ろのカーブの表示が覗くと「RECORDS STRAIGHT HOW TO PLAY」と読めてしまう）
            RectTransform back = NeonUI.CreateRect(startButton.parent, "TitleButtonRowBack", startButton.anchorMin, startButton.anchorMax, Vector2.zero, Vector2.zero);
            back.pivot = rect.pivot;
            // 画像なしの単色（パネルの画像は中が半透明で、後ろの文字が透けた）。角が見えないよう、ボタンの枠の内側に収める
            back.sizeDelta = new Vector2(size.x * buttonRowBackWidthScale - 24f, height - 8f);
            back.anchoredPosition = new Vector2(startButton.anchoredPosition.x, rect.anchoredPosition.y);
            NeonUI.CreateImage(back, null, buttonRowBackColor, false);
            // 始めるボタンと2つのボタンより後ろに描く
            back.SetSiblingIndex(startButton.GetSiblingIndex());
        }

        /// <summary>画面の枠・見出し・ボタンを作る（1回だけ）。ページの中身は <see cref="ShowPage"/> で入れ替える。</summary>
        private void Build()
        {
            // 後ろを暗くして、下を触れないようにする
            RectTransform dim = NeonUI.CreateStretch(root, "Dim");
            Image dimImage = NeonUI.CreateImage(dim, null, new Color(0f, 0f, 0f, 0.82f), false);
            dimImage.raycastTarget = true;

            _panel = NeonUI.CreateRect(root, "Panel", new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.965f), Vector2.zero, Vector2.zero);
            // 後ろのタイトルが透けて読みにくくならないよう、濃い板を敷いてから光る板を重ねる
            NeonUI.CreateImage(NeonUI.CreateRect(_panel, "Solid", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f)), null, solidColor, false);
            NeonUI.CreateImage(NeonUI.CreateStretch(_panel, "Plate"), skin.Panel, skin.PanelColor, true);
            Image frame = NeonUI.CreateImage(NeonUI.CreateRect(_panel, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12f, 12f)), skin.NeonFrame, Color.white, true);
            frame.pixelsPerUnitMultiplier = 1.6f;
            _buttons.Add(new NeonButton { frame = frame, offset = 0.45f });

            // 見出しとページ数
            _title = Bold(_panel, "Title", new Vector2(0.03f, 0.88f), new Vector2(0.6f, 0.985f), UIText.HowToPlay, 72f, TextAlignmentOptions.Left, true);
            _pageLabel = Bold(_panel, "PageTop", new Vector2(0.7f, 0.9f), new Vector2(0.97f, 0.97f), "", 34f, TextAlignmentOptions.Right, false);

            // 本文の入れ物（入りきらないときは上下になぞって・ホイールで動かせる）
            RectTransform viewport = NeonUI.CreateRect(_panel, "Viewport", new Vector2(0.03f, 0.14f), new Vector2(0.97f, 0.87f), Vector2.zero, Vector2.zero);
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
            layout.padding = new RectOffset(0, 0, 4, 12);
            var fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.content = _content;
            _scroll.viewport = viewport;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            // 下の段：◀ ページ ▶ と CLOSE
            _prev = CreateButton(NeonUI.CreateRect(_panel, "PrevButton", new Vector2(0.34f, 0.025f), new Vector2(0.43f, 0.11f), Vector2.zero, Vector2.zero), UIText.HowToPlayPrev, 40f, PrevPage);
            _pageBottom = Bold(_panel, "PageBottom", new Vector2(0.43f, 0.025f), new Vector2(0.57f, 0.11f), "", 36f, TextAlignmentOptions.Center, false);
            _next = CreateButton(NeonUI.CreateRect(_panel, "NextButton", new Vector2(0.57f, 0.025f), new Vector2(0.66f, 0.11f), Vector2.zero, Vector2.zero), UIText.HowToPlayNext, 40f, NextPage);
            CreateButton(NeonUI.CreateRect(_panel, "CloseButton", new Vector2(0.8f, 0.025f), new Vector2(0.97f, 0.11f), Vector2.zero, Vector2.zero), UIText.RecordsClose, 36f, Close);
        }

        /// <summary>ページを入れ替える。</summary>
        private void ShowPage(int page)
        {
            if (_content == null)
            {
                return;
            }
            _page = HowToPlayRules.ClampPage(page, SectionCount, sectionsPerPage);

            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                Destroy(_content.GetChild(i).gameObject);
            }

            HowToPlayRules.SectionRange(_page, SectionCount, sectionsPerPage, out int first, out int count);
            // 英語版は少し小さく、行の間も詰める（英語化。日本語版は今までどおり）
            float scale = GameLanguage.IsEnglish ? englishTextScale : 1f;
            float lineSpacing = GameLanguage.IsEnglish ? englishBodyLineSpacing : bodyLineSpacing;
            for (int i = 0; i < count; i++)
            {
                int s = first + i;
                TMP_Text heading = Paragraph("Heading" + (s + 1), UIText.HowToPlayHeadings[s], headingSize * scale, 0f, 0f, i == 0 ? 0f : sectionGap);
                heading.color = skin.GetAccent(s + 1);
                Paragraph("Body" + (s + 1), UIText.HowToPlayBodies[s], bodySize * scale, lineSpacing, bodyParagraphSpacing, headingGap);
            }

            string pageText = string.Format(UIText.HowToPlayPageFormat, _page + 1, PageCount);
            if (_pageLabel != null)
            {
                _pageLabel.text = pageText;
            }
            if (_pageBottom != null)
            {
                _pageBottom.text = pageText;
            }
            SetButtonEnabled(_prev, _page > 0);
            SetButtonEnabled(_next, _page < PageCount - 1);

            // 新しいページは上から読む
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _content.anchoredPosition = Vector2.zero;
            if (_scroll != null)
            {
                _scroll.StopMovement();
                _scroll.verticalNormalizedPosition = 1f;
            }
        }

        /// <summary>本文の1段落（折り返す普通の文字）。上に space ピクセルの間をあける。</summary>
        private TMP_Text Paragraph(string name, string text, float size, float lineSpacing, float paragraphSpacing, float space)
        {
            if (space > 0f)
            {
                RectTransform spacer = NeonUI.CreateRect(_content, name + "Space", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var le = spacer.gameObject.AddComponent<LayoutElement>();
                le.minHeight = space;
                le.preferredHeight = space;
            }
            RectTransform rect = NeonUI.CreateRect(_content, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = NeonUI.CreateText(rect, skin.RegularFont, skin.RegularPlainMaterial, size, bodyColor, TextAlignmentOptions.TopLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.lineSpacing = lineSpacing;
            label.paragraphSpacing = paragraphSpacing;
            label.text = text;
            return label;
        }

        private void SetButtonEnabled(Button button, bool on)
        {
            if (button == null)
            {
                return;
            }
            button.interactable = on;
            var group = button.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = button.gameObject.AddComponent<CanvasGroup>();
            }
            // 端のページでは押せないことが分かるように薄くする（点滅ではなく、ずっと薄いまま）
            group.alpha = on ? 1f : disabledButtonAlpha;
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
        }
    }
}
