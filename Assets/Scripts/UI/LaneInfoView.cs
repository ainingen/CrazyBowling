using UnityEngine;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 今どのレーンにいるかを出す。レーン名と進み具合、入ったときの一言。
    /// </summary>
    public class LaneInfoView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("状態を読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("「3 / 10」のような進み具合を出すテキスト。")]
        [SerializeField] private TMP_Text progressLabel;

        [Tooltip("レーン名を出すテキスト。")]
        [SerializeField] private TMP_Text laneNameLabel;

        [Tooltip("レーンに入ったときの一言を出すテキスト。")]
        [SerializeField] private TMP_Text hintLabel;

        [Header("一言の出し方")]
        [Tooltip("一言を出しておく時間（秒）。")]
        [SerializeField] private float hintSeconds = 3f;

        [Tooltip("消えるのにかける時間（秒）。")]
        [SerializeField] private float hintFadeSeconds = 0.6f;

        [Header("文字")]
        [Tooltip("進み具合の書き方。{0} が今のレーン、{1} が全部の数。")]
        [SerializeField] private string progressFormat = "{0} / {1}";

        /// <summary>一言を出し始めてからの経過（秒）。</summary>
        private float _hintTimer;

        /// <summary>今出している一言のもとになったレーン。切り替わりを見るために覚える。</summary>
        private int _shownLaneNumber = -1;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
        }

        private void LateUpdate()
        {
            if (gameManager == null)
            {
                return;
            }

            LaneData lane = gameManager.CurrentLane;

            if (progressLabel != null)
            {
                progressLabel.text = lane != null
                    ? string.Format(progressFormat, gameManager.LaneNumber, gameManager.LaneCount)
                    : string.Empty;
            }

            if (laneNameLabel != null)
            {
                laneNameLabel.text = lane != null ? lane.LaneName : string.Empty;
            }

            UpdateHint(lane);
        }

        /// <summary>レーンが変わったら一言を出し直し、時間が経ったら消す。</summary>
        private void UpdateHint(LaneData lane)
        {
            if (hintLabel == null)
            {
                return;
            }

            // レーンが変わった瞬間に出し直す
            if (gameManager.LaneNumber != _shownLaneNumber)
            {
                _shownLaneNumber = gameManager.LaneNumber;
                _hintTimer = 0f;

                string text = lane != null && !string.IsNullOrEmpty(lane.HintText)
                    ? lane.HintText
                    : lane != null ? lane.Description : string.Empty;
                hintLabel.text = text;
            }

            _hintTimer += Time.deltaTime;

            float alpha = 1f;
            if (_hintTimer > hintSeconds)
            {
                float fade = Mathf.Max(hintFadeSeconds, 0.01f);
                alpha = Mathf.Clamp01(1f - (_hintTimer - hintSeconds) / fade);
            }

            Color color = hintLabel.color;
            color.a = alpha;
            hintLabel.color = color;
        }
    }
}
