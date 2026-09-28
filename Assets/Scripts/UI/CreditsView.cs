using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// クレジットの自動送りの計算（段階6）。MonoBehaviour に依らない（EditMode テストあり）。
    /// 送る位置（offset）は「中身のいちばん上から、見える範囲のいちばん上までの長さ」。0 がいちばん上。
    /// </summary>
    public static class CreditsScrollRules
    {
        /// <summary>
        /// 見出しを、見える範囲の上から fraction の位置に来させるための送る位置。中身の端を越えない（いちばん下より先へは送らない）。
        /// </summary>
        /// <param name="anchorFromContentTop">中身のいちばん上から見出しの上の端までの長さ。</param>
        public static float OffsetFor(float anchorFromContentTop, float viewportHeight, float fraction, float contentHeight)
        {
            float max = Mathf.Max(0f, contentHeight - viewportHeight);
            return Mathf.Clamp(anchorFromContentTop - viewportHeight * fraction, 0f, max);
        }

        /// <summary>
        /// 時刻 t の送る位置。目印と目印の間は一定の速さ（まっすぐ結ぶ）。最初の目印より前は最初の位置、最後の目印より後は最後の位置のまま。
        /// </summary>
        public static float TargetOffset(float t, IList<float> times, IList<float> offsets)
        {
            int n = Mathf.Min(times != null ? times.Count : 0, offsets != null ? offsets.Count : 0);
            if (n == 0)
            {
                return 0f;
            }
            if (t <= times[0])
            {
                return offsets[0];
            }
            for (int i = 0; i < n - 1; i++)
            {
                if (t < times[i + 1])
                {
                    float span = Mathf.Max(times[i + 1] - times[i], 0.0001f);
                    return Mathf.Lerp(offsets[i], offsets[i + 1], (t - times[i]) / span);
                }
            }
            return offsets[n - 1];
        }

        /// <summary>今の位置から目標へ、速さの上限を守って近づける（急に飛ばない）。</summary>
        public static float Step(float current, float target, float maxSpeed, float deltaTime)
        {
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, maxSpeed) * Mathf.Max(0f, deltaTime));
        }
    }

    /// <summary>
    /// クレジットの画面で、手で送り始めたことを知らせる（なぞる・ホイール・右の棒をつかむ）。自動送りを止めるのに使う。
    /// </summary>
    public class CreditsScrollInterrupt : MonoBehaviour, IBeginDragHandler, IScrollHandler, IPointerDownHandler
    {
        /// <summary>押しただけでも止めるか（右の棒）。なぞる場所では、押しただけでは止めない。</summary>
        public bool stopOnPointerDown;

        /// <summary>手で送り始めたときに呼ぶ。</summary>
        public System.Action Interrupted;

        public void OnBeginDrag(PointerEventData eventData)
        {
            Interrupted?.Invoke();
        }

        public void OnScroll(PointerEventData eventData)
        {
            Interrupted?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (stopOnPointerDown)
            {
                Interrupted?.Invoke();
            }
        }
    }

    /// <summary>
    /// クレジットの画面（段階6）。タイトルの CREDITS ボタンで開く。
    /// 見た目は遊び方・記録の画面と同じネオンの板。いちばん上に「produced by 夜中のBBQ」、その下に音源の作者（<see cref="UIText.CreditsSectionLines"/>）。
    /// 長いので、上下になぞって（ホイールで）送る。右の細い棒で、今どのあたりかが分かる。
    /// あいさつの声が流れている間は、映画の終わりのように自動でゆっくり送る（目印は <see cref="CreditsScrollCues"/>）。
    /// 手で送り始めたら（なぞる・ホイール・右の棒）、閉じたら、あいさつが止まったら（音・DJ を消した・最後まで流れた）、自動送りをやめる。
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

        [Header("自動送り（あいさつに合わせる）")]
        [Tooltip("目印の時刻と、見出しを来させる位置・速さの上限。空なら自動では送らない。")]
        [SerializeField] private CreditsScrollCues scrollCues;

        [Tooltip("あいさつの声を流す係（今の位置を読む）。空なら同じシーンから探す。")]
        [SerializeField] private CreditsSpeechPlayer speechPlayer;

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
        private TMP_Text _producerBottom;
        private float _openTime;
        private bool _open;
        private readonly Dictionary<CreditsAnchor, RectTransform> _anchors = new Dictionary<CreditsAnchor, RectTransform>();
        private readonly List<float> _cueTimes = new List<float>();
        private readonly List<float> _cueOffsets = new List<float>();
        private bool _autoActive;
        private bool _autoStarted;
        private float _autoOffset;
        private int _nextCue;
        private LayoutElement _endSpace;

        /// <summary>自動で送っているか（確かめるとき用）。</summary>
        public bool IsAutoScrolling => _autoActive && _autoStarted;

        /// <summary>自動送りを止めたわけ（確かめるとき用）。</summary>
        public string AutoStopReason { get; private set; } = "";

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
            if (speechPlayer == null)
            {
                speechPlayer = FindFirstObjectByType<CreditsSpeechPlayer>();
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
            FitEndSpace();
            ScrollPosition = 1f;
            // 自動送り：あいさつが流れ始めたら動く（流れなければ動かない）
            _autoActive = scrollCues != null && scrollCues.autoScroll;
            _autoStarted = false;
            _autoOffset = 0f;
            _nextCue = 0;
            AutoStopReason = "";
            if (!wasOpen)
            {
                Opened?.Invoke();
            }
        }

        /// <summary>閉じる。</summary>
        public void Close()
        {
            bool wasOpen = _open;
            StopAutoScroll("閉じた");
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

            // 手で送り始めたら自動送りをやめる（なぞる・ホイール・右の棒をつかむ）
            viewport.gameObject.AddComponent<CreditsScrollInterrupt>().Interrupted = () => StopAutoScroll("手で送った");
            var barInterrupt = bar.gameObject.AddComponent<CreditsScrollInterrupt>();
            barInterrupt.stopOnPointerDown = true;
            barInterrupt.Interrupted = () => StopAutoScroll("右の棒をつかんだ");

            FillContent();

            CreateButton(NeonUI.CreateRect(_panel, "CloseButton", new Vector2(0.8f, 0.025f), new Vector2(0.97f, 0.11f), Vector2.zero, Vector2.zero), UIText.RecordsClose, 36f, Close);
        }

        /// <summary>中身を並べる：名義 → 前書き → 節ごとに見出しと行 → 結び。</summary>
        private void FillContent()
        {
            // いちばん上：produced by 夜中のBBQ（タイトルの名義と同じ表記）
            _anchors[CreditsAnchor.Top] = (RectTransform)Line(_content, "ProducedBy", UIText.CreditPrefix, skin.RegularFont, skin.RegularPlainMaterial, 30f, noteColor, TextAlignmentOptions.Center, 0f).transform;
            _producer = Line(_content, "Producer", UIText.CreditName, skin.BoldFont, skin.BoldNeonMaterial, producerSize, new Color(1f, 0.6f, 0.3f), TextAlignmentOptions.Center, 0f);
            Space("IntroSpace", 24f);
            Line(_content, "Intro", UIText.CreditsIntro, skin.RegularFont, skin.RegularPlainMaterial, bodySize, bodyColor, TextAlignmentOptions.Center, bodyLineSpacing);

            int sections = Mathf.Min(UIText.CreditsHeadings.Length, UIText.CreditsSectionLines.Length);
            for (int s = 0; s < sections; s++)
            {
                Space("SectionSpace" + (s + 1), sectionGap);
                TMP_Text heading = Line(_content, "Heading" + (s + 1), UIText.CreditsHeadings[s], skin.BoldFont, skin.BoldNeonMaterial, headingSize, Color.white, TextAlignmentOptions.Left, 0f);
                _headings.Add(heading);
                // 自動送りの目印（見出しの文字で見分ける）
                string h = UIText.CreditsHeadings[s];
                if (h == "MUSIC") _anchors[CreditsAnchor.Music] = heading.rectTransform;
                else if (h == "SOUND EFFECTS") _anchors[CreditsAnchor.SoundEffects] = heading.rectTransform;
                else if (h == "VOICES") _anchors[CreditsAnchor.Voices] = heading.rectTransform;
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
                        string label = lines[i].Substring(0, tab);
                        RectTransform row = Row($"Row{s + 1}_{i + 1}", label, lines[i].Substring(tab + 1), skin.GetAccent(s * 3 + 1));
                        if (label == MoonOperatorLabel)
                        {
                            _anchors[CreditsAnchor.MoonOperator] = row;
                        }
                    }
                }
            }

            Space("OutroSpace", sectionGap);
            _anchors[CreditsAnchor.Sponsor] = (RectTransform)Line(_content, "Outro", UIText.CreditsOutro, skin.RegularFont, skin.RegularPlainMaterial, bodySize, bodyColor, TextAlignmentOptions.Center, bodyLineSpacing).transform;

            // いちばん下にも produced by 夜中のBBQ（あいさつの締めで読むので）
            Space("ClosingSpace", sectionGap);
            _anchors[CreditsAnchor.Closing] = (RectTransform)Line(_content, "ProducedByBottom", UIText.CreditPrefix, skin.RegularFont, skin.RegularPlainMaterial, 30f, noteColor, TextAlignmentOptions.Center, 0f).transform;
            _producerBottom = Line(_content, "ProducerBottom", UIText.CreditName, skin.BoldFont, skin.BoldNeonMaterial, producerSize, new Color(1f, 0.6f, 0.3f), TextAlignmentOptions.Center, 0f);

            // いちばん下の余白：締めの produced by が、自動送りで上から決めた位置まで上がれるように（映画の終わりのように）。高さは開くたびに画面に合わせる
            RectTransform end = NeonUI.CreateRect(_content, "EndSpace", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _endSpace = end.gameObject.AddComponent<LayoutElement>();
            _endSpace.minHeight = 0f;
            _endSpace.preferredHeight = 0f;
        }

        /// <summary>
        /// いちばん下の余白を、締めの produced by が見える範囲の上から <see cref="CreditsScrollCues.anchorFromTop"/> の位置まで上がれる高さにする。
        /// 見える範囲の高さは縦横比で変わるので、開くたびに合わせる。
        /// </summary>
        private void FitEndSpace()
        {
            if (_endSpace == null || !_anchors.TryGetValue(CreditsAnchor.Closing, out RectTransform closing) || closing == null)
            {
                return;
            }
            float fraction = scrollCues != null ? scrollCues.anchorFromTop : 0.33f;
            _endSpace.preferredHeight = 0f;
            _endSpace.minHeight = 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            // 締めの上の端から、中身のいちばん下まで（余白を入れる前）
            float below = ContentHeight - AnchorFromContentTop(closing);
            float need = Mathf.Max(0f, ViewportHeight * (1f - fraction) - below);
            _endSpace.preferredHeight = need;
            _endSpace.minHeight = need;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
        }

        /// <summary>月の無線士の行の札（あいさつの台本と同じく、効果音のいちばん最後）。</summary>
        private const string MoonOperatorLabel = "u_wxn5lzrjy3";

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
        private RectTransform Row(string name, string labelText, string bodyText, Color accent)
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
            return row;
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
            Color ember = Color.Lerp(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.2f, 0.12f), NeonUI.Breath(3.2f));
            NeonUI.SetNeonColor(_producer, ember, 0.5f);
            NeonUI.SetNeonColor(_producerBottom, ember, 0.5f);
            for (int i = 0; i < _headings.Count; i++)
            {
                NeonUI.SetNeonColor(_headings[i], NeonUI.Hue(now * 0.06f + i * 0.17f, 0.75f), 0.4f);
            }

            UpdateAutoScroll();
        }

        // ================= 自動送り =================

        /// <summary>自動送りをやめる（手で送った・閉じた・あいさつが止まった）。やめたら、その画面を開いている間はもう動かさない。</summary>
        public void StopAutoScroll(string reason)
        {
            if (!_autoActive)
            {
                return;
            }
            _autoActive = false;
            AutoStopReason = reason;
            if (_autoStarted)
            {
                SoundPlayer.Instance?.Record("クレジットの自動送り：やめた（" + reason + "）");
            }
        }

        private void UpdateAutoScroll()
        {
            if (!_autoActive || scrollCues == null || speechPlayer == null || _content == null || _scroll == null)
            {
                return;
            }
            if (!speechPlayer.IsSpeaking)
            {
                // 流れ始める前は待つ。流れ始めたあとで止まった（最後まで流れた・音か DJ を消した）ら、そこでやめる（頭には戻らない）
                if (_autoStarted)
                {
                    StopAutoScroll(speechPlayer.IsPlaying ? "あいさつを小さくし始めた" : "あいさつが止まった");
                }
                return;
            }

            if (!_autoStarted)
            {
                _autoStarted = true;
                _autoOffset = Mathf.Max(0f, _content.anchoredPosition.y);
                SoundPlayer.Instance?.Record("クレジットの自動送り：始めた");
            }

            // 目印の送る位置（画面の大きさで変わるので毎回出す。数は少ない）
            float viewportHeight = ViewportHeight;
            float contentHeight = ContentHeight;
            _cueTimes.Clear();
            _cueOffsets.Clear();
            foreach (CreditsCue cue in scrollCues.cues)
            {
                if (cue == null || !_anchors.TryGetValue(cue.anchor, out RectTransform anchor) || anchor == null)
                {
                    continue;
                }
                _cueTimes.Add(cue.time);
                _cueOffsets.Add(CreditsScrollRules.OffsetFor(AnchorFromContentTop(anchor), viewportHeight, scrollCues.anchorFromTop, contentHeight));
            }

            float t = speechPlayer.PlaybackTime;
            float target = CreditsScrollRules.TargetOffset(t, _cueTimes, _cueOffsets);
            _autoOffset = CreditsScrollRules.Step(_autoOffset, target, scrollCues.maxSpeed, Time.unscaledDeltaTime);
            _scroll.StopMovement();
            Vector2 position = _content.anchoredPosition;
            position.y = _autoOffset;
            _content.anchoredPosition = position;

            // 目印の時刻を過ぎたら、その見出しがどこにあるかを記録に残す（確かめ用）
            while (_nextCue < scrollCues.cues.Count && scrollCues.cues[_nextCue] != null && t >= scrollCues.cues[_nextCue].time)
            {
                CreditsCue cue = scrollCues.cues[_nextCue];
                float at = _anchors.TryGetValue(cue.anchor, out RectTransform a) && a != null ? AnchorFromTop(cue.anchor) : -1f;
                SoundPlayer.Instance?.Record($"クレジットの自動送り：{cue.anchor}（{cue.note}）{cue.time:F2}秒 → 今 {t:F2}秒・見出しは見える範囲の上から {at:F2}");
                _nextCue++;
            }
        }

        /// <summary>中身のいちばん上から、その目印の上の端までの長さ。</summary>
        private float AnchorFromContentTop(RectTransform anchor)
        {
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            float top = _content.InverseTransformPoint(corners[1]).y;
            return _content.rect.yMax - top;
        }

        /// <summary>その目印の上の端が、見える範囲の上からどれだけの位置にあるか（0 がいちばん上、1 がいちばん下。確かめるとき用）。</summary>
        public float AnchorFromTop(CreditsAnchor anchor)
        {
            if (_scroll == null || !_anchors.TryGetValue(anchor, out RectTransform rect) || rect == null)
            {
                return -1f;
            }
            var viewport = (RectTransform)_scroll.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float top = viewport.InverseTransformPoint(corners[1]).y;
            return (viewport.rect.yMax - top) / Mathf.Max(viewport.rect.height, 1f);
        }
    }
}
