using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.UI
{
    /// <summary>
    /// ドラッグ中だけ出る、向きの矢印。
    /// 向きは BallController.CalculateThrowDirection をそのまま使うので、
    /// 実際に飛ぶ方向と必ず一致する（Invert Side Angle の設定も反映される）。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ThrowAimView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("状態を読む相手。")]
        [SerializeField] private BallController ballController;

        [Tooltip("矢印を描く LineRenderer。")]
        [SerializeField] private LineRenderer lineRenderer;

        [Header("長さ")]
        [Tooltip("引き幅0のときの長さ（m）。")]
        [SerializeField] private float minLength = 0.5f;

        [Tooltip("最大まで引いたときの長さ（m）。")]
        [SerializeField] private float maxLength = 4f;

        [Tooltip("床から浮かせる高さ（m）。めり込みを防ぐ。")]
        [SerializeField] private float heightOffset = 0.02f;

        [Header("矢じり")]
        [Tooltip("矢じりの長さ（m）。")]
        [SerializeField] private float headLength = 0.4f;

        [Tooltip("矢じりの開き角（度）。")]
        [SerializeField] private float headAngle = 25f;

        [Header("線")]
        [Tooltip("根元の太さ（m）。")]
        [SerializeField] private float startWidth = 0.06f;

        [Tooltip("先端の太さ（m）。")]
        [SerializeField] private float endWidth = 0.06f;

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

        private void Reset()
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        private void LateUpdate()
        {
            if (ballController == null || lineRenderer == null)
            {
                return;
            }

            bool isDragging = ballController.IsDragging;
            lineRenderer.enabled = isDragging;

            if (!isDragging)
            {
                return;
            }

            ThrowResult preview = ballController.DragPreview;
            Vector3 direction = ballController.CalculateThrowDirection(preview.sideAngle);

            // ボールの足元から描く。当たり判定の下端を基準にするので、
            // ボールの大きさを変えても追従する
            Vector3 ballPosition = ballController.transform.position;
            Collider ballCollider = ballController.GetComponent<Collider>();
            float footY = (ballCollider != null ? ballCollider.bounds.min.y : ballPosition.y) + heightOffset;
            Vector3 start = new Vector3(ballPosition.x, footY, ballPosition.z);

            float length = Mathf.Lerp(minLength, maxLength, preview.pullRatio);
            Vector3 tip = start + direction * length;

            // 矢じりは、先端から後ろ向きに左右へ開いた2本
            Vector3 leftBarb = tip + Quaternion.AngleAxis(180f - headAngle, Vector3.up) * direction * headLength;
            Vector3 rightBarb = tip + Quaternion.AngleAxis(180f + headAngle, Vector3.up) * direction * headLength;

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 5;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, tip);
            lineRenderer.SetPosition(2, leftBarb);
            lineRenderer.SetPosition(3, tip);
            lineRenderer.SetPosition(4, rightBarb);

            lineRenderer.startWidth = startWidth;
            lineRenderer.endWidth = endWidth;

            Color color = GetColor(preview);
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
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
