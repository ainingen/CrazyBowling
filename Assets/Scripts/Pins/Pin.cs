using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// ピン1本。立っていたときの姿勢を覚えておき、リセットで戻す。
    /// 物理はこの親が持ち、見た目は子の Visual が持つ。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Pin : MonoBehaviour
    {
        [Header("倒れやすさ")]
        [Tooltip("重心の高さ（m）。低いほど倒れにくくなる。倒れやすさの主な調整項目。実物のピンは約0.14。")]
        [SerializeField] private float centerOfMassHeight = 0.13f;

        [Header("物理の上限")]
        [Tooltip("回転の速さの上限（rad/s）。小さいと、当たったときの回り方が鈍くなる。")]
        [SerializeField] private float maxAngularVelocity = 50f;

        [Tooltip("速さの上限（m/s）。まれに起きる計算の暴走で、ピンが異常な速さで飛ぶのを防ぐ保険。" +
                 "上げると飛距離は伸びるが、デッキの外へ出ていくピンが増えて滞空時間はむしろ短くなる。")]
        [SerializeField] private float maxLinearVelocity = 15f;

        [Tooltip("当たり判定の方式。Continuous Speculative は、飛んだピンがすり抜けにくく、" +
                 "Continuous Dynamic より倒れ方も浮き方も良い（実測で確認済み）。")]
        [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

        [Header("場外へ飛んだピン")]
        [Tooltip("立っていた場所からこの距離を超えたら、物理を止めて見た目を隠す（m）。" +
                 "遠くまで転がり続けて判定を待たせないようにするため。")]
        [SerializeField] private float cullDistance = 4f;

        [Tooltip("この高さより下まで落ちたら、同じように物理を止めて隠す（m）。")]
        [SerializeField] private float cullBelowY = -1.5f;

        private Rigidbody _rigidbody;
        private Transform _visual;
        private PinExplosion _explosion;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        /// <summary>この投球で既に爆発の起点になったか。1投につき1回にするために使う。</summary>
        private bool _hasExploded;

        /// <summary>この投球で、満威力の爆発（世代0）の起点になったか。</summary>
        private bool _isBlastOrigin;

        /// <summary>
        /// 連鎖の何回目で飛ばされたか。-1 は「まだ飛ばされていない」。
        /// 0 を「未設定」と兼用すると、起点のピンの世代が後から上書きされてしまう。
        /// </summary>
        private int _blastGeneration = -1;

        /// <summary>場外へ飛んだので物理を止めたか。</summary>
        private bool _culled;

        /// <summary>立っていたときの位置。</summary>
        public Vector3 InitialPosition => _initialPosition;

        /// <summary>取り除かれずに残っているか。</summary>
        public bool IsStandingInPlay => gameObject.activeSelf;

        /// <summary>この投球で既に爆発の起点になったか。</summary>
        public bool HasExploded => _hasExploded;

        /// <summary>
        /// 次にこのピンが爆発するときの世代。まだ飛ばされていなければ0（＝起点になれる）。
        /// </summary>
        public int BlastGeneration => _blastGeneration < 0 ? 0 : _blastGeneration;

        /// <summary>この投球で、満威力の爆発の起点になったか。</summary>
        public bool IsBlastOrigin => _isBlastOrigin;

        /// <summary>吹き飛ばす対象にできるか。取り除かれたピンと場外のピンは対象外。</summary>
        public bool CanBeBlasted => gameObject.activeSelf && !_culled && !_rigidbody.isKinematic;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _visual = transform.Find("Visual");
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            ApplyPhysicsSettings();
        }

        /// <summary>PinExplosion が Awake で自分を通知先として登録する。</summary>
        public void SetExplosion(PinExplosion explosion)
        {
            _explosion = explosion;
        }

        /// <summary>
        /// ぶつかった勢いを見て、連鎖爆発の演出に知らせる。
        /// 床や壁は Rigidbody を持たないので、ピン同士とボールだけが対象になる。
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_explosion == null || !_explosion.IsEnabled || _culled)
            {
                return;
            }
            if (collision.rigidbody == null || collision.contactCount == 0)
            {
                return;
            }

            _explosion.ReportImpact(this, CalculateImpactSpeed(collision), collision.rigidbody);
        }

        /// <summary>
        /// ぶつかった強さ。接触面の法線方向の成分だけを見る。
        /// 速度の大きさをそのまま使うと、かすった当たりでも「強い衝突」になってしまい、
        /// 厚く当たった場合と区別できない。
        /// </summary>
        private static float CalculateImpactSpeed(Collision collision)
        {
            Vector3 normal = collision.GetContact(0).normal;
            return Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
        }

        /// <summary>
        /// 場外まで飛んだら物理を止めて見た目を隠す。
        /// 毎回の物理ステップで PinSet から呼ばれる。
        /// </summary>
        public void UpdateOutOfPlayCulling()
        {
            EnsureRigidbody();
            if (_culled || _rigidbody == null || _rigidbody.isKinematic)
            {
                return;
            }

            Vector3 moved = transform.position - _initialPosition;
            bool tooFar = moved.sqrMagnitude > cullDistance * cullDistance;
            bool tooLow = transform.position.y < cullBelowY;
            if (tooFar || tooLow)
            {
                Cull();
            }
        }

        /// <summary>
        /// 場外のピンを止める。GameObject は有効なままにするのが要点で、
        /// こうしておくと倒れ判定が「デッキから出た」として数えてくれる。
        /// 消してしまうと本数から漏れる。
        /// </summary>
        private void Cull()
        {
            _culled = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
            if (_visual != null)
            {
                _visual.gameObject.SetActive(false);
            }
        }

        /// <summary>爆発の起点になったことを記録する。1投につき1回にするため。</summary>
        public void MarkExploded(int generation)
        {
            _hasExploded = true;
            if (generation == 0)
            {
                _isBlastOrigin = true;
            }
        }

        /// <summary>
        /// 爆発で吹き飛ばされる。速度と回転を足し、連鎖の世代を受け取る。
        /// </summary>
        public void ApplyBlast(Vector3 velocityChange, Vector3 angularVelocityChange, int generation)
        {
            EnsureRigidbody();
            if (_culled || _rigidbody.isKinematic)
            {
                return;
            }

            _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
            _rigidbody.angularVelocity += angularVelocityChange;

            // 先に浅い世代で飛ばされていたら、そちらを残す（威力が強いほうの記録）
            if (_blastGeneration < 0 || generation < _blastGeneration)
            {
                _blastGeneration = generation;
            }
        }

        /// <summary>
        /// 1投ごとの記録を消す。位置は動かさない。
        /// 2投目に入る前に PinSet から呼ばれる。
        /// </summary>
        public void ClearThrowState()
        {
            _hasExploded = false;
            _isBlastOrigin = false;
            _blastGeneration = -1;
        }

        /// <summary>PinSet が並べ直したあとに、その場所を初期姿勢として覚える。</summary>
        public void SetInitialPose(Vector3 position, Quaternion rotation)
        {
            _initialPosition = position;
            _initialRotation = rotation;

            EnsureRigidbody();
            _rigidbody.position = position;
            _rigidbody.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>今の状態を観測値にまとめる。</summary>
        public PinSample CreateSample()
        {
            EnsureRigidbody();
            return new PinSample
            {
                up = transform.up,
                position = transform.position,
                initialPosition = _initialPosition,
                linearSpeed = _rigidbody.linearVelocity.magnitude,
                angularSpeed = _rigidbody.angularVelocity.magnitude,
            };
        }

        /// <summary>
        /// 取り除く。Destroy はせず、非表示にして物理を止めるだけにする。
        /// リセットで元に戻せるようにするため。
        /// </summary>
        public void Deactivate()
        {
            EnsureRigidbody();
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
            gameObject.SetActive(false);
        }

        /// <summary>立っていた場所に戻し、再び物理で動くようにする。</summary>
        public void ResetToInitial()
        {
            gameObject.SetActive(true);
            EnsureRigidbody();

            // 場外で止めていた場合は、物理と見た目を戻してから位置を書く
            _culled = false;
            _rigidbody.isKinematic = false;
            if (_visual != null)
            {
                _visual.gameObject.SetActive(true);
            }
            ClearThrowState();

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.position = _initialPosition;
            _rigidbody.rotation = _initialRotation;
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);

            ApplyPhysicsSettings();
        }

        /// <summary>
        /// Inspector の値を Rigidbody に反映する。立て直すたびに呼ぶので、
        /// 再生中に Inspector で値を変えると、次にピンを並べ直した時点から効く。
        /// </summary>
        private void ApplyPhysicsSettings()
        {
            EnsureRigidbody();

            // 重心を下げる。実物のピンは下が重く、少し傾いても立ち直る
            _rigidbody.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);

            _rigidbody.maxAngularVelocity = maxAngularVelocity;
            _rigidbody.maxLinearVelocity = maxLinearVelocity;
            _rigidbody.collisionDetectionMode = collisionDetection;
        }

        /// <summary>Awake より先に呼ばれても動くようにする。</summary>
        private void EnsureRigidbody()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }
            if (_visual == null)
            {
                _visual = transform.Find("Visual");
            }
        }
    }
}
