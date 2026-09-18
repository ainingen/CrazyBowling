using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// 連鎖爆発の調整値。Inspector から渡される。
    /// </summary>
    public struct PinBlastSettings
    {
        /// <summary>これ以上の勢いでぶつかられたら発動する（m/s）。</summary>
        public float triggerSpeed;

        /// <summary>吹き飛ばす力。速度の変化量として与える（m/s）。</summary>
        public float force;

        /// <summary>上向き成分の割合。0で真横、大きいほど高く浮く。</summary>
        public float upwardRatio;

        /// <summary>効果が及ぶ範囲（m）。起点からこの距離までのピンが飛ぶ。</summary>
        public float radius;

        /// <summary>連鎖するたびに威力へ掛ける率。0.6なら1回ごとに6割へ落ちる。</summary>
        public float chainFalloff;

        /// <summary>連鎖の最大回数。最初の爆発が0回目。</summary>
        public int maxChainCount;

        /// <summary>追加する回転の強さ（rad/s）。</summary>
        public float spinStrength;
    }

    /// <summary>
    /// 連鎖爆発の計算。MonoBehaviour に依存しないので EditMode テストで検証できる。
    /// </summary>
    public static class PinBlastCalculator
    {
        /// <summary>
        /// この衝突で爆発するか。
        /// 勢いが閾値に届いていること、連鎖の回数が上限に達していないことが条件。
        /// </summary>
        public static bool ShouldExplode(float impactSpeed, int generation, PinBlastSettings settings)
        {
            if (generation >= settings.maxChainCount)
            {
                return false;
            }
            return impactSpeed >= settings.triggerSpeed;
        }

        /// <summary>その世代の威力。連鎖するほど弱くなる。</summary>
        public static float StrengthAt(int generation, PinBlastSettings settings)
        {
            if (generation <= 0)
            {
                return settings.force;
            }
            return settings.force * Mathf.Pow(settings.chainFalloff, generation);
        }

        /// <summary>
        /// 起点から対象のピンへ与える速度の変化を求める。
        /// 範囲の外なら false を返し、velocityChange には触れない。
        /// </summary>
        public static bool TryCalculateBlast(
            Vector3 originPosition,
            Vector3 targetPosition,
            int generation,
            PinBlastSettings settings,
            out Vector3 velocityChange)
        {
            velocityChange = Vector3.zero;

            // 距離は水平だけで測る。高さの差で威力が変わらないようにするため
            Vector3 offset = targetPosition - originPosition;
            offset.y = 0f;
            float distance = offset.magnitude;

            float radius = Mathf.Max(settings.radius, 0.0001f);
            if (distance > radius)
            {
                return false;
            }

            // 真上に重なっている場合は向きが決まらないので飛ばさない
            if (distance < 0.0001f)
            {
                return false;
            }

            // 起点から離れるほど弱くする。範囲の端でちょうど半分
            float distanceFalloff = 1f - 0.5f * (distance / radius);
            float strength = StrengthAt(generation, settings) * distanceFalloff;

            Vector3 direction = offset / distance + Vector3.up * settings.upwardRatio;
            velocityChange = direction.normalized * strength;
            return true;
        }

        /// <summary>その世代で追加する回転の強さ。威力と同じ率で減衰させる。</summary>
        public static float SpinAt(int generation, PinBlastSettings settings)
        {
            if (generation <= 0)
            {
                return settings.spinStrength;
            }
            return settings.spinStrength * Mathf.Pow(settings.chainFalloff, generation);
        }
    }
}
