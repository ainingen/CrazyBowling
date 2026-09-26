using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// レーンごとの得点表と合計。ネオンの電光掲示板（段階6）。
    /// 升目はレーンの本数に合わせて実行時に作る。
    ///
    /// ・今のレーンの升目は、そのレーンの差し色でゆっくり呼吸し、光の帯が横切る
    /// ・遊び終わった升目は、そのレーンの差し色で縁取る（ストライク・スペアは金）
    /// ・得点が入ると数字が回って増える。合計も数え上がり、「+30」のような数字が浮かぶ
    ///
    /// 得点は GameManager から読むだけ。計算には一切関わらない。
    /// ★ぱかぱか点滅させない。明るさはゆっくり呼吸させるか、色を流すだけ。
    /// </summary>
    public class ScoreBoardView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("得点を読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("升目を並べる入れ物。Horizontal Layout Group を付けておく。")]
        [SerializeField] private RectTransform cellParent;

        [Tooltip("合計を出すテキスト。")]
        [SerializeField] private TMP_Text totalLabel;

        [Tooltip("升目の文字に使うフォント（見た目の材料が無いとき）。")]
        [SerializeField] private TMP_FontAsset font;

        [Tooltip("見た目の材料（段階6）。空なら升目は地味なまま。")]
        [SerializeField] private UISkin skin;

        [Tooltip("得点板全体を囲むネオンの枠（色が流れる）。空でもよい。")]
        [SerializeField] private Image boardFrame;

        [Tooltip("合計が増えたときに「+30」を浮かべる入れ物。空なら浮かべない。")]
        [SerializeField] private RectTransform gainParent;

        [Header("升目の見た目")]
        [Tooltip("レーン番号の文字の大きさ。")]
        [SerializeField] private float numberFontSize = 22f;

        [Tooltip("得点の文字の大きさ。")]
        [SerializeField] private float scoreFontSize = 44f;

        [Tooltip("まだ遊んでいないレーンの升目の色。")]
        [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.35f);

        [Tooltip("遊び終わったレーンの升目の色。")]
        [SerializeField] private Color playedColor = new Color(0f, 0f, 0f, 0.55f);

        [Tooltip("今いるレーンの升目の色。")]
        [SerializeField] private Color currentColor = new Color(0.20f, 0.45f, 0.75f, 0.85f);

        [Tooltip("ストライクとスペアの文字の色。")]
        [SerializeField] private Color highlightTextColor = new Color(1f, 0.85f, 0.25f);

        [Tooltip("ふつうの文字の色。")]
        [SerializeField] private Color normalTextColor = Color.white;

        [Header("動き")]
        [Tooltip("得点が回って増えるのにかける時間（秒）。")]
        [SerializeField] private float countUpSeconds = 0.7f;

        [Tooltip("合計が数え上がるのにかける時間（秒）。")]
        [SerializeField] private float totalCountUpSeconds = 1.1f;

        [Tooltip("今のレーンの升目の呼吸の周期（秒）。")]
        [SerializeField] private float currentBreathSeconds = 1.8f;

        [Tooltip("光の帯が升目を横切る間隔（秒）。")]
        [SerializeField] private float sweepSeconds = 2.2f;

        [Tooltip("得点板の枠の色が一周する時間（秒）。")]
        [SerializeField] private float frameHueSeconds = 10f;

        [Tooltip("「+30」が浮かんで消えるまでの時間（秒）。")]
        [SerializeField] private float gainSeconds = 1.2f;

        [Tooltip("得点板の枠の上を走る光の粒が一周する時間（秒）。")]
        [SerializeField] private float cometLapSeconds = 6f;

        [Header("文字")]
        [Tooltip("まだ遊んでいないレーンに出す文字。")]
        [SerializeField] private string emptyText = UIText.NotPlayed;

        [Tooltip("ストライクに出す文字。")]
        [SerializeField] private string strikeText = UIText.StrikeMark;

        [Tooltip("スペアに出す文字。")]
        [SerializeField] private string spareText = UIText.SpareMark;

        [Tooltip("合計の書き方。{0} が合計、{1} が満点。")]
        [SerializeField] private string totalFormat = UIText.TotalFormat;

        /// <summary>升目1つぶんの部品と、数え上げの途中の値。</summary>
        private class Cell
        {
            public Image background;
            public Image frame;
            public Image glow;
            public RectTransform band;
            public TMP_Text number;
            public TMP_Text score;
            public int target = -1;
            public float from;
            public float startTime = -10f;
        }

        [System.NonSerialized] private Cell[] _cells;

        /// <summary>合計の数え上げ。</summary>
        private int _totalTarget;
        private float _totalFrom;
        private float _totalStart = -10f;

        /// <summary>得点板の枠の上を走る光の粒（2つ。反対向きに走る）。</summary>
        [System.NonSerialized] private Image[] _comets;

        /// <summary>浮かべている「+30」。</summary>
        private TMP_Text _gainLabel;
        private float _gainStart = -10f;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
        }

        private void LateUpdate()
        {
            if (gameManager == null || cellParent == null)
            {
                return;
            }

            EnsureCells(gameManager.LaneCount);
            UpdateCells();
            UpdateTotal();
            UpdateBoardFrame();
        }

        /// <summary>レーンの本数ぶんの升目を用意する。数が合っていれば何もしない。</summary>
        private void EnsureCells(int count)
        {
            if (_cells != null && _cells.Length == count)
            {
                return;
            }

            for (int i = cellParent.childCount - 1; i >= 0; i--)
            {
                Destroy(cellParent.GetChild(i).gameObject);
            }

            _cells = new Cell[Mathf.Max(0, count)];
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = BuildCell(i + 1);
            }
        }

        /// <summary>升目を1つ組み立てる。</summary>
        private Cell BuildCell(int laneNumber)
        {
            var root = new GameObject($"Cell{laneNumber:00}", typeof(RectTransform));
            root.transform.SetParent(cellParent, false);
            var rootRect = (RectTransform)root.transform;

            var layout = root.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;

            var cell = new Cell();

            if (skin != null)
            {
                // 光の玉（今のレーンのときだけ見える）→ 暗い板 → 光の帯 → ネオンの枠 → 文字 の順に重ねる
                RectTransform glowRect = NeonUI.CreateRect(rootRect, "Glow", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(40f, 40f));
                cell.glow = NeonUI.CreateImage(glowRect, skin.Glow, Color.clear, false);

                RectTransform plate = NeonUI.CreateRect(rootRect, "Plate", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6f, -6f));
                cell.background = NeonUI.CreateImage(plate, skin.Panel, emptyColor, true);
                plate.gameObject.AddComponent<RectMask2D>();

                cell.band = NeonUI.CreateRect(plate, "Band", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(70f, 0f));
                NeonUI.CreateImage(cell.band, skin.Band, new Color(1f, 1f, 1f, 0f), false);

                RectTransform frameRect = NeonUI.CreateRect(rootRect, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(10f, 10f));
                cell.frame = NeonUI.CreateImage(frameRect, skin.NeonFrame, new Color(1f, 1f, 1f, 0.35f), true);
                cell.frame.pixelsPerUnitMultiplier = 2.2f;
            }
            else
            {
                cell.background = root.AddComponent<Image>();
                cell.background.color = emptyColor;
                cell.background.raycastTarget = false;
            }

            cell.number = BuildLabel(rootRect, "Number", numberFontSize, new Vector2(0f, 0.6f), new Vector2(1f, 0.98f), false);
            cell.number.text = laneNumber.ToString();

            cell.score = BuildLabel(rootRect, "Score", scoreFontSize, new Vector2(0f, 0.02f), new Vector2(1f, 0.66f), true);
            cell.score.text = emptyText;

            return cell;
        }

        /// <summary>升目の中の文字を1つ作る。</summary>
        private TMP_Text BuildLabel(RectTransform parent, string name, float size,
            Vector2 anchorMin, Vector2 anchorMax, bool neon)
        {
            RectTransform rect = NeonUI.CreateRect(parent, name, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            TMP_FontAsset labelFont = skin != null && skin.BoldFont != null ? skin.BoldFont : font;
            Material material = skin != null ? (neon ? skin.BoldNeonMaterial : skin.BoldPlainMaterial) : null;
            TextMeshProUGUI label = NeonUI.CreateText(rect, labelFont, material, size, normalTextColor, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = size * 0.5f;
            label.fontSizeMax = size;
            return label;
        }

        /// <summary>升目の中身を今の得点に合わせる。</summary>
        private void UpdateCells()
        {
            if (_cells == null)
            {
                return;
            }

            float now = Time.unscaledTime;

            for (int i = 0; i < _cells.Length; i++)
            {
                Cell cell = _cells[i];
                int laneNumber = i + 1;
                bool played = gameManager.IsLanePlayed(i);
                bool current = gameManager.LaneNumber == laneNumber && !gameManager.IsFinished;
                LaneScore score = played ? gameManager.GetLaneScore(i) : default;
                bool mark = played && (score.isStrike || score.isSpare);

                UpdateCellLook(cell, laneNumber, played, current, mark, now);

                if (cell.score == null)
                {
                    continue;
                }

                if (!played)
                {
                    cell.target = -1;
                    cell.score.text = emptyText;
                    cell.score.color = normalTextColor;
                    cell.score.rectTransform.localScale = Vector3.one;
                    if (cell.number != null)
                    {
                        cell.number.text = laneNumber.ToString();
                        cell.number.color = normalTextColor;
                    }
                    continue;
                }

                // 得点が入った（変わった）瞬間から、数字を回して増やす
                if (cell.target != score.score)
                {
                    cell.from = cell.target < 0 ? 0f : cell.target;
                    cell.target = score.score;
                    cell.startTime = now;
                }

                float t = (now - cell.startTime) / Mathf.Max(countUpSeconds, 0.01f);
                int shown = Mathf.RoundToInt(Mathf.Lerp(cell.from, cell.target, NeonUI.EaseOutCubic(t)));
                cell.score.text = shown.ToString();
                cell.score.color = mark ? highlightTextColor : normalTextColor;

                // 入った瞬間だけ、ふくらんで戻る
                float pop = t < 1f ? 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) : 1f;
                cell.score.rectTransform.localScale = new Vector3(pop, pop, 1f);

                if (cell.number != null)
                {
                    // ストライクとスペアは番号の位置に印を出す
                    cell.number.text = score.isStrike ? strikeText
                        : score.isSpare ? spareText
                        : laneNumber.ToString();
                    cell.number.color = mark ? highlightTextColor : normalTextColor;
                }
            }
        }

        /// <summary>升目の枠・光・帯の色と動き。</summary>
        private void UpdateCellLook(Cell cell, int laneNumber, bool played, bool current, bool mark, float now)
        {
            if (skin == null)
            {
                if (cell.background != null)
                {
                    cell.background.color = current ? currentColor : played ? playedColor : emptyColor;
                }
                return;
            }

            Color accent = skin.GetAccent(laneNumber);
            float breath = NeonUI.Breath(currentBreathSeconds);

            if (current)
            {
                cell.background.color = new Color(accent.r * 0.28f, accent.g * 0.28f, accent.b * 0.28f, 0.92f);
                cell.frame.color = Color.Lerp(accent, Color.white, 0.35f + 0.25f * breath);
                cell.glow.color = NeonUI.WithAlpha(accent, 0.35f + 0.3f * breath);

                // 光の帯が左から右へ横切る
                float phase = Mathf.Repeat(now / Mathf.Max(sweepSeconds, 0.1f), 1f);
                float width = ((RectTransform)cell.band.parent).rect.width;
                cell.band.anchoredPosition = new Vector2(Mathf.Lerp(-60f, width + 60f, phase), 0f);
                cell.band.GetComponent<Image>().color = NeonUI.WithAlpha(Color.white, 0.35f);
            }
            else
            {
                cell.background.color = played ? new Color(0f, 0f, 0f, 0.62f) : new Color(0f, 0f, 0f, 0.45f);
                Color edge = mark ? skin.Gold : played ? NeonUI.Scale(accent, 0.8f) : new Color(1f, 1f, 1f, 0.25f);
                cell.frame.color = played ? NeonUI.WithAlpha(edge, 0.85f) : edge;
                cell.glow.color = mark ? NeonUI.WithAlpha(skin.Gold, 0.22f) : Color.clear;
                cell.band.GetComponent<Image>().color = Color.clear;
            }

            if (cell.score != null)
            {
                NeonUI.SetNeonColor(cell.score, mark ? skin.Gold : current ? accent : NeonUI.Scale(accent, 0.7f), current ? 0.45f : 0.25f);
            }
        }

        /// <summary>合計を数え上げる。増えたら「+30」を浮かべる。</summary>
        private void UpdateTotal()
        {
            if (totalLabel == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            int total = gameManager.TotalScore;

            if (total != _totalTarget)
            {
                if (total > _totalTarget)
                {
                    ShowGain(total - _totalTarget, now);
                    _totalFrom = CurrentTotal(now);
                    _totalStart = now;
                }
                else
                {
                    // やり直し（合計が減る）ときは数えずにそのまま出す
                    _totalFrom = total;
                    _totalStart = -10f;
                }
                _totalTarget = total;
            }

            int shown = Mathf.RoundToInt(CurrentTotal(now));
            totalLabel.text = string.Format(totalFormat, shown, gameManager.PerfectScore);

            if (skin != null)
            {
                // 数え上げ中は光を強くし、終わったらゆっくり戻す（点滅ではない）
                float t = (now - _totalStart) / Mathf.Max(totalCountUpSeconds, 0.01f);
                float swell = t < 1.6f ? Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI) : 0f;
                Color accent = skin.GetAccent(gameManager.LaneNumber);
                NeonUI.SetNeonColor(totalLabel, Color.Lerp(accent, skin.Gold, swell), 0.4f + 0.5f * swell);
                float scale = 1f + 0.12f * swell;
                totalLabel.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            UpdateGain(now);
        }

        /// <summary>数え上げ途中の合計。</summary>
        private float CurrentTotal(float now)
        {
            float t = (now - _totalStart) / Mathf.Max(totalCountUpSeconds, 0.01f);
            return t >= 1f ? _totalTarget : Mathf.Lerp(_totalFrom, _totalTarget, NeonUI.EaseOutCubic(t));
        }

        /// <summary>「+30」を浮かべ始める。</summary>
        private void ShowGain(int gain, float now)
        {
            if (gainParent == null || skin == null)
            {
                return;
            }

            if (_gainLabel == null)
            {
                RectTransform rect = NeonUI.CreateRect(gainParent, "Gain", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 70f));
                _gainLabel = NeonUI.CreateText(rect, skin.BoldFont, skin.BoldNeonMaterial, 54f, Color.white, TextAlignmentOptions.Center);
            }

            _gainLabel.text = string.Format(UIText.ScoreGainFormat, gain);
            _gainStart = now;
        }

        /// <summary>「+30」を上へ浮かべながら消す。</summary>
        private void UpdateGain(float now)
        {
            if (_gainLabel == null)
            {
                return;
            }

            float t = (now - _gainStart) / Mathf.Max(gainSeconds, 0.01f);
            bool visible = t >= 0f && t < 1f;
            _gainLabel.enabled = visible;
            if (!visible)
            {
                return;
            }

            _gainLabel.rectTransform.anchoredPosition = new Vector2(0f, -10f - 70f * NeonUI.EaseOutCubic(t));
            Color c = Color.white;
            c.a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            _gainLabel.color = c;
            NeonUI.SetNeonColor(_gainLabel, skin.Gold, 0.6f);
        }

        /// <summary>得点板全体の枠の色をゆっくり流し、枠の上に光の粒を走らせる（電光掲示板の電飾のように）。</summary>
        private void UpdateBoardFrame()
        {
            if (boardFrame == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            boardFrame.color = NeonUI.Hue(now / Mathf.Max(frameHueSeconds, 0.1f), 0.7f);

            if (skin == null)
            {
                return;
            }

            if (_comets == null || _comets.Length == 0)
            {
                _comets = new Image[2];
                for (int i = 0; i < _comets.Length; i++)
                {
                    RectTransform rect = NeonUI.CreateRect(boardFrame.rectTransform, $"Comet{i}", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120f, 46f));
                    _comets[i] = NeonUI.CreateImage(rect, skin.Glow, Color.clear, false);
                }
            }

            Rect frame = boardFrame.rectTransform.rect;
            // 枠の線は画像の縁から少し内側にある
            float inset = 15f;
            float w = Mathf.Max(frame.width - inset * 2f, 1f);
            float h = Mathf.Max(frame.height - inset * 2f, 1f);
            for (int i = 0; i < _comets.Length; i++)
            {
                float lap = now / Mathf.Max(cometLapSeconds, 0.1f);
                float t = Mathf.Repeat(i == 0 ? lap : 0.5f - lap, 1f);
                Vector2 p = PointOnFrame(t, w, h) + new Vector2(inset, inset);
                RectTransform rect = _comets[i].rectTransform;
                rect.anchoredPosition = p;
                _comets[i].color = NeonUI.WithAlpha(NeonUI.Hue(now * 0.2f + i * 0.5f, 0.5f), 0.95f);
            }
        }

        /// <summary>四角の縁を一周する点（t は 0〜1。左下から時計の反対回り）。</summary>
        private static Vector2 PointOnFrame(float t, float w, float h)
        {
            float d = t * 2f * (w + h);
            if (d < w) return new Vector2(d, 0f);
            d -= w;
            if (d < h) return new Vector2(w, d);
            d -= h;
            if (d < w) return new Vector2(w - d, h);
            d -= w;
            return new Vector2(0f, h - d);
        }
    }
}
