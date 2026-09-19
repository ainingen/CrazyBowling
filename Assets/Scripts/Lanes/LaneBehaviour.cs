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

        [Header("オイル区画")]
        [Tooltip("手前にオイルを塗る。オンにすると、手前では曲がらず奥で大きく曲がる。" +
                 "オフにすると全域が乾いた床になり、手前から曲がる。")]
        [SerializeField] private bool useOil = true;

        [Tooltip("オイルが終わる位置（レーンの手前からの奥行き・m）。" +
                 "ピンは16.2mにあるので、ここを奥にしすぎると曲がりきらない。")]
        [SerializeField] private float oilEndZ = 10f;

        [Tooltip("オイルから乾いた床へ移り変わる長さ（m）。短いと曲がりが折れ線に見える。")]
        [SerializeField] private float oilTransitionLength = 3f;

        [Tooltip("オイル区画の摩擦の係数。小さいほど曲がらないまま滑る。")]
        [SerializeField] private float oilFriction = 0.03f;

        [Tooltip("乾いた区画の摩擦の係数。")]
        [SerializeField] private float dryFriction = 1f;

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

        /// <summary>
        /// その位置で受ける横向きの加速度を返す。
        /// 回る円盤を持たないレーンは何も答えず false を返し、呼ぶ側は何もしない。
        /// 円盤のあるレーンだけが答える。
        /// </summary>
        public virtual bool TryGetDrift(Vector3 worldPosition, Vector3 worldVelocity, out Vector3 acceleration)
        {
            acceleration = Vector3.zero;
            return false;
        }

        /// <summary>このレーンのオイルの設定。</summary>
        public LaneOilSettings OilSettings => new LaneOilSettings
        {
            enabled = useOil,
            oilEndZ = oilEndZ,
            transitionLength = oilTransitionLength,
            oilFriction = oilFriction,
            dryFriction = dryFriction,
        };

        /// <summary>
        /// その位置の摩擦の効き具合。ボールの曲がりに使う。
        /// レーンの手前からの奥行きで決まるので、傾いたレーンでも同じように働く。
        /// </summary>
        public virtual float GetFrictionScale(Vector3 worldPosition)
        {
            float distance = transform.InverseTransformPoint(worldPosition).z;
            return LaneOil.GetFrictionScale(distance, OilSettings);
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
        /// レーンの毎フレーム更新（物理と同じ間隔）。
        /// 動く障害物や回る円盤は、これを継承して動かす。
        /// 物理に関わるものを動かすので、Update ではなくこちらを使う。
        /// </summary>
        /// <param name="deltaTime">前回からの経過（秒）。</param>
        protected virtual void OnLaneFixedUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// 足りないぶんの重力を加え、レーンごとの毎フレーム更新を呼ぶ。
        /// Physics.gravity を書き換えるとプロジェクト設定に残ってしまうので、そちらは触らない。
        /// </summary>
        private void FixedUpdate()
        {
            OnLaneFixedUpdate(Time.fixedDeltaTime);

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
