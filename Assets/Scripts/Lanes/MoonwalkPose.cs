using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 滑り歩きの手足の角度を決める計算。
    ///
    /// 歩く周期は「経過した時間」ではなく「進んだ距離」で進める。
    /// こうすると、動くものが端で遅くなったときに足も遅くなり、
    /// 止まれば足も止まる。足が地面を蹴っているように見えるのはこのため。
    ///
    /// 向きは進行方向の逆を向かせる。前に歩いているのに後ろへ滑る、
    /// というのが滑り歩きの見え方なので。
    ///
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class MoonwalkPose
    {
        /// <summary>
        /// 歩きの周期のどこにいるか（0から1）。
        /// 1周で両足が1回ずつ前に出る。
        /// </summary>
        /// <param name="distance">出発してから進んだ距離（m）。</param>
        /// <param name="strideLength">一歩の長さ（m）。</param>
        public static float GetPhase(float distance, float strideLength)
        {
            if (strideLength <= Mathf.Epsilon)
            {
                return 0f;
            }

            // 一歩で半周なので、一歩の長さの2倍で1周
            return Mathf.Repeat(distance / (strideLength * 2f), 1f);
        }

        /// <summary>
        /// 太ももの角度（度）。正で前に出る。
        /// </summary>
        /// <param name="phase">歩きの周期の位置（0から1）。</param>
        /// <param name="swing">前後に振る角度（度）。</param>
        /// <param name="rightLeg">右足ならtrue。左足と半周ずらす。</param>
        public static float GetThighAngle(float phase, float swing, bool rightLeg)
        {
            float shifted = rightLeg ? phase + 0.5f : phase;
            return swing * Mathf.Sin(shifted * 2f * Mathf.PI);
        }

        /// <summary>
        /// 膝の角度（度）。正で曲がる。膝は逆には曲がらないので負にはしない。
        /// 足を前へ運んでいる間だけ曲げる。
        /// </summary>
        public static float GetKneeAngle(float phase, float bend, bool rightLeg)
        {
            float shifted = rightLeg ? phase + 0.5f : phase;

            // 足が後ろから前へ戻る半周のあいだだけ曲げる
            float wave = -Mathf.Sin(shifted * 2f * Mathf.PI + Mathf.PI * 0.5f);
            return bend * Mathf.Max(0f, wave);
        }

        /// <summary>
        /// 腕の角度（度）。足と逆に振る。
        /// </summary>
        public static float GetArmAngle(float phase, float swing, bool rightArm)
        {
            // 右腕は左足と一緒に前へ出る。左腕は右足と一緒に出る
            return GetThighAngle(phase, swing, !rightArm);
        }

        /// <summary>
        /// 腰の上下（m）。一歩ごとに1回沈むので、歩きの倍の速さで動く。
        /// </summary>
        public static float GetBob(float phase, float amount)
        {
            return -Mathf.Abs(amount) * Mathf.Abs(Mathf.Sin(phase * 2f * Mathf.PI));
        }

        /// <summary>
        /// 向くべき側。進行方向の逆を向く。
        ///
        /// 端で折り返すときは速度が0に近づくので、そこで向きが暴れないように、
        /// ある速さを超えるまでは前の向きを保つ。
        /// </summary>
        /// <param name="velocity">今の速さ（進む向きに沿った符号つき）。</param>
        /// <param name="currentSign">今向いている側（1か−1）。</param>
        /// <param name="deadZone">この速さ以下なら向きを変えない（m/秒）。</param>
        /// <returns>向くべき側。1か−1。</returns>
        public static float GetFacingSign(float velocity, float currentSign, float deadZone)
        {
            if (Mathf.Abs(velocity) <= Mathf.Abs(deadZone))
            {
                return currentSign >= 0f ? 1f : -1f;
            }

            // 進む向きの逆を向く
            return velocity > 0f ? -1f : 1f;
        }
    }
}
