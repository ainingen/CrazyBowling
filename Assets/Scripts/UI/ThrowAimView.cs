using System.Collections.Generic;
using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.UI
{
    /// <summary>
    /// ドラッグ中だけ出る、進路の予測線。
    /// カーブの計算は BallCurveModel を実際の投球と共有しているので、
    /// 予測と実際の軌道が同じ式から出る（あくまで近似で、物理の細部までは一致しない）。
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

        [Header("予測線")]
        [Tooltip("線を分割する点の数。多いほど曲線が滑らかになる。")]
        [Range(2, 64)]
        [SerializeField] private int pathStepCount = 24;

        [Tooltip("滑りが減っていく速さの見積もり（m/s を1秒あたり）。" +
                 "予測線がどこまで曲がるかに効く。実際の値は床の摩擦で決まる。")]
        [SerializeField] private float slipDecayRate = 20f;

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

        /// <summary>予測線の点。毎フレーム使い回して、確保し直さないようにする。</summary>
        private readonly List<Vector3> _pathPoints = new List<Vector3>();

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

            bool isDragging = ballController.IsPulling;
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

            // 引き幅で決まる長さを、初速で割って「何秒ぶん描くか」に直す
            float length = Mathf.Lerp(minLength, maxLength, preview.pullRatio);
            float speed = Mathf.Max(preview.speed, 0.01f);
            float duration = length / speed;

            BallCurveSettings curveSettings = ballController.BuildCurveSettings();
            BallCurveModel.PredictPath(
                start,
                direction,
                speed,
                preview.curve * curveSettings.maxSideSpin,
                ballController.EstimateInitialSlip(speed),
                slipDecayRate,
                curveSettings,
                duration,
                pathStepCount,
                _pathPoints);

            if (_pathPoints.Count < 2)
            {
                lineRenderer.enabled = false;
                return;
            }

            // 予測線の最後の向きに合わせて矢じりを付ける
            Vector3 tip = _pathPoints[_pathPoints.Count - 1];
            Vector3 tipDirection = tip - _pathPoints[_pathPoints.Count - 2];
            tipDirection.y = 0f;
            tipDirection = tipDirection.sqrMagnitude > Mathf.Epsilon ? tipDirection.normalized : direction;

            Vector3 leftBarb = tip + Quaternion.AngleAxis(180f - headAngle, Vector3.up) * tipDirection * headLength;
            Vector3 rightBarb = tip + Quaternion.AngleAxis(180f + headAngle, Vector3.up) * tipDirection * headLength;

            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = _pathPoints.Count + 3;
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                lineRenderer.SetPosition(i, _pathPoints[i]);
            }
            lineRenderer.SetPosition(_pathPoints.Count, leftBarb);
            lineRenderer.SetPosition(_pathPoints.Count + 1, tip);
            lineRenderer.SetPosition(_pathPoints.Count + 2, rightBarb);

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
