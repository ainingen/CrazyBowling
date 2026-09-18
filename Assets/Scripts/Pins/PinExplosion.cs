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

        [Header("発動")]
        [Tooltip("これ以上の勢いでぶつかられたら発動する（m/s）。下げるほど小さな衝突でも爆発する。")]
        [SerializeField] private float triggerSpeed = 4.5f;

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
            };
        }

        /// <summary>
        /// ピンが強くぶつかられたときに、そのピンから呼ばれる。
        /// 条件を満たしていれば、そのピンを起点に周りを吹き飛ばす。
        /// </summary>
        public void ReportImpact(Pin origin, float impactSpeed)
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

            // 起点（世代0）は1投につき1か所だけにする。
            // ボールがラックを突き進む間、触れたピンごとに満威力で爆発すると、
            // 薄く当たっても全部倒れてしまうため
            if (generation == 0 && oneOriginPerThrow && _originUsed)
            {
                return;
            }

            if (!PinBlastCalculator.ShouldExplode(impactSpeed, generation, settings))
            {
                return;
            }

            if (generation == 0)
            {
                _originUsed = true;
            }

            Explode(origin, generation, settings);
        }

        /// <summary>起点のピンの周りを吹き飛ばす。</summary>
        private void Explode(Pin origin, int generation, PinBlastSettings settings)
        {
            origin.MarkExploded();

            Vector3 originPosition = origin.transform.position;
            float spin = PinBlastCalculator.SpinAt(generation, settings);
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
                        originPosition, target.transform.position, generation, settings, out velocityChange))
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
