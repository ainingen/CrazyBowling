using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 動く障害物の見た目を、滑り歩きしている人型にする。
    ///
    /// 当たり判定には一切触らない。親の BoxCollider はそのまま。
    /// 見た目の子だけを動かすので、あとで市販のモデルに差し替えても物理は変わらない。
    ///
    /// 人型は進行方向の逆を向く。前へ歩いているのに後ろへ滑る、という形にするため。
    /// </summary>
    public class MoonwalkVisual : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("動きを読む相手。空なら親から探す。")]
        [SerializeField] private MovingObstacle obstacle;

        [Tooltip("向きを変えるもの。人型のまとまりを入れる。")]
        [SerializeField] private Transform figure;

        [Tooltip("上下に揺らすもの。腰から上のまとまりを入れる。")]
        [SerializeField] private Transform bob;

        [Header("足")]
        [Tooltip("左の太もも。")]
        [SerializeField] private Transform leftThigh;

        [Tooltip("左のすね。")]
        [SerializeField] private Transform leftShin;

        [Tooltip("右の太もも。")]
        [SerializeField] private Transform rightThigh;

        [Tooltip("右のすね。")]
        [SerializeField] private Transform rightShin;

        [Header("腕")]
        [Tooltip("左の腕。")]
        [SerializeField] private Transform leftArm;

        [Tooltip("右の腕。")]
        [SerializeField] private Transform rightArm;

        [Header("歩き方")]
        [Tooltip("一歩の長さ（m）。短いほど足がせわしなく動く。" +
                 "歩きは時間ではなく進んだ距離で進むので、端で遅くなると足も遅くなる。")]
        [SerializeField] private float strideLength = 0.16f;

        [Tooltip("太ももを前後に振る角度（度）。")]
        [SerializeField] private float thighSwing = 34f;

        [Tooltip("膝を曲げる角度（度）。")]
        [SerializeField] private float kneeBend = 42f;

        [Tooltip("腕を前後に振る角度（度）。")]
        [SerializeField] private float armSwing = 22f;

        [Tooltip("腰が沈む深さ（m）。")]
        [SerializeField] private float bobAmount = 0.012f;

        [Header("向き")]
        [Tooltip("人型が正面を向いているときの、体の向き（度）。" +
                 "動く向きが横（X）なので、既定では90度回して横を向かせる。")]
        [SerializeField] private float facingYaw = 90f;

        [Tooltip("この速さ以下では向きを変えない（m/秒）。端で折り返すときに向きが暴れないようにする。")]
        [SerializeField] private float facingDeadZone = 0.10f;

        [Tooltip("向きを変えるのにかける時間（秒）。端で止まっている間に振り向く。")]
        [SerializeField] private float turnSeconds = 0.30f;

        /// <summary>今向いている側。1か−1。</summary>
        private float _facingSign = 1f;

        /// <summary>振り向く途中の度合い。−1から1へ動き、これに facingYaw を掛けて向きにする。</summary>
        private float _facingBlend = 1f;

        /// <summary>置く前の腕と足の向き。ここからの差で動かす。</summary>
        private Quaternion _leftThighRest, _leftShinRest, _rightThighRest, _rightShinRest;
        private Quaternion _leftArmRest, _rightArmRest;

        /// <summary>揺らす前の腰の位置。</summary>
        private Vector3 _bobRest;

        private void Awake()
        {
            if (obstacle == null)
            {
                obstacle = GetComponentInParent<MovingObstacle>();
            }

            if (leftThigh != null) _leftThighRest = leftThigh.localRotation;
            if (leftShin != null) _leftShinRest = leftShin.localRotation;
            if (rightThigh != null) _rightThighRest = rightThigh.localRotation;
            if (rightShin != null) _rightShinRest = rightShin.localRotation;
            if (leftArm != null) _leftArmRest = leftArm.localRotation;
            if (rightArm != null) _rightArmRest = rightArm.localRotation;
            if (bob != null) _bobRest = bob.localPosition;
        }

        /// <summary>
        /// 見た目だけなので、物理と同じ間隔ではなく描画に合わせて動かす。
        /// </summary>
        private void LateUpdate()
        {
            if (obstacle == null)
            {
                return;
            }

            UpdateFacing();
            UpdateLimbs();
        }

        /// <summary>進行方向の逆を向く。端で止まっている間にゆっくり振り向く。</summary>
        private void UpdateFacing()
        {
            _facingSign = MoonwalkPose.GetFacingSign(obstacle.AxisVelocity, _facingSign, facingDeadZone);

            if (figure == null)
            {
                return;
            }

            // 右向きと左向きはちょうど180度差なので、角度のまま補間すると
            // どちら回りになるか定まらない。−1から1の値を動かして向きに直す。
            // 途中の0で正面（投げる人の側）を向くので、その場で向き直る動きになる
            float step = turnSeconds > Mathf.Epsilon
                ? Time.deltaTime / turnSeconds * 2f
                : 2f;
            _facingBlend = Mathf.MoveTowards(_facingBlend, _facingSign, step);

            figure.localRotation = Quaternion.Euler(0f, facingYaw * _facingBlend, 0f);
        }

        /// <summary>進んだ距離に合わせて手足を動かす。</summary>
        private void UpdateLimbs()
        {
            float phase = MoonwalkPose.GetPhase(obstacle.TravelledDistance, strideLength);

            // 太ももと腕は「正で前」、膝は「正で後ろへ曲がる」。
            // Apply は後ろ向きが正なので、前に出すものは符号を返す
            Apply(leftThigh, _leftThighRest, -MoonwalkPose.GetThighAngle(phase, thighSwing, false));
            Apply(rightThigh, _rightThighRest, -MoonwalkPose.GetThighAngle(phase, thighSwing, true));
            Apply(leftShin, _leftShinRest, MoonwalkPose.GetKneeAngle(phase, kneeBend, false));
            Apply(rightShin, _rightShinRest, MoonwalkPose.GetKneeAngle(phase, kneeBend, true));
            Apply(leftArm, _leftArmRest, -MoonwalkPose.GetArmAngle(phase, armSwing, false));
            Apply(rightArm, _rightArmRest, -MoonwalkPose.GetArmAngle(phase, armSwing, true));

            if (bob != null)
            {
                Vector3 position = _bobRest;
                position.y += MoonwalkPose.GetBob(phase, bobAmount);
                bob.localPosition = position;
            }
        }

        /// <summary>
        /// 置いたときの向きを基準に、X軸まわりで曲げる。
        /// 人型は自分から見て +Z を向いているので、正の角度で手足は後ろへ振れる。
        /// </summary>
        private static void Apply(Transform joint, Quaternion rest, float degrees)
        {
            if (joint != null)
            {
                joint.localRotation = rest * Quaternion.Euler(degrees, 0f, 0f);
            }
        }
    }
}
