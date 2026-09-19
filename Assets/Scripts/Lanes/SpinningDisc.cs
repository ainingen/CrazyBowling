using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンの床に埋め込まれた、回る円盤。
    ///
    /// **Collider も Rigidbody も持たない。** ボールを流す力はスクリプトで加えるので、
    /// 当たり判定が要らない。おかげで Obstacle の物理マテリアル（摩擦0.05）に
    /// 一切触れずに済み、MOVING WALL の物理をそのまま残せる。
    ///
    /// 当たり判定が無いので、回転は Transform を直接回してよい。
    /// MovingObstacle のコメントにある「Transform を直接動かすな」は、
    /// 当たり判定を持つものの話（すり抜けとめり込みが起きる）なので、ここには当てはまらない。
    ///
    /// 自分では時間を数えず、レーンから Step を呼んでもらう。
    /// レーンが止まっているときに勝手に回らないようにするため。
    /// </summary>
    public class SpinningDisc : MonoBehaviour
    {
        [Header("大きさ")]
        [Tooltip("円盤の半径（m）。レーンの幅は1.05mなので、0.5でほぼ全幅になる。")]
        [SerializeField] private float radius = 0.5f;

        [Tooltip("外周で効きが弱まる幅（m）。0にすると円盤の中は一律で効く。" +
                 "境目でいきなり効き始めると、ボールが折れ線に曲がって見える。")]
        [SerializeField] private float edgeSoftness = 0.05f;

        [Header("回り方")]
        [Tooltip("一回転にかかる時間（秒）。負にすると逆回りになる。0なら止まる。")]
        [SerializeField] private float period = 1f;

        [Tooltip("開始角度のずらし（1で一回転ぶん）。円盤を複数置くとき、ずらすと向きが揃わない。")]
        [SerializeField] private float phase = 0f;

        [Header("効き具合")]
        [Tooltip("ボールを横へ流す強さ。0で流れない。" +
                 "横に付く速度は「この値 × 回る速さ(rad/s) × 円盤を横切った長さ(m)」になり、" +
                 "ボールの速さには左右されない。")]
        [SerializeField] private float driftGain = 0.075f;

        /// <summary>置かれたときの向き（親から見た向き）。ここを基準に回す。</summary>
        private Quaternion _origin;

        /// <summary>向きを覚えたか。</summary>
        private bool _ready;

        /// <summary>今の設定。横へ流す計算に渡す。</summary>
        public SpinningDiscSettings Settings => new SpinningDiscSettings
        {
            radius = radius,
            period = period,
            driftGain = driftGain,
            edgeSoftness = edgeSoftness,
        };

        /// <summary>回る速さ（ラジアン/秒）。正で上から見て反時計回り。</summary>
        public float AngularSpeed => ObstacleMotion.GetAngularSpeed(period);

        /// <summary>円盤の半径（m）。置き場所の確認に使う。</summary>
        public float Radius => radius;

        /// <summary>今の回転角（度）。見た目の確認に使う。</summary>
        public float Angle { get; private set; }

        private void Awake()
        {
            _origin = transform.localRotation;
            _ready = true;
        }

        /// <summary>
        /// 時刻に合わせて回す。レーンの FixedUpdate から呼ぶ。
        /// </summary>
        /// <param name="time">レーンに入ってからの経過（秒）。</param>
        public void Step(float time)
        {
            if (!_ready)
            {
                return;
            }

            Angle = ObstacleMotion.GetAngle(time, period, phase);

            // 上から見て回すので、縦軸（Y）まわり。
            // 反時計回りを正にするため、Y の回転は符号を反転する
            transform.localRotation = _origin * Quaternion.Euler(0f, -Angle, 0f);
        }

        /// <summary>時刻の向きへそのまま戻す。レーンに入った瞬間に使う。</summary>
        public void ResetTo(float time)
        {
            Step(time);
        }
    }
}
