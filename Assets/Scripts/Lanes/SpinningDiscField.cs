using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 回る円盤の設定。
    /// </summary>
    public struct SpinningDiscSettings
    {
        /// <summary>円盤の半径（m）。</summary>
        public float radius;

        /// <summary>一周にかかる時間（秒）。負なら逆回り。0なら止まっている。</summary>
        public float period;

        /// <summary>効き具合。0で流れない。大きいほど横へ流れる。</summary>
        public float driftGain;

        /// <summary>外周で効きが弱まる幅（m）。0なら円盤の中は一律で効く。</summary>
        public float edgeSoftness;
    }

    /// <summary>
    /// 回る円盤の上を通ったボールを、横へ流す計算。
    ///
    /// 円盤の表面速度をそのまま足す形は採っていない。
    /// 表面速度の横成分は円盤の中心より手前と奥で符号が反対なので、
    /// まっすぐ通り抜けると打ち消し合い、ほとんど曲がらないため。
    ///
    /// 代わりに、回る床の上を転がる球が実際に描く円弧と同じ形
    /// ＝「進行方向に対して常に横向きの力」にしてある。
    /// ただし本来の係数（球なら 2/7）では毎秒1回転でレーンから飛び出すほど強いので、
    /// 効き具合は driftGain で決め、見た目の回転速度とは切り離してある。
    ///
    /// この形だと、横に付く速度は
    ///     driftGain × 回る速さ(rad/s) × 円盤を横切った長さ(m)
    /// になり、ボールの速さに左右されない。
    /// 速い球は円盤の上にいる時間が短く、遅い球は長いので釣り合うため。
    /// 結果、「円盤のどこを通したか」だけで流れる量が決まる。
    ///
    /// 力はスクリプトで加えるので、円盤に Collider は持たせない。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class SpinningDiscField
    {
        /// <summary>これより遅いボールには効かせない（m/秒）。</summary>
        private const float MinSpeed = 0.01f;

        /// <summary>
        /// その位置での効き具合（0〜1）。
        ///
        /// 外周の手前 edgeSoftness の幅で 1 から 0 へ落とす。
        /// 境目でいきなり効き始めると、ボールが折れ線に曲がって見えるため。
        /// </summary>
        /// <param name="distanceFromCenter">円盤の中心からの距離（m）。</param>
        public static float GetInfluence(float distanceFromCenter, SpinningDiscSettings settings)
        {
            float radius = settings.radius;
            if (radius <= Mathf.Epsilon)
            {
                return 0f;
            }

            float distance = Mathf.Abs(distanceFromCenter);
            if (distance >= radius)
            {
                return 0f;
            }

            float softness = Mathf.Clamp(settings.edgeSoftness, 0f, radius);
            if (softness <= Mathf.Epsilon)
            {
                return 1f;
            }

            // 外周から softness だけ内側に入れば、そこから先は一律で効く
            return Mathf.Clamp01((radius - distance) / softness);
        }

        /// <summary>
        /// 円盤を横切る長さ（m）。まっすぐ通り抜けたときの弦の長さ。
        /// 中心を通せば直径、端をかすめればほぼ0、外れれば0。
        /// </summary>
        /// <param name="distanceFromCenterLine">通り道と円盤の中心との距離（m）。</param>
        public static float GetChordLength(float distanceFromCenterLine, SpinningDiscSettings settings)
        {
            float radius = settings.radius;
            if (radius <= Mathf.Epsilon)
            {
                return 0f;
            }

            float distance = Mathf.Abs(distanceFromCenterLine);
            if (distance >= radius)
            {
                return 0f;
            }

            return 2f * Mathf.Sqrt(radius * radius - distance * distance);
        }

        /// <summary>
        /// 横へ流れる速さの見積もり（m/秒）。
        /// ボールの速さは入らない。どこを通したかだけで決まる。
        /// 円盤の位置と半径から、ピンまでにどれだけ流れるかを逆算するのに使う。
        /// </summary>
        /// <param name="chordLength">円盤を横切る長さ（m）。</param>
        public static float EstimateDriftSpeed(float chordLength, SpinningDiscSettings settings)
        {
            return settings.driftGain * ObstacleMotion.GetAngularSpeed(settings.period) * chordLength;
        }

        /// <summary>
        /// 円盤の上にいるボールに加える横向きの加速度を求める。
        ///
        /// 高さ（y）は見ない。円盤は床と面一なので、上下のずれで効き方を変えない。
        /// </summary>
        /// <param name="discCenter">円盤の中心（ワールド座標）。</param>
        /// <param name="ballPosition">ボールの位置（ワールド座標）。</param>
        /// <param name="ballVelocity">ボールの速度（ワールド座標）。</param>
        /// <param name="acceleration">加える加速度（m/秒²）。効かないときは0。</param>
        /// <returns>効くなら true。</returns>
        public static bool TryCalculateDrift(
            Vector3 discCenter,
            Vector3 ballPosition,
            Vector3 ballVelocity,
            SpinningDiscSettings settings,
            out Vector3 acceleration)
        {
            acceleration = Vector3.zero;

            Vector3 offset = ballPosition - discCenter;
            offset.y = 0f;

            float influence = GetInfluence(offset.magnitude, settings);
            if (influence <= 0f)
            {
                return false;
            }

            float angularSpeed = ObstacleMotion.GetAngularSpeed(settings.period);
            if (Mathf.Abs(angularSpeed) <= Mathf.Epsilon)
            {
                return false;
            }

            if (Mathf.Abs(settings.driftGain) <= Mathf.Epsilon)
            {
                return false;
            }

            Vector3 horizontal = ballVelocity;
            horizontal.y = 0f;
            if (horizontal.magnitude < MinSpeed)
            {
                return false;
            }

            // 進行方向に対して常に横向き。
            // Cross の結果の大きさが既に速さなので、速さを追加で掛けない。
            // これで積み上げた横速度が driftGain × 回る速さ × 横切った長さ になる。
            acceleration = Vector3.Cross(Vector3.up, horizontal)
                           * (settings.driftGain * angularSpeed * influence);
            return true;
        }
    }
}
