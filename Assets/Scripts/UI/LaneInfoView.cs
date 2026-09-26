using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 今どのレーンにいるかを出す。レーン名と進み具合、入ったときの一言。
    /// 段階6：レーン名はそのレーンの差し色で光らせる。一言（説明文）は淡々と白い文字で出す。
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

        [Header("光（段階6）")]
        [Tooltip("見た目の材料。空なら色を変えない。")]
        [SerializeField] private UISkin skin;

        [Tooltip("差し色に染める帯（空でもよい）。")]
        [SerializeField] private Image accentBar;

        [Header("一言の出し方")]
        [Tooltip("レーンに入ってから一言を出し始めるまでの間（秒）。" +
                 "レーン名の大見出し（LaneIntroView）と重ならないようにする。0なら入った瞬間に出す。")]
        [SerializeField] private float hintDelaySeconds = 0f;

        [Tooltip("一言を出しておく時間（秒）。" +
                 "説明は2文で最長35字あり、ゆっくり読むと5秒ほどかかる。" +
                 "下見カメラが流れている間と重なるので、短くしすぎないこと。")]
        [SerializeField] private float hintSeconds = 5f;

        [Tooltip("消えるのにかける時間（秒）。")]
        [SerializeField] private float hintFadeSeconds = 0.6f;

        [Header("文字")]
        [Tooltip("進み具合の書き方。{0} が今のレーン、{1} が全部の数。")]
        [SerializeField] private string progressFormat = UIText.ProgressFormat;

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

            UpdateAccent();
            UpdateHint(lane);
        }

        /// <summary>レーンに入り直したときのように、一言を最初から出し直す（タイトルから始めたとき）。</summary>
        public void Replay()
        {
            _shownLaneNumber = -1;
        }

        /// <summary>レーン名と帯を、そのレーンの差し色にする。</summary>
        private void UpdateAccent()
        {
            if (skin == null)
            {
                return;
            }

            Color accent = skin.GetAccent(gameManager.LaneNumber);
            float breath = NeonUI.Breath(2.4f);
            NeonUI.SetNeonColor(laneNameLabel, accent, 0.35f + 0.2f * breath);
            if (accentBar != null)
            {
                accentBar.color = Color.Lerp(accent, Color.white, 0.2f * breath);
            }
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

            // 出し始めるまでは消しておく。出てから hintSeconds のあいだ出し、ゆっくり消す
            float shown = _hintTimer - hintDelaySeconds;
            float alpha = shown < 0f ? 0f : Mathf.Clamp01(shown / 0.3f);
            if (shown > hintSeconds)
            {
                float fade = Mathf.Max(hintFadeSeconds, 0.01f);
                alpha = Mathf.Clamp01(1f - (shown - hintSeconds) / fade);
            }

            Color color = hintLabel.color;
            color.a = alpha;
            hintLabel.color = color;
        }
    }
}
