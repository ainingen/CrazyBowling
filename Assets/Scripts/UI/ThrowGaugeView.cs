using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Ball;

namespace CrazyBowling.UI
{
    /// <summary>
    /// ドラッグ中だけ出る強さゲージ。
    /// BallController の状態と計算結果を読むだけで、投球には一切干渉しない。
    /// </summary>
    public class ThrowGaugeView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("状態を読む相手。")]
        [SerializeField] private BallController ballController;

        [Tooltip("表示と非表示を切り替える対象。")]
        [SerializeField] private GameObject gaugeRoot;

        [Tooltip("伸びるバー。Image Type は Filled / Vertical / Bottom にしておく。")]
        [SerializeField] private Image fillImage;

        [Tooltip("初速を出すテキスト。")]
        [SerializeField] private TMP_Text speedLabel;

        [Header("カーブ")]
        [Tooltip("カーブ表示のまとまり。強さゲージと一緒に出し入れする。")]
        [SerializeField] private GameObject curveRoot;

        [Tooltip("左へのカーブで伸びるバー。Image Type は Filled / Horizontal / Right にしておく。")]
        [SerializeField] private Image curveLeftImage;

        [Tooltip("右へのカーブで伸びるバー。Image Type は Filled / Horizontal / Left にしておく。")]
        [SerializeField] private Image curveRightImage;

        [Tooltip("カーブの強さと、どちらのボタンを押しているかを出すテキスト。")]
        [SerializeField] private TMP_Text curveLabel;

        [Tooltip("カーブのバーと文字の色。")]
        [SerializeField] private Color curveColor = new Color(0.35f, 0.85f, 0.45f);

        [Tooltip("カーブ無しのときの色。")]
        [SerializeField] private Color noCurveColor = new Color(0.55f, 0.55f, 0.55f);

        [Header("色")]
        [Tooltip("弱いときの色。")]
        [SerializeField] private Color weakColor = new Color(0.20f, 0.55f, 1.00f);

        [Tooltip("中間の色。")]
        [SerializeField] private Color mediumColor = new Color(1.00f, 0.85f, 0.10f);

        [Tooltip("強いときの色。")]
        [SerializeField] private Color strongColor = new Color(1.00f, 0.25f, 0.20f);

        [Tooltip("投げない範囲のときの色。")]
        [SerializeField] private Color cancelColor = new Color(0.55f, 0.55f, 0.55f);

        [Tooltip("どの割合で中間の色になるか。")]
        [Range(0f, 1f)]
        [SerializeField] private float mediumThreshold = 0.5f;

        [Header("文字")]
        [Tooltip("初速の書き方。{0} に数値が入る。")]
        [SerializeField] private string speedFormat = "{0:F1} m/s";

        [Tooltip("投げない範囲のときに出す文字。")]
        [SerializeField] private string cancelText = "キャンセル";

        [Tooltip("左カーブのときの書き方。{0} に強さのパーセントが入る。")]
        [SerializeField] private string curveLeftFormat = "◀ 左カーブ {0:F0}%";

        [Tooltip("右カーブのときの書き方。{0} に強さのパーセントが入る。")]
        [SerializeField] private string curveRightFormat = "右カーブ {0:F0}% ▶";

        [Tooltip("カーブ無しのときに出す文字。")]
        [SerializeField] private string noCurveText = "ストレート";

        private void LateUpdate()
        {
            if (ballController == null)
            {
                return;
            }

            bool isDragging = ballController.IsDragging;

            if (gaugeRoot != null && gaugeRoot.activeSelf != isDragging)
            {
                gaugeRoot.SetActive(isDragging);
            }

            if (curveRoot != null && curveRoot.activeSelf != isDragging)
            {
                curveRoot.SetActive(isDragging);
            }

            if (!isDragging)
            {
                return;
            }

            ThrowResult preview = ballController.DragPreview;
            Color color = GetColor(preview);

            if (fillImage != null)
            {
                fillImage.fillAmount = preview.pullRatio;
                fillImage.color = color;
            }

            if (speedLabel != null)
            {
                speedLabel.text = preview.isValid
                    ? string.Format(speedFormat, preview.speed)
                    : cancelText;
                speedLabel.color = color;
            }

            UpdateCurve(preview);
        }

        /// <summary>カーブのバーと文字。中央から左右に伸びるので、向きと強さが同時に分かる。</summary>
        private void UpdateCurve(ThrowResult preview)
        {
            float leftAmount = preview.curve < 0f ? -preview.curve : 0f;
            float rightAmount = preview.curve > 0f ? preview.curve : 0f;
            bool hasCurve = leftAmount > 0f || rightAmount > 0f;
            Color barColor = hasCurve ? curveColor : noCurveColor;

            if (curveLeftImage != null)
            {
                curveLeftImage.fillAmount = leftAmount;
                curveLeftImage.color = barColor;
            }

            if (curveRightImage != null)
            {
                curveRightImage.fillAmount = rightAmount;
                curveRightImage.color = barColor;
            }

            if (curveLabel != null)
            {
                if (leftAmount > 0f)
                {
                    curveLabel.text = string.Format(curveLeftFormat, leftAmount * 100f);
                }
                else if (rightAmount > 0f)
                {
                    curveLabel.text = string.Format(curveRightFormat, rightAmount * 100f);
                }
                else
                {
                    curveLabel.text = noCurveText;
                }
                curveLabel.color = barColor;
            }
        }

        /// <summary>強さに応じた色。投げない範囲では灰色。</summary>
        private Color GetColor(ThrowResult preview)
        {
            if (!preview.isValid)
            {
                return cancelColor;
            }

            float ratio = preview.pullRatio;
            if (ratio <= mediumThreshold)
            {
                return Color.Lerp(weakColor, mediumColor, Mathf.InverseLerp(0f, mediumThreshold, ratio));
            }

            return Color.Lerp(mediumColor, strongColor, Mathf.InverseLerp(mediumThreshold, 1f, ratio));
        }
    }
}
