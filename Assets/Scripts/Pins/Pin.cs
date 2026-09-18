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

        [Tooltip("速さの上限（m/s）。まれに起きる計算の暴走で、ピンが異常な速さで飛ぶのを防ぐ保険。")]
        [SerializeField] private float maxLinearVelocity = 15f;

        [Tooltip("当たり判定の方式。Continuous Speculative は、飛んだピンがすり抜けにくく動きも落ち着きやすい。")]
        [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

        private Rigidbody _rigidbody;
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        /// <summary>立っていたときの位置。</summary>
        public Vector3 InitialPosition => _initialPosition;

        /// <summary>取り除かれずに残っているか。</summary>
        public bool IsStandingInPlay => gameObject.activeSelf;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            ApplyPhysicsSettings();
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
        }
    }
}
