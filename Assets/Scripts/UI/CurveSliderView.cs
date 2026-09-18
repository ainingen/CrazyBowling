using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Ball;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 構え中に触るカーブのスライダー。-1で左いっぱい、0でカーブ無し、+1で右いっぱい。
    /// 常に出しておき、引いている間は触れないようにする。
    /// 投球後も値は残るので、同じカーブで続けて投げられる。
    /// </summary>
    public class CurveSliderView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("値を渡す相手。")]
        [SerializeField] private BallController ballController;

        [Tooltip("カーブを決めるスライダー。Min Value は -1、Max Value は 1 にしておく。")]
        [SerializeField] private Slider slider;

        [Tooltip("今のカーブを出すテキスト。")]
        [SerializeField] private TMP_Text label;

        [Header("色")]
        [Tooltip("カーブを掛けているときの色。")]
        [SerializeField] private Color curveColor = new Color(0.35f, 0.85f, 0.45f);

        [Tooltip("カーブ無しのときの色。")]
        [SerializeField] private Color noCurveColor = new Color(0.55f, 0.55f, 0.55f);

        [Tooltip("引いている最中（触れないとき）の色。")]
        [SerializeField] private Color lockedColor = new Color(0.40f, 0.40f, 0.40f);

        [Header("文字")]
        [Tooltip("左カーブのときの書き方。{0} に強さのパーセントが入る。")]
        [SerializeField] private string leftFormat = UIText.CurveLeftFormat;

        [Tooltip("右カーブのときの書き方。{0} に強さのパーセントが入る。")]
        [SerializeField] private string rightFormat = UIText.CurveRightFormat;

        [Tooltip("カーブ無しのときに出す文字。")]
        [SerializeField] private string noCurveText = UIText.CurveNone;

        /// <summary>スライダーの値を反映している最中か。二重に反映しないための印。</summary>
        private bool _applying;

        private void Awake()
        {
            if (slider != null)
            {
                slider.minValue = -1f;
                slider.maxValue = 1f;
                slider.wholeNumbers = false;
                slider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        private void OnDestroy()
        {
            if (slider != null)
            {
                slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        private void OnSliderChanged(float value)
        {
            if (_applying || ballController == null)
            {
                return;
            }
            ballController.SetSelectedCurve(value);
        }

        private void LateUpdate()
        {
            if (ballController == null)
            {
                return;
            }

            // 引いている間は触らせない。構えに戻ったらまた触れる
            bool canEdit = ballController.IsAiming;
            if (slider != null)
            {
                if (slider.interactable != canEdit)
                {
                    slider.interactable = canEdit;
                }

                // ボール側が持っている値に合わせる（投球後も残る）
                if (!Mathf.Approximately(slider.value, ballController.SelectedCurve))
                {
                    _applying = true;
                    slider.value = ballController.SelectedCurve;
                    _applying = false;
                }
            }

            UpdateLabel(ballController.SelectedCurve, canEdit);
        }

        /// <summary>今のカーブを文字にする。</summary>
        private void UpdateLabel(float curve, bool canEdit)
        {
            if (label == null)
            {
                return;
            }

            float strength = Mathf.Abs(curve) * 100f;
            if (curve < -0.005f)
            {
                label.text = string.Format(leftFormat, strength);
            }
            else if (curve > 0.005f)
            {
                label.text = string.Format(rightFormat, strength);
            }
            else
            {
                label.text = noCurveText;
            }

            label.color = !canEdit ? lockedColor
                : Mathf.Abs(curve) > 0.005f ? curveColor
                : noCurveColor;
        }
    }
}
