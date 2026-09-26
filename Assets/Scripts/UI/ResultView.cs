using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 全レーンが終わったときに出すリザルト。
    /// 合計点と、レーンごとの内訳を出す。
    ///
    /// 段階6：ゲームの顔として派手にする（見た目の材料 skin があるとき）。
    ///   ・後ろで光の筋が回る。RESULT はネオン
    ///   ・合計が0から数え上がり、1文字ずつ虹色が流れる。数え終わると RANK（S〜D）が飛び込む
    ///   ・内訳の10行が1行ずつ右から滑り込む。ストライク・スペアの行は金色
    ///   ・締めの一言は説明書口調で淡々と
    /// 得点は GameManager から読むだけ。RANK は合計÷満点の割合で決める表示だけで、得点の計算には関わらない。
    /// </summary>
    public class ResultView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("結果を読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("出し入れするまとまり。")]
        [SerializeField] private GameObject resultRoot;

        [Tooltip("合計点を出すテキスト。")]
        [SerializeField] private TMP_Text totalLabel;

        [Tooltip("レーンごとの内訳を出すテキスト（見た目の材料が無いとき）。")]
        [SerializeField] private TMP_Text breakdownLabel;

        [Tooltip("もう一度遊ぶボタン。")]
        [SerializeField] private Button restartButton;

        [Header("派手な見た目（段階6）")]
        [Tooltip("見た目の材料。空なら今までどおりの地味な画面。")]
        [SerializeField] private UISkin skin;

        [Tooltip("RESULT の見出し。")]
        [SerializeField] private TMP_Text titleLabel;

        [Tooltip("内訳の行を並べる入れ物。")]
        [SerializeField] private RectTransform rowsParent;

        [Tooltip("RANK を出すテキスト。")]
        [SerializeField] private TMP_Text rankLabel;

        [Tooltip("締めの一言を出すテキスト。")]
        [SerializeField] private TMP_Text commentLabel;

        [Tooltip("光の筋を回す入れ物（画面の後ろ）。")]
        [SerializeField] private RectTransform raysParent;

        [Header("出るまでの間")]
        [Tooltip("最後のレーンが終わってから出るまでの間（秒）。倒れたピンを見る時間。")]
        [SerializeField] private float delaySeconds = 1.5f;

        [Header("動き（秒）")]
        [Tooltip("合計が数え上がるのにかける時間。")]
        [SerializeField] private float totalCountSeconds = 1.8f;

        [Tooltip("内訳の行が滑り込む間隔。")]
        [SerializeField] private float rowInterval = 0.09f;

        [Tooltip("光の筋の本数。")]
        [SerializeField] private int rayCount = 18;

        [Tooltip("RANK の境目（合計÷満点）。S・A・B・C の順。これより下は D。")]
        [SerializeField] private float[] rankThresholds = { 0.6f, 0.45f, 0.3f, 0.15f };

        [Header("文字")]
        [Tooltip("合計点の書き方。{0} が合計、{1} が満点。")]
        [SerializeField] private string totalFormat = UIText.ResultTotalFormat;

        [Tooltip("内訳1行の書き方。{0} がレーン番号、{1} がレーン名、{2} が結果、{3} が得点。")]
        [SerializeField] private string lineFormat = UIText.ResultLineFormat;

        [Tooltip("ストライクに出す文字。")]
        [SerializeField] private string strikeText = UIText.Strike;

        [Tooltip("スペアに出す文字。")]
        [SerializeField] private string spareText = UIText.Spare;

        [Tooltip("それ以外に出す文字。{0} が倒した本数。")]
        [SerializeField] private string fallenFormat = UIText.PinsFormat;

        /// <summary>終わってからの経過（秒）。</summary>
        private float _timer;

        /// <summary>もう内訳を作ったか。毎フレーム作り直さないための印。</summary>
        private bool _built;

        /// <summary>画面を出した時刻（動きの起点）。</summary>
        private float _shownTime;

        [System.NonSerialized] private Image[] _rays;

        /// <summary>内訳1行ぶんの部品。</summary>
        private struct Row
        {
            public RectTransform rect;
            public Image frame;
            public TMP_Text number;
            public TMP_Text name;
            public TMP_Text kind;
            public TMP_Text score;
            public bool mark;
            public Color accent;
        }

        [System.NonSerialized] private Row[] _rows;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }

            if (resultRoot != null)
            {
                resultRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(Restart);
            }
        }

        private void LateUpdate()
        {
            if (gameManager == null || resultRoot == null)
            {
                return;
            }

            if (!gameManager.IsFinished)
            {
                // 途中で戻ったときのために、出ていたら引っ込める
                if (resultRoot.activeSelf)
                {
                    resultRoot.SetActive(false);
                }
                _timer = 0f;
                _built = false;
                return;
            }

            _timer += Time.deltaTime;
            if (_timer < delaySeconds)
            {
                return;
            }

            if (!_built)
            {
                Build();
                _built = true;
                _shownTime = Time.unscaledTime;
            }

            if (!resultRoot.activeSelf)
            {
                resultRoot.SetActive(true);
            }

            Animate(Time.unscaledTime - _shownTime);
        }

        /// <summary>合計と内訳を作る。</summary>
        private void Build()
        {
            if (totalLabel != null)
            {
                totalLabel.text = string.Format(totalFormat,
                    gameManager.TotalScore, gameManager.PerfectScore);
            }

            if (skin != null && rowsParent != null)
            {
                BuildRows();
                BuildRays();
                if (commentLabel != null)
                {
                    commentLabel.text = UIText.ResultComment;
                }
                if (rankLabel != null)
                {
                    rankLabel.text = string.Format(UIText.RankFormat, Rank());
                }
                return;
            }

            if (breakdownLabel == null)
            {
                return;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < gameManager.LaneCount; i++)
            {
                builder.AppendLine(string.Format(lineFormat,
                    i + 1, LaneName(i), KindText(i),
                    gameManager.IsLanePlayed(i) ? gameManager.GetLaneScore(i).score.ToString() : UIText.NotPlayed));
            }

            breakdownLabel.text = builder.ToString();
        }

        private string LaneName(int index)
        {
            LaneData data = gameManager.GetLaneData(index);
            return data != null ? data.LaneName : string.Empty;
        }

        private string KindText(int index)
        {
            LaneScore score = gameManager.GetLaneScore(index);
            return !gameManager.IsLanePlayed(index) ? UIText.NotPlayed
                : score.isStrike ? strikeText
                : score.isSpare ? spareText
                : string.Format(fallenFormat, score.fallen);
        }

        /// <summary>合計÷満点で S〜D を決める（表示だけ）。</summary>
        private string Rank()
        {
            float ratio = gameManager.PerfectScore > 0 ? gameManager.TotalScore / (float)gameManager.PerfectScore : 0f;
            string[] names = { "S", "A", "B", "C" };
            for (int i = 0; i < rankThresholds.Length && i < names.Length; i++)
            {
                if (ratio >= rankThresholds[i])
                {
                    return names[i];
                }
            }
            return "D";
        }

        /// <summary>内訳の行を作り直す。</summary>
        private void BuildRows()
        {
            for (int i = rowsParent.childCount - 1; i >= 0; i--)
            {
                Destroy(rowsParent.GetChild(i).gameObject);
            }

            int count = gameManager.LaneCount;
            _rows = new Row[count];
            float rowHeight = 1f / Mathf.Max(count, 1);
            for (int i = 0; i < count; i++)
            {
                float top = 1f - i * rowHeight;
                RectTransform rect = NeonUI.CreateRect(rowsParent, $"Row{i + 1:00}", new Vector2(0f, top - rowHeight), new Vector2(1f, top), Vector2.zero, new Vector2(0f, -8f));
                var row = new Row { rect = rect };
                LaneScore score = gameManager.GetLaneScore(i);
                row.mark = gameManager.IsLanePlayed(i) && (score.isStrike || score.isSpare);
                row.accent = skin.GetAccent(i + 1);

                NeonUI.CreateImage(NeonUI.CreateStretch(rect, "Plate"), skin.Panel, new Color(0f, 0f, 0f, 0.6f), true);
                row.frame = NeonUI.CreateImage(NeonUI.CreateRect(rect, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(8f, 8f)), skin.NeonFrame, Color.white, true);
                row.frame.pixelsPerUnitMultiplier = 2.4f;

                row.number = Cellular(rect, "Number", 0f, 0.1f, TextAlignmentOptions.Center, (i + 1).ToString("00"), skin.BoldNeonMaterial);
                row.name = Cellular(rect, "Name", 0.11f, 0.56f, TextAlignmentOptions.Left, LaneName(i), skin.BoldPlainMaterial);
                row.kind = Cellular(rect, "Kind", 0.56f, 0.8f, TextAlignmentOptions.Center, KindText(i), skin.BoldPlainMaterial);
                row.score = Cellular(rect, "Score", 0.8f, 0.97f, TextAlignmentOptions.Right,
                    gameManager.IsLanePlayed(i) ? score.score.ToString() : UIText.NotPlayed, skin.BoldNeonMaterial);

                if (row.mark)
                {
                    row.kind.color = skin.Gold;
                    row.score.color = skin.Gold;
                }
                _rows[i] = row;
            }
        }

        /// <summary>行の中の文字を1つ作る。left〜right は行の幅に対する割合。</summary>
        private TMP_Text Cellular(RectTransform parent, string name, float left, float right,
            TextAlignmentOptions alignment, string text, Material material)
        {
            RectTransform rect = NeonUI.CreateRect(parent, name, new Vector2(left, 0f), new Vector2(right, 1f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = NeonUI.CreateText(rect, skin.BoldFont, material, 34f, Color.white, alignment);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = 34f;
            label.text = text;
            return label;
        }

        /// <summary>後ろで回る光の筋を作る（1回だけ）。</summary>
        private void BuildRays()
        {
            if (raysParent == null || (_rays != null && _rays.Length > 0))
            {
                return;
            }

            _rays = new Image[Mathf.Max(0, rayCount)];
            for (int i = 0; i < _rays.Length; i++)
            {
                RectTransform ray = NeonUI.CreateRect(raysParent, $"Ray{i:00}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260f, 1400f));
                ray.pivot = new Vector2(0.5f, 1f);
                ray.localEulerAngles = new Vector3(0f, 0f, 180f + i * 360f / _rays.Length);
                _rays[i] = NeonUI.CreateImage(ray, skin.Ray, Color.clear, false);
            }
        }

        /// <summary>出してからの経過 t（秒）に合わせて動かす。</summary>
        private void Animate(float t)
        {
            if (skin == null || _rows == null)
            {
                return;
            }

            float now = Time.unscaledTime;

            // 光の筋：ゆっくり回り、色が筋ごとにずれて流れる
            if (raysParent != null && _rays != null)
            {
                raysParent.localEulerAngles = new Vector3(0f, 0f, now * 9f);
                float fade = Mathf.Clamp01(t / 0.8f);
                for (int i = 0; i < _rays.Length; i++)
                {
                    _rays[i].color = NeonUI.WithAlpha(NeonUI.Hue(i / (float)_rays.Length - now * 0.05f, 0.75f), 0.3f * fade);
                }
            }

            // RESULT：上から落ちてきて止まる。光はゆっくり呼吸する
            if (titleLabel != null)
            {
                float drop = NeonUI.EaseOutBack(t / 0.45f);
                titleLabel.rectTransform.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(200f, 0f, drop));
                NeonUI.SetNeonColor(titleLabel, NeonUI.Hue(now * 0.08f, 0.8f), 0.5f + 0.3f * NeonUI.Breath(2f));
            }

            // 合計：0から数え上がる。数えている間は光を強く
            if (totalLabel != null)
            {
                float count = NeonUI.EaseOutCubic((t - 0.3f) / Mathf.Max(totalCountSeconds, 0.01f));
                int shown = Mathf.RoundToInt(gameManager.TotalScore * count);
                totalLabel.text = string.Format(totalFormat, shown, gameManager.PerfectScore);
                float counting = count < 1f ? 1f : NeonUI.Breath(2.4f) * 0.4f;
                NeonUI.SetNeonColor(totalLabel, skin.Gold, 0.45f + 0.45f * counting);
                float pop = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01((t - 0.3f) / Mathf.Max(totalCountSeconds, 0.01f)) * Mathf.PI);
                totalLabel.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }

            // RANK：数え終わったら大きく飛び込む
            if (rankLabel != null)
            {
                float rankT = (t - 0.3f - totalCountSeconds) / 0.4f;
                rankLabel.alpha = Mathf.Clamp01(rankT * 3f);
                float scale = rankT <= 0f ? 3f : Mathf.LerpUnclamped(3f, 1f, NeonUI.EaseOutBack(rankT));
                rankLabel.rectTransform.localScale = new Vector3(scale, scale, 1f);
                rankLabel.rectTransform.localEulerAngles = new Vector3(0f, 0f, rankT <= 0f ? -20f : Mathf.LerpUnclamped(-20f, -6f, NeonUI.EaseOutBack(rankT)));
                NeonUI.SetNeonColor(rankLabel, NeonUI.Hue(now * 0.15f, 0.8f), 0.7f);
            }

            // 内訳：1行ずつ右から滑り込む
            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = _rows[i];
                float slide = NeonUI.EaseOutBack((t - 0.4f - i * rowInterval) / 0.35f);
                row.rect.anchoredPosition = new Vector2(Mathf.LerpUnclamped(1400f, 0f, slide), 0f);
                Color edge = row.mark ? skin.Gold : row.accent;
                row.frame.color = Color.Lerp(edge, Color.white, 0.25f * NeonUI.Breath(2.2f, i * 0.1f));
                NeonUI.SetNeonColor(row.number, row.accent, 0.4f);
                NeonUI.SetNeonColor(row.score, row.mark ? skin.Gold : row.accent, 0.45f);
            }

            // 締めの一言：淡々と出るだけ
            if (commentLabel != null)
            {
                commentLabel.alpha = Mathf.Clamp01((t - 0.6f - totalCountSeconds) / 0.5f);
            }
        }

        /// <summary>最初のレーンから遊び直す。</summary>
        public void Restart()
        {
            if (gameManager == null)
            {
                return;
            }

            if (resultRoot != null)
            {
                resultRoot.SetActive(false);
            }
            _timer = 0f;
            _built = false;

            gameManager.StartGame();
        }
    }
}
