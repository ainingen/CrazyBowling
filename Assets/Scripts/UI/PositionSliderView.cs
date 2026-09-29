using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Ball;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 構え中に触る立ち位置のゲージ（POSITION。段階6）。-1で左いっぱい、0で真ん中、+1で右いっぱい。
    /// ゲージの左右いっぱいが、ボールの立ち位置の範囲（BallController の sideMoveLimit。±0.4m）になる。
    /// 指でもマウスでも同じ操作。動かすとボールがその位置へ動き、投げるまでは何度でも動かせる。
    /// 投球後も値は残るので、同じ位置から続けて投げられる。作りはカーブのつまみ（<see cref="CurveSliderView"/>）と同じ。
    /// </summary>
    public class PositionSliderView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("立ち位置を渡す相手。")]
        [SerializeField] private BallController ballController;

        [Tooltip("立ち位置を決めるスライダー。値の範囲はこのスクリプトが -1〜1 にする。")]
        [SerializeField] private Slider slider;

        [Tooltip("今の立ち位置を出すテキスト。押すと真ん中に戻る。")]
        [SerializeField] private TMP_Text label;

        [Tooltip("見出し（POSITION）のテキスト。")]
        [SerializeField] private TMP_Text heading;

        [Tooltip("結果画面の間はゲージと文字を隠す。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Header("色")]
        [Tooltip("真ん中から外しているときの色。")]
        [SerializeField] private Color offCenterColor = new Color(0.35f, 0.75f, 1.00f);

        [Tooltip("真ん中のときの色（カーブの STRAIGHT と同じ）。")]
        [SerializeField] private Color centerColor = new Color(0.92f, 0.95f, 1.00f);

        [Tooltip("引いている最中（触れないとき）の色。")]
        [SerializeField] private Color lockedColor = new Color(0.55f, 0.55f, 0.62f);

        [Header("文字")]
        [Tooltip("左に寄せたときの書き方。{0} に中央からの距離（cm）が入る。")]
        [SerializeField] private string leftFormat = UIText.PositionLeftFormat;

        [Tooltip("右に寄せたときの書き方。{0} に中央からの距離（cm）が入る。")]
        [SerializeField] private string rightFormat = UIText.PositionRightFormat;

        [Tooltip("真ん中のときに出す文字。")]
        [SerializeField] private string centerText = UIText.PositionCenter;

        [Header("真ん中に戻しやすく")]
        [Tooltip("ゲージを離したとき、この幅より真ん中に近ければ真ん中（0）に吸い付ける（0.05 で ±5%）。0 で吸い付けない。")]
        [Range(0f, 0.3f)]
        [SerializeField] private float snapWidth = 0.05f;

        /// <summary>ゲージの見え方（結果画面の間は隠す）。</summary>
        private CanvasGroup _group;

        /// <summary>ゲージの値を反映している最中か。二重に反映しないための印。</summary>
        private bool _applying;

        /// <summary>押している間にゲージを動かしたか（離したときに吸い付けるため）。</summary>
        private bool _dragged;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
            if (heading != null)
            {
                heading.text = UIText.PositionHeading;
            }
            if (slider != null)
            {
                _group = slider.GetComponent<CanvasGroup>();
                if (_group == null)
                {
                    _group = slider.gameObject.AddComponent<CanvasGroup>();
                }
                slider.minValue = -1f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
                slider.onValueChanged.AddListener(OnSliderChanged);
            }

            // 文字（CENTER・LEFT・RIGHT）を押したら真ん中に戻す
            if (label != null)
            {
                label.raycastTarget = true;
                CurveLabelTap tap = label.GetComponent<CurveLabelTap>();
                if (tap == null)
                {
                    tap = label.gameObject.AddComponent<CurveLabelTap>();
                }
                tap.Tapped += ResetToCenter;
            }
        }

        /// <summary>真ん中に戻す（構え中だけ）。</summary>
        public void ResetToCenter()
        {
            if (ballController != null && ballController.IsAiming)
            {
                ballController.SetSideOffset(0f);
            }
        }

        private void OnDestroy()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
            if (label != null && label.TryGetComponent(out CurveLabelTap tap))
            {
                tap.Tapped -= ResetToCenter;
            }
        }

        private void OnSliderChanged(float value)
        {
            if (_applying || ballController == null)
            {
                return;
            }
            ballController.SetSideOffset(PositionGauge.ToOffset(value, ballController.SideLimit));
            _dragged = true;
        }

        private void LateUpdate()
        {
            if (ballController == null)
            {
                return;
            }

            // 結果画面の間は隠す（カーブのつまみと同じ）
            if (_group != null)
            {
                bool show = gameManager == null || !gameManager.IsFinished;
                _group.alpha = show ? 1f : 0f;
                _group.blocksRaycasts = show;
            }

            float limit = ballController.SideLimit;

            // 離したとき、真ん中付近なら真ん中に吸い付ける
            UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
            if (_dragged && (pointer == null || !pointer.press.isPressed))
            {
                _dragged = false;
                float snapped = CurveSnap.Apply(PositionGauge.ToValue(ballController.SideOffset, limit), snapWidth);
                ballController.SetSideOffset(PositionGauge.ToOffset(snapped, limit));
            }

            // 引いている間・転がっている間は触らせない。構えに戻ったらまた触れる
            bool canEdit = ballController.IsAiming;
            float value = PositionGauge.ToValue(ballController.SideOffset, limit);
            if (slider != null)
            {
                if (slider.interactable != canEdit)
                {
                    slider.interactable = canEdit;
                }

                // ボール側が持っている値に合わせる（投球後も残る）
                if (!Mathf.Approximately(slider.value, value))
                {
                    _applying = true;
                    slider.value = value;
                    _applying = false;
                }
            }

            UpdateLabel(ballController.SideOffset, canEdit);
        }

        /// <summary>今の立ち位置を文字にする。</summary>
        private void UpdateLabel(float offset, bool canEdit)
        {
            if (label == null)
            {
                return;
            }

            int cm = PositionGauge.ToCentimeters(offset);
            if (cm == 0)
            {
                label.text = centerText;
            }
            else
            {
                label.text = string.Format(offset < 0f ? leftFormat : rightFormat, cm);
            }

            label.color = !canEdit ? lockedColor
                : cm != 0 ? offCenterColor
                : centerColor;
        }
    }
}
