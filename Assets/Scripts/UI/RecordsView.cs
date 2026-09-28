using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 記録の画面（段階6）。タイトルの RECORDS ボタンで開く。
    ///   ・左：合計点の自己ベスト（RANK・日時）、通算（ゲーム数・ストライク・スペア・ガター）、レーンごとの自己ベスト
    ///   ・右：最近の成績（1ゲーム1行。得点板のように10レーンの升目を並べ、ストライク・スペア・ガターが見分けられる）
    ///   ・下：説明書口調の注意書き、CLEAR（確かめてから消す）、CLOSE
    /// 中身はすべてコードで組み立てる（開くたびに作り直す）。見た目はネオン。点滅はさせない。
    /// 記録は読むだけ（CLEAR のときだけ消す）。得点の計算には関わらない。
    /// </summary>
    public class RecordsView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("レーン名とレーンの数を読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("投球が始まったら閉じる（通しの確認のとき）。空なら同じシーンから探す。")]
        [SerializeField] private ThrowSequencer sequencer;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("記録の画面を置く入れ物（画面全体）。")]
        [SerializeField] private RectTransform root;

        [Header("タイトルの RECORDS ボタン")]
        [Tooltip("RECORDS ボタンを置く入れ物（タイトル画面）。空ならボタンを作らない。")]
        [SerializeField] private RectTransform titleRoot;

        [Tooltip("位置を合わせる相手（タイトルの始めるボタン）。RECORDS ボタンはこの下に置く。")]
        [SerializeField] private RectTransform startButton;

        [Tooltip("始めるボタンに対する RECORDS ボタンの大きさ（幅・高さの割合）。")]
        [SerializeField] private Vector2 buttonScale = new Vector2(0.55f, 0.55f);

        [Tooltip("始めるボタンと RECORDS ボタンの間（ピクセル）。")]
        [SerializeField] private float buttonGap = 10f;

        [Header("見た目")]
        [Tooltip("最近の成績の1行の高さ（ピクセル）。")]
        [SerializeField] private float recentRowHeight = 64f;

        [Tooltip("開くときの動きの時間（秒）。")]
        [SerializeField] private float openSeconds = 0.3f;

        [Tooltip("板の下に敷く濃い色（後ろが透けないように）。")]
        [SerializeField] private Color solidColor = new Color(0.01f, 0.01f, 0.04f, 0.95f);

        [Tooltip("ガターの印の色。")]
        [SerializeField] private Color gutterColor = new Color(1f, 0.35f, 0.4f);

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
        private RectTransform _confirm;
        private TMP_Text _title;
        private TMP_Text _bestLabel;
        private float _openTime;
        private bool _open;

        /// <summary>開いているか。</summary>
        public bool IsOpen => _open;

        /// <summary>確認の窓（CLEAR）を出しているか。</summary>
        public bool IsConfirming => _confirm != null && _confirm.gameObject.activeSelf;

        /// <summary>今出している最近の成績の行数（確かめるとき用）。</summary>
        public int RecentRowCount { get; private set; }

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
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

        /// <summary>開く（中身を今の記録で作り直す）。</summary>
        public void Open()
        {
            if (root == null || skin == null)
            {
                return;
            }
            Rebuild();
            _open = true;
            _openTime = Time.unscaledTime;
            SetVisible(true);
        }

        /// <summary>閉じる。</summary>
        public void Close()
        {
            _open = false;
            SetVisible(false);
        }

        /// <summary>CLEAR：確認の窓を出す。</summary>
        public void RequestClear()
        {
            if (_confirm != null)
            {
                _confirm.gameObject.SetActive(true);
            }
        }

        /// <summary>確認の窓の YES：記録をすべて消して、中身を作り直す。</summary>
        public void ConfirmClear()
        {
            RecordStore.Clear();
            Rebuild();
        }

        /// <summary>確認の窓の NO：何もせずに窓を閉じる。</summary>
        public void CancelClear()
        {
            if (_confirm != null)
            {
                _confirm.gameObject.SetActive(false);
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

        /// <summary>タイトルの始めるボタンの下に RECORDS ボタンを作る（1回だけ）。</summary>
        private void BuildTitleButton()
        {
            if (titleRoot == null || startButton == null)
            {
                return;
            }
            RectTransform rect = NeonUI.CreateRect(startButton.parent, "RecordsButton", startButton.anchorMin, startButton.anchorMax, Vector2.zero, Vector2.zero);
            rect.pivot = startButton.pivot;
            Vector2 size = startButton.rect.size;
            rect.sizeDelta = new Vector2(size.x * buttonScale.x, size.y * buttonScale.y);
            // 始めるボタンの下の端から、間をあけて下に置く（ピボットの位置を考えて合わせる）
            float startBottom = startButton.anchoredPosition.y - size.y * startButton.pivot.y;
            float height = rect.sizeDelta.y;
            rect.anchoredPosition = new Vector2(startButton.anchoredPosition.x, startBottom - buttonGap - height * (1f - rect.pivot.y));
            rect.SetSiblingIndex(startButton.GetSiblingIndex() + 1);
            CreateButton(rect, UIText.Records, 44f, Open);
        }

        /// <summary>画面の中身を今の記録で作り直す。</summary>
        private void Rebuild()
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
            // タイトルの RECORDS ボタンは残す（root の外にある）
            _buttons.RemoveAll(b => b.frame == null || !b.frame.transform.IsChildOf(titleRoot != null ? titleRoot : transform));
            _headings.Clear();

            int laneCount = gameManager != null ? gameManager.LaneCount : 10;
            RecordBook book = RecordStore.Load(laneCount);
            RecordRules.Normalize(book, laneCount);
            RecordKeeper keeper = RecordKeeper.Instance;
            int keep = keeper != null ? keeper.KeepRecentGames : 20;

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
            _buttons.Add(new NeonButton { frame = frame, offset = 0.3f });

            // 見出しと一言
            _title = Bold(_panel, "Title", new Vector2(0.02f, 0.885f), new Vector2(0.3f, 0.99f), UIText.Records, 72f, TextAlignmentOptions.Left, true);
            Regular(_panel, "Intro", new Vector2(0.32f, 0.9f), new Vector2(0.98f, 0.975f), UIText.RecordsIntro, 30f, TextAlignmentOptions.Right);

            BuildBest(book);
            BuildTotals(book);
            BuildLaneBests(book);
            BuildRecent(book, keep);

            // 注意書きとボタン
            Regular(_panel, "StorageNote", new Vector2(0.02f, 0.06f), new Vector2(0.64f, 0.11f), UIText.RecordsStorageNote, 24f, TextAlignmentOptions.Left);
            Regular(_panel, "RuleNote", new Vector2(0.02f, 0.015f), new Vector2(0.64f, 0.06f), UIText.RecordsRuleNote, 24f, TextAlignmentOptions.Left);
            CreateButton(NeonUI.CreateRect(_panel, "ClearButton", new Vector2(0.67f, 0.025f), new Vector2(0.81f, 0.1f), Vector2.zero, Vector2.zero), UIText.RecordsClear, 36f, RequestClear);
            CreateButton(NeonUI.CreateRect(_panel, "CloseButton", new Vector2(0.83f, 0.025f), new Vector2(0.98f, 0.1f), Vector2.zero, Vector2.zero), UIText.RecordsClose, 36f, Close);

            BuildConfirm();
        }

        /// <summary>左上：合計点の自己ベスト。</summary>
        private void BuildBest(RecordBook book)
        {
            Heading(_panel, "BestHeading", new Vector2(0.02f, 0.81f), new Vector2(0.33f, 0.87f), UIText.RecordsBestScore);
            bool has = book.bestTotal >= 0 && book.games > 0;
            _bestLabel = Bold(_panel, "BestTotal", new Vector2(0.02f, 0.69f), new Vector2(0.2f, 0.81f), has ? book.bestTotal.ToString() : UIText.NotPlayed, 96f, TextAlignmentOptions.Left, true);
            _bestLabel.color = skin.Gold;
            if (has)
            {
                Bold(_panel, "BestRank", new Vector2(0.2f, 0.74f), new Vector2(0.33f, 0.81f), string.Format(UIText.RankFormat, book.bestRank), 44f, TextAlignmentOptions.Right, false);
                Bold(_panel, "BestDate", new Vector2(0.2f, 0.69f), new Vector2(0.33f, 0.74f), book.bestPlayedAt, 26f, TextAlignmentOptions.Right, false);
            }
        }

        /// <summary>左の中ほど：通算。</summary>
        private void BuildTotals(RecordBook book)
        {
            Heading(_panel, "TotalsHeading", new Vector2(0.02f, 0.615f), new Vector2(0.33f, 0.675f), UIText.RecordsTotals);
            string[] names = { UIText.RecordsGames, UIText.RecordsStrikes, UIText.RecordsSpares, UIText.RecordsGutters };
            int[] values = { book.games, book.strikes, book.spares, book.gutters };
            for (int i = 0; i < names.Length; i++)
            {
                float x0 = 0.02f + (i % 2) * 0.16f;
                float y1 = 0.61f - (i / 2) * 0.055f;
                Bold(_panel, names[i], new Vector2(x0, y1 - 0.05f), new Vector2(x0 + 0.1f, y1), names[i], 26f, TextAlignmentOptions.Left, false);
                TMP_Text value = Bold(_panel, names[i] + "Value", new Vector2(x0 + 0.09f, y1 - 0.05f), new Vector2(x0 + 0.15f, y1), values[i].ToString(), 34f, TextAlignmentOptions.Right, true);
                value.color = i == 1 || i == 2 ? skin.Gold : i == 3 ? gutterColor : Color.white;
            }
        }

        /// <summary>左下：レーンごとの自己ベスト。</summary>
        private void BuildLaneBests(RecordBook book)
        {
            Heading(_panel, "LaneBestHeading", new Vector2(0.02f, 0.44f), new Vector2(0.33f, 0.5f), UIText.RecordsLaneBest);
            int count = book.laneBests.Length;
            float top = 0.44f;
            float bottom = 0.125f;
            float h = (top - bottom) / Mathf.Max(count, 1);
            for (int i = 0; i < count; i++)
            {
                float y1 = top - i * h;
                RectTransform row = NeonUI.CreateRect(_panel, $"LaneBest{i + 1:00}", new Vector2(0.02f, y1 - h), new Vector2(0.33f, y1), Vector2.zero, Vector2.zero);
                Color accent = skin.GetAccent(i + 1);
                TMP_Text number = Bold(row, "Number", new Vector2(0f, 0f), new Vector2(0.1f, 1f), (i + 1).ToString("00"), 26f, TextAlignmentOptions.Left, true);
                number.color = accent;
                Bold(row, "Name", new Vector2(0.11f, 0f), new Vector2(0.8f, 1f), LaneName(i), 24f, TextAlignmentOptions.Left, false);
                int best = book.laneBests[i];
                TMP_Text score = Bold(row, "Score", new Vector2(0.8f, 0f), new Vector2(1f, 1f), best >= 0 ? best.ToString() : UIText.NotPlayed, 26f, TextAlignmentOptions.Right, false);
                if (best >= 30)
                {
                    score.color = skin.Gold;
                }
            }
        }

        /// <summary>右：最近の成績（新しいものが上）。</summary>
        private void BuildRecent(RecordBook book, int keep)
        {
            float x0 = 0.35f;
            float x1 = 0.98f;
            Heading(_panel, "RecentHeading", new Vector2(x0, 0.81f), new Vector2(0.8f, 0.87f), UIText.RecordsRecent);
            Bold(_panel, "RecentCount", new Vector2(0.8f, 0.81f), new Vector2(x1, 0.87f), string.Format(UIText.RecordsRecentCountFormat, book.recent.Count, keep), 28f, TextAlignmentOptions.Right, false);

            // 列の見出し
            RectTransform header = NeonUI.CreateRect(_panel, "RecentHeader", new Vector2(x0, 0.765f), new Vector2(x1, 0.81f), Vector2.zero, new Vector2(-24f, 0f));
            int laneCount = book.laneBests.Length;
            HeaderCell(header, UIText.RecordsDate, 0.005f, DateEnd, TextAlignmentOptions.Left);
            HeaderCell(header, UIText.RecordsTotals, DateEnd, TotalEnd, TextAlignmentOptions.Right);
            HeaderCell(header, UIText.RecordsRank, TotalEnd, CellStart, TextAlignmentOptions.Center);
            for (int i = 0; i < laneCount; i++)
            {
                float a = CellStart + (1f - CellStart) * i / laneCount;
                float b = CellStart + (1f - CellStart) * (i + 1) / laneCount;
                TMP_Text t = HeaderCell(header, (i + 1).ToString("00"), a, b, TextAlignmentOptions.Center);
                t.color = skin.GetAccent(i + 1);
            }

            // 流れる入れ物（ホイール・指で上下に動かせる）
            RectTransform viewport = NeonUI.CreateRect(_panel, "RecentViewport", new Vector2(x0, 0.125f), new Vector2(x1, 0.765f), Vector2.zero, Vector2.zero);
            Image viewportImage = NeonUI.CreateImage(viewport, null, new Color(0f, 0f, 0f, 0.001f), false);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = NeonUI.CreateRect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(-24f, book.recent.Count * recentRowHeight);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = recentRowHeight * 0.5f;

            RecentRowCount = book.recent.Count;
            for (int g = 0; g < book.recent.Count; g++)
            {
                BuildRecentRow(content, book.recent[g], g, laneCount);
            }

            if (book.recent.Count == 0)
            {
                Regular(viewport, "Empty", new Vector2(0f, 0.8f), new Vector2(1f, 0.95f), UIText.RecordsEmpty, 30f, TextAlignmentOptions.Center);
            }
        }

        /// <summary>行の中の列の位置（行の幅に対する割合）。日時・合計・RANK のあとに升目が並ぶ。</summary>
        private const float DateEnd = 0.14f;
        private const float TotalEnd = 0.2f;
        private const float CellStart = 0.27f;

        /// <summary>最近の成績の1行。</summary>
        private void BuildRecentRow(RectTransform content, GameRecord game, int index, int laneCount)
        {
            RectTransform row = NeonUI.CreateRect(content, $"Game{index + 1:00}", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -index * recentRowHeight);
            row.sizeDelta = new Vector2(0f, recentRowHeight - 6f);
            NeonUI.CreateImage(NeonUI.CreateStretch(row, "Plate"), skin.Panel, new Color(1f, 1f, 1f, index % 2 == 0 ? 0.06f : 0.03f), true);

            // 日時は2段（日付・時刻）
            string at = game.playedAt ?? "";
            int space = at.IndexOf(' ');
            string date = space > 0 ? at.Substring(0, space) + "\n" + at.Substring(space + 1) : at;
            TMP_Text dateLabel = Bold(row, "Date", new Vector2(0.005f, 0.05f), new Vector2(DateEnd, 0.95f), date, 20f, TextAlignmentOptions.Left, false);
            dateLabel.textWrappingMode = TextWrappingModes.Normal;
            dateLabel.lineSpacing = -10f;
            TMP_Text total = Bold(row, "Total", new Vector2(DateEnd, 0f), new Vector2(TotalEnd, 1f), game.total.ToString(), 34f, TextAlignmentOptions.Right, true);
            total.color = skin.Gold;
            Bold(row, "Rank", new Vector2(TotalEnd, 0f), new Vector2(CellStart, 1f), game.rank ?? "", 30f, TextAlignmentOptions.Center, true);

            LaneRecord[] lanes = game.lanes ?? new LaneRecord[0];
            for (int i = 0; i < laneCount; i++)
            {
                float a = CellStart + (1f - CellStart) * i / laneCount;
                float b = CellStart + (1f - CellStart) * (i + 1) / laneCount;
                RectTransform cell = NeonUI.CreateRect(row, $"Lane{i + 1:00}", new Vector2(a, 0f), new Vector2(b, 1f), Vector2.zero, new Vector2(-6f, -4f));
                Image cellFrame = NeonUI.CreateImage(NeonUI.CreateRect(cell, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(4f, 4f)), skin.NeonFrame, NeonUI.WithAlpha(skin.GetAccent(i + 1), 0.55f), true);
                cellFrame.pixelsPerUnitMultiplier = 3.2f;

                LaneRecord lane = i < lanes.Length ? lanes[i] : null;
                string marks = Marks(lane);
                bool gold = lane != null && (lane.kind == RecordRules.KindStrike || lane.kind == RecordRules.KindSpare);
                bool gutter = lane != null && lane.kind == RecordRules.KindGutter;
                TMP_Text markLabel = Bold(cell, "Marks", new Vector2(0.05f, 0.5f), new Vector2(0.95f, 0.98f), marks, 22f, TextAlignmentOptions.Center, false);
                markLabel.richText = true;
                if (gold) markLabel.color = skin.Gold;
                TMP_Text scoreLabel = Bold(cell, "Score", new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.52f), lane != null ? lane.score.ToString() : UIText.NotPlayed, 24f, TextAlignmentOptions.Center, false);
                if (gold) scoreLabel.color = skin.Gold;
                if (gutter) scoreLabel.color = gutterColor;
            }
        }

        /// <summary>升目の上の段に出す印。ストライクは X、スペアは「本数 /」、倒した本数 0 の投は G（色付き）。</summary>
        private string Marks(LaneRecord lane)
        {
            if (lane == null)
            {
                return UIText.NotPlayed;
            }
            if (lane.kind == RecordRules.KindStrike)
            {
                return UIText.StrikeMark;
            }
            string first = Throw(lane.first);
            string second = lane.kind == RecordRules.KindSpare ? UIText.SpareMark
                : lane.throws >= 2 ? Throw(lane.second)
                : UIText.NotPlayed;
            return first + " " + second;
        }

        private string Throw(int fallen)
        {
            return fallen == 0
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(gutterColor)}>{UIText.GutterMark}</color>"
                : fallen.ToString();
        }

        /// <summary>確認の窓（CLEAR）。はじめは隠しておく。</summary>
        private void BuildConfirm()
        {
            _confirm = NeonUI.CreateStretch(root, "Confirm");
            Image dim = NeonUI.CreateImage(_confirm, null, new Color(0f, 0f, 0f, 0.7f), false);
            dim.raycastTarget = true;

            RectTransform box = NeonUI.CreateRect(_confirm, "Box", new Vector2(0.28f, 0.33f), new Vector2(0.72f, 0.67f), Vector2.zero, Vector2.zero);
            NeonUI.CreateImage(NeonUI.CreateRect(box, "Solid", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f)), null, solidColor, false);
            NeonUI.CreateImage(NeonUI.CreateStretch(box, "Plate"), skin.Panel, new Color(0.02f, 0.02f, 0.06f, 0.96f), true);
            Image frame = NeonUI.CreateImage(NeonUI.CreateRect(box, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12f, 12f)), skin.NeonFrame, Color.white, true);
            frame.pixelsPerUnitMultiplier = 1.6f;
            _buttons.Add(new NeonButton { frame = frame, offset = 0.7f });

            Heading(box, "Title", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.94f), UIText.RecordsClear);
            TMP_Text message = Regular(box, "Message", new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.74f), UIText.RecordsClearConfirm, 30f, TextAlignmentOptions.Center);
            message.textWrappingMode = TextWrappingModes.Normal;
            CreateButton(NeonUI.CreateRect(box, "Yes", new Vector2(0.1f, 0.08f), new Vector2(0.45f, 0.28f), Vector2.zero, Vector2.zero), UIText.RecordsYes, 36f, ConfirmClear);
            CreateButton(NeonUI.CreateRect(box, "No", new Vector2(0.55f, 0.08f), new Vector2(0.9f, 0.28f), Vector2.zero, Vector2.zero), UIText.RecordsNo, 36f, CancelClear);
            _confirm.gameObject.SetActive(false);
        }

        // ================= 部品 =================

        private string LaneName(int index)
        {
            LaneData data = gameManager != null ? gameManager.GetLaneData(index) : null;
            return data != null ? data.LaneName : string.Empty;
        }

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

        /// <summary>普通の文字（日本語の説明）。淡々と出す。</summary>
        private TMP_Text Regular(RectTransform parent, string name, Vector2 min, Vector2 max, string text, float size, TextAlignmentOptions alignment)
        {
            RectTransform rect = NeonUI.CreateRect(parent, name, min, max, Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = NeonUI.CreateText(rect, skin.RegularFont, skin.RegularPlainMaterial, size, new Color(0.9f, 0.9f, 0.95f), alignment);
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = size;
            label.text = text;
            return label;
        }

        /// <summary>見出し（光る太字。色はゆっくり流れる）。</summary>
        private TMP_Text Heading(RectTransform parent, string name, Vector2 min, Vector2 max, string text)
        {
            TMP_Text label = Bold(parent, name, min, max, text, 36f, TextAlignmentOptions.Left, true);
            _headings.Add(label);
            return label;
        }

        private TMP_Text HeaderCell(RectTransform parent, string text, float a, float b, TextAlignmentOptions alignment)
        {
            return Bold(parent, text, new Vector2(a, 0f), new Vector2(b, 1f), text, 22f, alignment, false);
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
            NeonUI.SetNeonColor(_bestLabel, skin.Gold, 0.45f + 0.3f * NeonUI.Breath(2.4f));
            for (int i = 0; i < _headings.Count; i++)
            {
                NeonUI.SetNeonColor(_headings[i], NeonUI.Hue(now * 0.06f + i * 0.13f, 0.75f), 0.4f);
            }
        }
    }
}
