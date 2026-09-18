using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Ball
{
    /// <summary>
    /// カーブの計算に使う設定値。Inspector から渡される。
    /// </summary>
    public struct BallCurveSettings
    {
        /// <summary>ボールの半径（m）。接地点の位置を求めるのに使う。</summary>
        public float radius;

        /// <summary>カーブ最大のときに与える縦軸まわりの回転（rad/s）。</summary>
        public float maxSideSpin;

        /// <summary>横回転1rad/s あたりに加える横向きの加速度（m/s^2）。</summary>
        public float curveForce;

        /// <summary>横回転が失われる速さ。大きいほど早く曲がりが止まる（1/秒）。</summary>
        public float spinDecay;

        /// <summary>接地点の滑りがこれ以下なら「転がっている」とみなす（m/s）。</summary>
        public float slipThreshold;
    }

    /// <summary>
    /// カーブの計算。MonoBehaviour に依存しないので EditMode テストで検証でき、
    /// 実際の投球と予測線が同じ式を使える。
    ///
    /// Unity の物理は、縦軸まわりの回転を与えてもボールを曲げてくれない。
    /// 接地点の速度は ω×(0,-R,0) で決まり、縦軸成分はここに現れないため。
    /// そこで「滑っている間だけ横向きの力を足す」という形で自前で処理する。
    /// </summary>
    public static class BallCurveModel
    {
        /// <summary>
        /// 接地点が床に対してどれだけ滑っているか（m/s）。
        /// 前回転だけで転がっていれば0になる。
        /// </summary>
        public static float CalculateSlipSpeed(Vector3 velocity, Vector3 angularVelocity, float radius)
        {
            Vector3 contactVelocity = velocity + Vector3.Cross(angularVelocity, new Vector3(0f, -radius, 0f));
            contactVelocity.y = 0f;
            return contactVelocity.magnitude;
        }

        /// <summary>
        /// 進む向きについての滑り（m/s）。床の摩擦で前回転が増えるにつれて0に近づく。
        ///
        /// 滑りの大きさをそのまま使うと、横向きの速度が増えたぶんも滑りとして数えてしまい、
        /// 「曲がる → 横に滑る → まだ滑っていると判定される」で永久に曲がり続ける。
        /// 転がりに変わったかは、進む向きの成分だけで見る。
        /// </summary>
        public static float CalculateForwardSlip(Vector3 velocity, Vector3 angularVelocity, float radius)
        {
            Vector3 direction = velocity;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }
            direction.Normalize();

            Vector3 contactVelocity = velocity + Vector3.Cross(angularVelocity, new Vector3(0f, -radius, 0f));
            contactVelocity.y = 0f;
            return Mathf.Abs(Vector3.Dot(contactVelocity, direction));
        }

        /// <summary>滑っているか。転がりに変わったら曲がりを止めるために使う。</summary>
        public static bool IsSliding(float slipSpeed, BallCurveSettings settings)
        {
            return slipSpeed > settings.slipThreshold;
        }

        /// <summary>
        /// 横向きに加える加速度。進行方向に対して直角の向きで、
        /// 横回転が正なら右へ、負なら左へ曲がる。
        /// </summary>
        public static Vector3 CalculateLateralAcceleration(Vector3 velocity, float sideSpin, BallCurveSettings settings)
        {
            Vector3 direction = velocity;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            direction.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            return right * (sideSpin * settings.curveForce);
        }

        /// <summary>横回転を減衰させる。指数的に落とすので、いきなり止まらない。</summary>
        public static float DecaySideSpin(float sideSpin, float deltaTime, BallCurveSettings settings)
        {
            if (settings.spinDecay <= 0f)
            {
                return sideSpin;
            }
            return sideSpin * Mathf.Exp(-settings.spinDecay * deltaTime);
        }

        /// <summary>
        /// 予測線のために、同じ式で軌道を前に進める。
        /// 物理エンジンは使わず、床の摩擦で前回転が増えていく分は
        /// 「滑りが一定の割合で減っていく」という近似で表す。
        /// </summary>
        /// <param name="startPosition">ボールの現在位置。</param>
        /// <param name="direction">投げる向き（水平・正規化済み）。</param>
        /// <param name="speed">初速（m/s）。</param>
        /// <param name="sideSpin">与える横回転（rad/s）。</param>
        /// <param name="initialSlip">投げた瞬間の接地点の滑り（m/s）。</param>
        /// <param name="slipDecayRate">滑りが減る割合（1/秒）。摩擦が弱いところでは緩む。</param>
        /// <param name="settings">調整値。</param>
        /// <param name="duration">何秒先まで予測するか。</param>
        /// <param name="stepCount">刻み数。</param>
        /// <param name="output">結果の点を入れる先。呼ぶ側が用意する。</param>
        /// <param name="frictionSampler">その場所の摩擦の係数を返す役。null なら全域1。</param>
        public static void PredictPath(
            Vector3 startPosition,
            Vector3 direction,
            float speed,
            float sideSpin,
            float initialSlip,
            float slipDecayRate,
            BallCurveSettings settings,
            float duration,
            int stepCount,
            List<Vector3> output,
            System.Func<Vector3, float> frictionSampler = null)
        {
            if (output == null)
            {
                return;
            }

            output.Clear();
            if (stepCount < 2 || duration <= 0f)
            {
                return;
            }

            float dt = duration / (stepCount - 1);
            Vector3 position = startPosition;
            Vector3 velocity = direction * speed;
            float spin = sideSpin;
            float slip = initialSlip;

            output.Add(position);
            for (int i = 1; i < stepCount; i++)
            {
                // その場所の摩擦。オイルの上では小さく、乾いた床では大きい
                float friction = frictionSampler != null ? frictionSampler(position) : 1f;

                if (IsSliding(slip, settings))
                {
                    velocity += CalculateLateralAcceleration(velocity, spin, settings) * (friction * dt);
                }

                position += velocity * dt;

                // 摩擦が弱いところでは、横回転も滑りもほとんど失われない
                spin = DecaySideSpin(spin, dt * friction, settings);
                slip = Mathf.Max(0f, slip - slipDecayRate * friction * dt);
                output.Add(position);
            }
        }
    }
}
