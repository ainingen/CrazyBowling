using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Core
{
    /// <summary>
    /// カメラの制御。構え中は決まった位置で待ち、投球後はボールを追う。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("対象")]
        [Tooltip("追いかける対象（ボール）。")]
        [SerializeField] private Transform target;

        [Tooltip("状態を見るための BallController。構え中と転がり中を切り替える。")]
        [SerializeField] private BallController ballController;

        [Header("構え中")]
        [Tooltip("構え中のカメラ位置（ワールド座標）。")]
        [SerializeField] private Vector3 aimingPosition = new Vector3(0f, 1.6f, -2.5f);

        [Tooltip("構え中のカメラの角度（度）。")]
        [SerializeField] private Vector3 aimingEulerAngles = new Vector3(12f, 0f, 0f);

        [Header("追従中")]
        [Tooltip("ボールからどれだけ離れて追うか（ワールド座標のずれ）。")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 1.5f, -3f);

        [Tooltip("位置の追従の滑らかさ。小さいほど機敏に動く（秒）。")]
        [SerializeField] private float followSmoothTime = 0.2f;

        [Tooltip("常にボールの方を向くか。")]
        [SerializeField] private bool lookAtTarget = true;

        [Tooltip("向きの追従の速さ。大きいほど機敏に向く。")]
        [SerializeField] private float rotationSmoothSpeed = 10f;

        private Vector3 _positionVelocity;

        /// <summary>
        /// 下見カメラなど、外からカメラを動かしている間は true。
        /// この間は通常の追従を止めて、位置と向きに手を出さない。
        /// </summary>
        public bool ExternalControl { get; set; }

        /// <summary>
        /// 追従中のずれを、レーンの都合で上書きする。null なら Inspector の followOffset を使う。
        /// 輪をくぐるレーンなどで、カメラが見た目の中を通り抜けないようにするため。
        /// 使うレーンは、入ったときに入れて、出るときに null へ戻すこと（前の値は覚えない）。
        /// </summary>
        public Vector3? FollowOffsetOverride { get; set; }

        /// <summary>
        /// 構え中の視点を、レーンの目印に合わせる。GameManager がレーンを差し替えたときに呼ぶ。
        /// レーンをまたぐときは滑らかに動かす意味が無いので、その場で切り替える。
        /// </summary>
        public void SetAimingView(Transform anchor, bool snap = true)
        {
            if (anchor == null)
            {
                return;
            }

            aimingPosition = anchor.position;
            aimingEulerAngles = anchor.eulerAngles;

            if (!snap)
            {
                return;
            }

            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            _positionVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (ExternalControl)
            {
                return;
            }

            // 決着後も判定が終わるまでは奥に留まり、倒れたピンが見えるようにする
            bool isInPlay = ballController != null && ballController.IsInPlay;

            if (!isInPlay)
            {
                UpdateAimingView();
                return;
            }

            UpdateFollowView();
        }

        /// <summary>構え中：決まった位置と角度へ戻る。</summary>
        private void UpdateAimingView()
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, aimingPosition, ref _positionVelocity, followSmoothTime);

            SmoothLookTo(Quaternion.Euler(aimingEulerAngles));
        }

        /// <summary>追従中：ボールを追いかける。</summary>
        private void UpdateFollowView()
        {
            if (target == null)
            {
                return;
            }

            Vector3 offset = FollowOffsetOverride ?? followOffset;
            Vector3 desiredPosition = target.position + offset;
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref _positionVelocity, followSmoothTime);

            if (!lookAtTarget)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            if (toTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            SmoothLookTo(Quaternion.LookRotation(toTarget));
        }

        /// <summary>フレームレートに左右されにくい形で目標の向きへ近づける。</summary>
        private void SmoothLookTo(Quaternion targetRotation)
        {
            float t = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
    }
}
