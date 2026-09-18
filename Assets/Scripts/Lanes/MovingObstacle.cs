using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンの上を往復する障害物。
    ///
    /// Transform を直接動かすと、PhysX は瞬間移動として扱うので
    /// ボールをすり抜けたり、めり込んでから弾き返したりする。
    /// kinematic な Rigidbody を MovePosition で動かすと、
    /// PhysX が「動いているもの」として当たりを解いてくれる。
    ///
    /// 動く向きを変えれば、左右の壁にも上下の柵にも使える。
    /// 自分では時間を数えず、レーンから Step を呼んでもらう。
    /// レーンが止まっているときに勝手に動かないようにするため。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MovingObstacle : MonoBehaviour
    {
        [Header("動き")]
        [Tooltip("動く向き（レーンから見た向き）。右へ動かすなら X に1。長さは見ない。")]
        [SerializeField] private Vector3 axis = Vector3.right;

        [Tooltip("中心からの振れ幅（片側・m）。レーンの幅は1.05mなので、" +
                 "幅0.35mの壁なら0.35で端から端まで届く。")]
        [SerializeField] private float travel = 0.35f;

        [Tooltip("一往復にかかる時間（秒）。短いほど速い。")]
        [SerializeField] private float period = 2.5f;

        [Tooltip("開始位置のずらし。0で中心から始まり、0.25で片端から始まる。" +
                 "障害物を複数置くとき、ずらすと互い違いに動く。")]
        [SerializeField] private float phase = 0f;

        /// <summary>動かす本体。</summary>
        private Rigidbody _rigidbody;

        /// <summary>動く前の位置（親から見た位置）。ここを中心に往復する。</summary>
        private Vector3 _origin;

        /// <summary>置き場所を覚えたか。</summary>
        private bool _ready;

        /// <summary>今の設定。</summary>
        public ObstacleMotionSettings Settings => new ObstacleMotionSettings
        {
            travel = travel,
            period = period,
            phase = phase,
        };

        /// <summary>動きが一番速いときの速さ（m/秒）。</summary>
        public float PeakSpeed => ObstacleMotion.GetPeakSpeed(Settings);

        /// <summary>出発点からの今のずれ（m）。見た目を合わせるのに使う。</summary>
        public float Offset { get; private set; }

        /// <summary>今の速さ（m/秒。動く向きに沿った符号つき）。見た目を合わせるのに使う。</summary>
        public float AxisVelocity { get; private set; }

        /// <summary>出発してから進んだ道のり（m）。往復ぶんを足し続ける。</summary>
        public float TravelledDistance { get; private set; }

        /// <summary>動く向き（このものから見た向き）。</summary>
        public Vector3 LocalAxis => axis.sqrMagnitude > Mathf.Epsilon ? axis.normalized : Vector3.right;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            // 当たりは取るが、ボールに押されて動いてほしくはないので kinematic にする
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;

            // kinematic 用の連続判定。速いボールとの間ですり抜けを防ぐ
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // 物理は50回／秒なので、そのままだと見た目がかくつく
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            _origin = transform.localPosition;
            _ready = true;
        }

        /// <summary>
        /// 時刻に合わせて動かす。レーンの FixedUpdate から呼ぶ。
        /// </summary>
        /// <param name="time">レーンに入ってからの経過（秒）。</param>
        public void Step(float time)
        {
            if (!_ready)
            {
                return;
            }

            UpdateMotionState(time);
            _rigidbody.MovePosition(GetWorldPosition(time));
        }

        /// <summary>
        /// 時刻の位置へ、当たりを解かずにそのまま置く。
        /// レーンに入った瞬間など、移動として扱われたくないときに使う。
        /// </summary>
        public void Warp(float time)
        {
            if (!_ready)
            {
                return;
            }

            Offset = ObstacleMotion.GetOffset(time, Settings);
            AxisVelocity = 0f;
            TravelledDistance = 0f;

            _rigidbody.position = GetWorldPosition(time);
            transform.position = _rigidbody.position;
        }

        /// <summary>
        /// 今どこにいて、どちらへどれだけ速く動いているかを控えておく。
        /// 見た目の側（滑り歩きなど）がこれを読む。
        /// </summary>
        private void UpdateMotionState(float time)
        {
            float offset = ObstacleMotion.GetOffset(time, Settings);
            float moved = offset - Offset;

            AxisVelocity = Time.fixedDeltaTime > Mathf.Epsilon ? moved / Time.fixedDeltaTime : 0f;
            TravelledDistance += Mathf.Abs(moved);
            Offset = offset;
        }

        /// <summary>その時刻の置き場所（ワールド座標）。</summary>
        private Vector3 GetWorldPosition(float time)
        {
            Vector3 direction = axis.sqrMagnitude > Mathf.Epsilon ? axis.normalized : Vector3.right;
            Vector3 local = _origin + direction * ObstacleMotion.GetOffset(time, Settings);

            return transform.parent != null
                ? transform.parent.TransformPoint(local)
                : local;
        }
    }
}
