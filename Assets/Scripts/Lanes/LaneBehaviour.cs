using UnityEngine;
using CrazyBowling.Ball;
using CrazyBowling.Data;
using CrazyBowling.Pins;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンに渡される、共通の持ち物へのつなぎ。
    /// レーン側から GameObject.Find をしなくて済むようにする。
    /// </summary>
    public struct LaneContext
    {
        /// <summary>ボールの操作。</summary>
        public BallController ball;

        /// <summary>ボールの当たり判定。摩擦を変えるときに使う。</summary>
        public Collider ballCollider;

        /// <summary>ピン10本。</summary>
        public PinSet pinSet;

        /// <summary>このレーンの設定。</summary>
        public LaneData data;
    }

    /// <summary>
    /// レーンごとの振る舞い。レーンのプレハブのルートに付ける。
    /// 回る円盤や動く障害物は、これを継承して作る。
    /// 何もしないレーンでも、重力や摩擦を変えたいならこれを付ける。
    /// </summary>
    public class LaneBehaviour : MonoBehaviour
    {
        [Header("物理の癖")]
        [Tooltip("重力の倍率。1で普通、0.4で低重力。" +
                 "プロジェクト設定は触らず、ボールとピンに足りないぶんの力を毎回加えて再現する。")]
        [SerializeField] private float gravityScale = 1f;

        [Tooltip("ボールと床の摩擦を上書きするか。オフなら Prefab のマテリアルのまま。")]
        [SerializeField] private bool overrideBallFriction = false;

        [Tooltip("上書きするときの摩擦。小さいほどよく滑る。")]
        [SerializeField] private float ballFriction = 0.2f;

        /// <summary>このレーンにいる間の持ち物。</summary>
        protected LaneContext Context { get; private set; }

        /// <summary>差し替える前のマテリアル。レーンを出るときに戻す。</summary>
        private PhysicsMaterial _originalBallMaterial;

        /// <summary>実行時にだけ作ったマテリアル。アセットは書き換えない。</summary>
        private PhysicsMaterial _runtimeBallMaterial;

        /// <summary>重力を足す相手。毎回集め直さないように覚えておく。</summary>
        private Rigidbody[] _gravityTargets;

        /// <summary>レーンに入った。アンカーを読まれる前に呼ばれるので、ここで形を決めてよい。</summary>
        public virtual void OnLaneStart(LaneContext context)
        {
            Context = context;
            ApplyBallFriction();
            CollectGravityTargets();
        }

        /// <summary>投げた瞬間。</summary>
        public virtual void OnThrowStart()
        {
        }

        /// <summary>1投が決着した瞬間。</summary>
        public virtual void OnThrowEnd()
        {
        }

        /// <summary>
        /// その位置の床の高さと向きを返す。
        /// 平らなレーンは床の形を知らないので false を返し、呼ぶ側は何もしない。
        /// 起伏のあるレーンだけが答える。
        /// </summary>
        public virtual bool TrySampleFloor(Vector3 worldPosition, out float height, out Vector3 normal)
        {
            height = worldPosition.y;
            normal = Vector3.up;
            return false;
        }

        /// <summary>レーンを出る。借りたものはここで返す。</summary>
        public virtual void OnLaneEnd()
        {
            RestoreBallFriction();
        }

        private void OnDestroy()
        {
            // レーンごと消されたときも、マテリアルは必ず戻す
            RestoreBallFriction();
        }

        /// <summary>
        /// 足りないぶんの重力を加える。
        /// Physics.gravity を書き換えるとプロジェクト設定に残ってしまうので、そちらは触らない。
        /// </summary>
        private void FixedUpdate()
        {
            if (Mathf.Approximately(gravityScale, 1f) || _gravityTargets == null)
            {
                return;
            }

            Vector3 extra = Physics.gravity * (gravityScale - 1f);
            foreach (Rigidbody body in _gravityTargets)
            {
                if (body == null || body.isKinematic)
                {
                    continue;
                }
                body.AddForce(extra, ForceMode.Acceleration);
            }
        }

        /// <summary>重力を足す相手（ボールとピン）を集める。</summary>
        private void CollectGravityTargets()
        {
            if (Mathf.Approximately(gravityScale, 1f))
            {
                _gravityTargets = null;
                return;
            }

            var targets = new System.Collections.Generic.List<Rigidbody>();

            if (Context.ball != null)
            {
                Rigidbody ballBody = Context.ball.GetComponent<Rigidbody>();
                if (ballBody != null)
                {
                    targets.Add(ballBody);
                }
            }

            if (Context.pinSet != null)
            {
                targets.AddRange(Context.pinSet.GetComponentsInChildren<Rigidbody>(true));
            }

            _gravityTargets = targets.ToArray();
        }

        /// <summary>
        /// ボールの摩擦を差し替える。
        /// 実行時にだけ作った複製を使うので、Project のマテリアルは書き換わらない。
        /// </summary>
        private void ApplyBallFriction()
        {
            if (!overrideBallFriction || Context.ballCollider == null)
            {
                return;
            }

            _originalBallMaterial = Context.ballCollider.sharedMaterial;

            _runtimeBallMaterial = _originalBallMaterial != null
                ? Instantiate(_originalBallMaterial)
                : new PhysicsMaterial();
            _runtimeBallMaterial.name = "ボール（このレーンだけ）";
            _runtimeBallMaterial.dynamicFriction = ballFriction;
            _runtimeBallMaterial.staticFriction = ballFriction;

            Context.ballCollider.sharedMaterial = _runtimeBallMaterial;
        }

        /// <summary>差し替えた摩擦を元に戻す。</summary>
        private void RestoreBallFriction()
        {
            if (_runtimeBallMaterial == null)
            {
                return;
            }

            if (Context.ballCollider != null)
            {
                Context.ballCollider.sharedMaterial = _originalBallMaterial;
            }

            Destroy(_runtimeBallMaterial);
            _runtimeBallMaterial = null;
            _originalBallMaterial = null;
        }
    }
}
