using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>筒の推力の設定。</summary>
    public struct TubeThrustSettings
    {
        /// <summary>推力が効き始める奥行き（m）。</summary>
        public float startZ;

        /// <summary>効き始めてから全力になるまでの長さ（m）。0なら入った瞬間から全力。</summary>
        public float rampLength;

        /// <summary>推力が終わる奥行き（m）。</summary>
        public float endZ;

        /// <summary>いちばん強いときの加速度（m/s²）。0以下なら推力なし。</summary>
        public float acceleration;

        /// <summary>この前向きの速さ（m/s）以上では推力が0になる。</summary>
        public float fadeSpeed;
    }

    /// <summary>
    /// 7本目：筒の中で止まりかけたボールを、エンジンの推力で出口へ押し出す強さを決める。
    ///
    /// ── なぜ要るか ──────────────────────────────
    ///
    /// 遅い球や端から投げた球は、羽根2組目（奥へ行くほど軸に寄る＝筒の底では上り坂の並び）を
    /// 登り切れずに押し戻され、1組目と2組目のすき間の底に落ちて止まっていた（45投中17投）。
    ///
    /// ── 効き方 ─────────────────────────────────
    ///
    /// 止まりかけ・逆走している球には全力、前向きの速さが fadeSpeed に近づくほど弱く、
    /// それより速い球には効かない。速い球は元のまま（壁を駆け上がる見せ場は速いうちに起きる）で、
    /// 加速しすぎてピンを素通りすることもない。
    ///
    /// 物理の世界には触らない。強さを返すだけで、加えるのは TubeLane。
    /// </summary>
    public static class TubeThrust
    {
        /// <summary>
        /// その場所・その速さで受ける、出口向きの加速度（m/s²）。
        /// </summary>
        /// <param name="localZ">レーンの座標での奥行き（m）。</param>
        /// <param name="forwardSpeed">出口向きの速さ（m/s）。逆走なら負。</param>
        public static float CalculateAcceleration(float localZ, float forwardSpeed, TubeThrustSettings settings)
        {
            if (settings.acceleration <= 0f)
            {
                return 0f;
            }

            if (localZ < settings.startZ || localZ > settings.endZ)
            {
                return 0f;
            }

            // 入口では少しずつ効かせる。急に押すと、筒の中を回っている球が不自然に跳ねる
            float ramp = settings.rampLength > 0f
                ? Mathf.Clamp01((localZ - settings.startZ) / settings.rampLength)
                : 1f;

            // 前向きに速いほど弱く。止まりかけと逆走は全力
            float speedFactor = settings.fadeSpeed > 0f
                ? Mathf.Clamp01(1f - forwardSpeed / settings.fadeSpeed)
                : (forwardSpeed <= 0f ? 1f : 0f);

            return settings.acceleration * ramp * speedFactor;
        }
    }
}
