using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// レーンごとの得点表と合計。
    /// 升目はレーンの本数に合わせて実行時に作る。
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

        [Tooltip("升目の文字に使うフォント。")]
        [SerializeField] private TMP_FontAsset font;

        [Header("升目の見た目")]
        [Tooltip("レーン番号の文字の大きさ。")]
        [SerializeField] private float numberFontSize = 18f;

        [Tooltip("得点の文字の大きさ。")]
        [SerializeField] private float scoreFontSize = 26f;

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

        [Header("文字")]
        [Tooltip("まだ遊んでいないレーンに出す文字。")]
        [SerializeField] private string emptyText = UIText.NotPlayed;

        [Tooltip("ストライクに出す文字。")]
        [SerializeField] private string strikeText = UIText.StrikeMark;

        [Tooltip("スペアに出す文字。")]
        [SerializeField] private string spareText = UIText.SpareMark;

        [Tooltip("合計の書き方。{0} が合計、{1} が満点。")]
        [SerializeField] private string totalFormat = UIText.TotalFormat;

        /// <summary>升目1つぶんの部品。</summary>
        private struct Cell
        {
            public Image background;
            public TMP_Text number;
            public TMP_Text score;
        }

        private Cell[] _cells;

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

            if (totalLabel != null)
            {
                totalLabel.text = string.Format(totalFormat, gameManager.TotalScore, gameManager.PerfectScore);
            }
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

            var background = root.AddComponent<Image>();
            background.color = emptyColor;
            background.raycastTarget = false;

            var layout = root.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;

            TMP_Text number = BuildLabel(root.transform, "Number", numberFontSize,
                new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            number.text = laneNumber.ToString();

            TMP_Text score = BuildLabel(root.transform, "Score", scoreFontSize,
                new Vector2(0f, 0f), new Vector2(1f, 0.58f));
            score.text = emptyText;

            return new Cell { background = background, number = number, score = score };
        }

        /// <summary>升目の中の文字を1つ作る。</summary>
        private TMP_Text BuildLabel(Transform parent, string name, float size,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                label.font = font;
            }
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = normalTextColor;
            label.raycastTarget = false;

            return label;
        }

        /// <summary>升目の中身を今の得点に合わせる。</summary>
        private void UpdateCells()
        {
            if (_cells == null)
            {
                return;
            }

            for (int i = 0; i < _cells.Length; i++)
            {
                Cell cell = _cells[i];
                bool played = gameManager.IsLanePlayed(i);
                bool current = gameManager.LaneNumber == i + 1 && !gameManager.IsFinished;

                if (cell.background != null)
                {
                    cell.background.color = current ? currentColor
                        : played ? playedColor
                        : emptyColor;
                }

                if (cell.score == null)
                {
                    continue;
                }

                if (!played)
                {
                    cell.score.text = emptyText;
                    cell.score.color = normalTextColor;
                    continue;
                }

                LaneScore score = gameManager.GetLaneScore(i);
                cell.score.text = score.score.ToString();
                cell.score.color = score.isStrike || score.isSpare ? highlightTextColor : normalTextColor;

                if (cell.number != null)
                {
                    // ストライクとスペアは番号の位置に印を出す
                    cell.number.text = score.isStrike ? strikeText
                        : score.isSpare ? spareText
                        : (i + 1).ToString();
                    cell.number.color = score.isStrike || score.isSpare
                        ? highlightTextColor : normalTextColor;
                }
            }
        }
    }
}
