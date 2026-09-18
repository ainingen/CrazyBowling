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

        [Tooltip("レーンごとの内訳を出すテキスト。")]
        [SerializeField] private TMP_Text breakdownLabel;

        [Tooltip("もう一度遊ぶボタン。")]
        [SerializeField] private Button restartButton;

        [Header("出るまでの間")]
        [Tooltip("最後のレーンが終わってから出るまでの間（秒）。倒れたピンを見る時間。")]
        [SerializeField] private float delaySeconds = 1.5f;

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
            }

            if (!resultRoot.activeSelf)
            {
                resultRoot.SetActive(true);
            }
        }

        /// <summary>合計と内訳を作る。</summary>
        private void Build()
        {
            if (totalLabel != null)
            {
                totalLabel.text = string.Format(totalFormat,
                    gameManager.TotalScore, gameManager.PerfectScore);
            }

            if (breakdownLabel == null)
            {
                return;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < gameManager.LaneCount; i++)
            {
                LaneData data = gameManager.GetLaneData(i);
                LaneScore score = gameManager.GetLaneScore(i);

                string kind = !gameManager.IsLanePlayed(i) ? UIText.NotPlayed
                    : score.isStrike ? strikeText
                    : score.isSpare ? spareText
                    : string.Format(fallenFormat, score.fallen);

                builder.AppendLine(string.Format(lineFormat,
                    i + 1,
                    data != null ? data.LaneName : string.Empty,
                    kind,
                    gameManager.IsLanePlayed(i) ? score.score.ToString() : UIText.NotPlayed));
            }

            breakdownLabel.text = builder.ToString();
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
