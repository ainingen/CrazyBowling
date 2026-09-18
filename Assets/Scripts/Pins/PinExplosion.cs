using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// ピンの連鎖爆発の演出。Pins オブジェクトに付ける。
    /// 強い衝突を受けたピンを起点に、周りのピンへ速度と回転を足す。
    /// 飛ばされたピンが次のピンにぶつかると、また衝突として検知されて連鎖する。
    /// 物理の値だけで派手さを出すとピンが止まらなくなるため、演出として足している。
    /// </summary>
    public class PinExplosion : MonoBehaviour
    {
        [Header("演出")]
        [Tooltip("切ると素の物理だけになる。比べるときに使う。")]
        [SerializeField] private bool enableEffect = true;

        [Header("起点の条件")]
        [Tooltip("ヘッドピン。起点になれる横ずれを、このピンの立っていた位置から測る。")]
        [SerializeField] private Pin headPin;

        [Tooltip("起点はボールとの衝突だけにする。切ると、ピン同士がたまたま強くぶつかっただけでも" +
                 "満威力の爆発が起きてしまい、厚く当てたかどうかと関係なく発動する。")]
        [SerializeField] private bool originFromBallOnly = true;

        [Tooltip("起点になれる横ずれの下限（m）。0なら、ど真ん中に当たっても爆発する。")]
        [SerializeField] private float originOffsetMin = 0f;

        [Tooltip("起点になれる横ずれの上限（m）。0.12 なら12cm。" +
                 "これより薄い当たりでは爆発しない。厚い／薄いを分ける主な調整項目。")]
        [SerializeField] private float originOffsetMax = 0.12f;

        [Tooltip("上限の手前、威力が落ち始める幅（m）。0.03 なら 9cm から 12cm にかけて弱くなる。" +
                 "0にすると境界で急に爆発が消える。")]
        [SerializeField] private float originFalloffWidth = 0.03f;

        [Header("発動")]
        [Tooltip("これ以上の勢いでぶつかられたら発動する（m/s）。" +
                 "厚い／薄いの区別は横ずれが担うので、ここは弱い球を弾くためだけに使う。")]
        [SerializeField] private float triggerSpeed = 6f;

        [Tooltip("連鎖の最大回数。最初の爆発が0回目。小さいほど端のピンが残りやすい。")]
        [SerializeField] private int maxChainCount = 4;

        [Tooltip("1投につき、満威力の爆発（起点）を1か所だけにする。" +
                 "切るとボールが触れたピンのたびに満威力で爆発し、薄く当たっても全部倒れてしまう。")]
        [SerializeField] private bool oneOriginPerThrow = true;

        [Header("威力")]
        [Tooltip("吹き飛ばす力。速度の変化量として与える（m/s）。派手さの主な調整項目。")]
        [SerializeField] private float force = 7f;

        [Tooltip("上向き成分の割合。0で真横に飛び、大きいほど高く浮く。")]
        [Range(0f, 2f)]
        [SerializeField] private float upwardRatio = 0.5f;

        [Tooltip("効果が及ぶ範囲（m）。ピンの間隔は0.30なので、0.45なら隣のピンまで届く。")]
        [SerializeField] private float radius = 0.45f;

        [Tooltip("連鎖するたびに威力へ掛ける率。小さいほど早く連鎖が弱まる。")]
        [Range(0.1f, 1f)]
        [SerializeField] private float chainFalloff = 0.6f;

        [Tooltip("追加する回転の強さ（rad/s）。ピンの回転上限を超える分は切り捨てられる。")]
        [SerializeField] private float spinStrength = 25f;

        [Header("デバッグ")]
        [Tooltip("爆発のたびに Console に出す。")]
        [SerializeField] private bool logEvents = false;

        private Pin[] _pins;

        /// <summary>ピンの Rigidbody。衝突の相手がピンかボールかを見分けるために持つ。</summary>
        private readonly System.Collections.Generic.HashSet<int> _pinBodyIds =
            new System.Collections.Generic.HashSet<int>();

        /// <summary>この投球で、満威力の爆発（起点）が既に起きたか。</summary>
        private bool _originUsed;

        /// <summary>演出が有効か。ピン側が発動前に確認する。</summary>
        public bool IsEnabled => enableEffect;

        /// <summary>1投ごとの記録を消す。PinSet から呼ばれる。</summary>
        public void ClearThrowState()
        {
            _originUsed = false;
        }

        private void Awake()
        {
            // 子のピンを集めて、自分を通知先として登録する
            _pins = GetComponentsInChildren<Pin>(true);
            for (int i = 0; i < _pins.Length; i++)
            {
                _pins[i].SetExplosion(this);

                var body = _pins[i].GetComponent<Rigidbody>();
                if (body != null)
                {
                    _pinBodyIds.Add(body.GetInstanceID());
                }
            }

            // Inspector で指定がなければ、一番手前のピンをヘッドピンとみなす
            if (headPin == null && _pins.Length > 0)
            {
                headPin = _pins[0];
            }
        }

        /// <summary>Inspector の値を計算用の設定にまとめる。</summary>
        public PinBlastSettings BuildSettings()
        {
            return new PinBlastSettings
            {
                triggerSpeed = triggerSpeed,
                force = force,
                upwardRatio = upwardRatio,
                radius = radius,
                chainFalloff = chainFalloff,
                maxChainCount = maxChainCount,
                spinStrength = spinStrength,
                originOffsetMin = originOffsetMin,
                originOffsetMax = originOffsetMax,
                originFalloffWidth = originFalloffWidth,
            };
        }

        /// <summary>
        /// ピンが強くぶつかられたときに、そのピンから呼ばれる。
        /// 条件を満たしていれば、そのピンを起点に周りを吹き飛ばす。
        /// </summary>
        public void ReportImpact(Pin origin, float impactSpeed, Rigidbody other)
        {
            if (!enableEffect || origin == null || _pins == null)
            {
                return;
            }

            if (origin.HasExploded)
            {
                return;
            }

            PinBlastSettings settings = BuildSettings();
            int generation = origin.BlastGeneration;

            if (!PinBlastCalculator.ShouldExplode(impactSpeed, generation, settings))
            {
                return;
            }

            float strengthScale = 1f;

            if (generation == 0)
            {
                // 起点（世代0）は1投につき1か所だけにする。
                // ボールがラックを突き進む間、触れたピンごとに満威力で爆発すると、
                // 薄く当たっても全部倒れてしまうため
                if (oneOriginPerThrow && _originUsed)
                {
                    return;
                }

                bool fromBall = other != null && !_pinBodyIds.Contains(other.GetInstanceID());
                if (originFromBallOnly && !fromBall)
                {
                    return;
                }

                // 厚く当たったかを、ボールの進む向きに対するヘッドピンからの横ずれで見る
                if (fromBall && headPin != null)
                {
                    float offset = PinBlastCalculator.CalculateLateralOffset(
                        other.position, other.linearVelocity, headPin.InitialPosition);
                    strengthScale = PinBlastCalculator.OriginStrengthScale(offset, settings);

                    if (strengthScale <= 0f)
                    {
                        if (logEvents)
                        {
                            Debug.Log($"爆発せず：横ずれ {offset * 100f:F1}cm が範囲外", this);
                        }
                        return;
                    }

                    if (logEvents)
                    {
                        Debug.Log($"起点：{origin.name} 横ずれ {offset * 100f:F1}cm 威力{strengthScale:P0} 衝突{impactSpeed:F2}m/s", this);
                    }
                }

                _originUsed = true;
            }

            Explode(origin, generation, strengthScale, settings);
        }

        /// <summary>起点のピンの周りを吹き飛ばす。</summary>
        private void Explode(Pin origin, int generation, float strengthScale, PinBlastSettings settings)
        {
            origin.MarkExploded(generation);

            Vector3 originPosition = origin.transform.position;
            float spin = PinBlastCalculator.SpinAt(generation, settings) * strengthScale;
            int blasted = 0;

            for (int i = 0; i < _pins.Length; i++)
            {
                Pin target = _pins[i];
                if (target == null || target == origin || !target.CanBeBlasted)
                {
                    continue;
                }

                Vector3 velocityChange;
                if (!PinBlastCalculator.TryCalculateBlast(
                        originPosition, target.transform.position, generation, strengthScale, settings, out velocityChange))
                {
                    continue;
                }

                target.ApplyBlast(velocityChange, Random.onUnitSphere * spin, generation + 1);
                blasted++;
            }

            if (logEvents)
            {
                Debug.Log($"爆発：{origin.name} 世代{generation} → {blasted}本を吹き飛ばした", this);
            }
        }
    }
}
