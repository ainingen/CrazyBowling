using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// ピン1本分の観測値。MonoBehaviour 側で集めて、判定に渡す。
    /// </summary>
    public struct PinSample
    {
        /// <summary>ピンの上方向。</summary>
        public Vector3 up;

        /// <summary>現在位置。</summary>
        public Vector3 position;

        /// <summary>立っていたときの位置。</summary>
        public Vector3 initialPosition;

        /// <summary>速度の大きさ（m/s）。</summary>
        public float linearSpeed;

        /// <summary>角速度の大きさ（rad/s）。</summary>
        public float angularSpeed;
    }

    /// <summary>
    /// 倒れ判定と静止判定の設定値。
    /// </summary>
    public struct PinJudgeSettings
    {
        /// <summary>この角度を超えて傾いたら倒れたとみなす（度）。</summary>
        public float tiltThresholdDegrees;

        /// <summary>この高さより下に落ちたら倒れたとみなす（m）。</summary>
        public float fallYThreshold;

        /// <summary>初期位置からこの距離を超えて水平移動したら倒れたとみなす（m）。</summary>
        public float horizontalMoveThreshold;

        /// <summary>これ以下なら静止とみなす速度（m/s）。</summary>
        public float restLinearSpeed;

        /// <summary>これ以下なら静止とみなす角速度（rad/s）。</summary>
        public float restAngularSpeed;
    }

    /// <summary>
    /// ピンが倒れたかどうかを判定する純粋なC#クラス。
    /// MonoBehaviour に依存しないので EditMode テストで検証できる。
    /// </summary>
    public static class PinFallJudge
    {
        /// <summary>
        /// ピンがピンデッキから出てしまったか。次のどちらかに当てはまる場合。
        /// ・レーン外に落ちた
        /// ・初期位置から水平に大きく動いた（台から外れた扱い）
        /// こうなったピンは二度と立ち上がらず、残りのピンに当たることもないので、
        /// 静止判定の対象から外す。
        /// </summary>
        public static bool IsOutOfPlay(PinSample sample, PinJudgeSettings settings)
        {
            // レーン外への落下
            if (sample.position.y < settings.fallYThreshold)
            {
                return true;
            }

            // 台から外れるほど水平に動いた
            Vector3 moved = sample.position - sample.initialPosition;
            moved.y = 0f;
            return moved.magnitude > settings.horizontalMoveThreshold;
        }

        /// <summary>
        /// 1本のピンが倒れたか。次のどれかに当てはまれば倒れたとみなす。
        /// ・デッキから出た（落下、または台から外れるほど動いた）
        /// ・傾きが閾値を超えた
        /// </summary>
        public static bool IsFallen(PinSample sample, PinJudgeSettings settings)
        {
            if (IsOutOfPlay(sample, settings))
            {
                return true;
            }

            // 傾き。閾値ちょうどは「超えていない」ので倒れていない
            float tiltAngle = Vector3.Angle(sample.up, Vector3.up);
            return tiltAngle > settings.tiltThresholdDegrees;
        }

        /// <summary>倒れている本数を数える。</summary>
        public static int CountFallen(IReadOnlyList<PinSample> samples, PinJudgeSettings settings)
        {
            if (samples == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                if (IsFallen(samples[i], settings))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 全部が静止しているか。1本でも動いていれば false。
        /// ピットに落ちたピンは転がり続けて止まらないので、判定の対象から外す。
        /// 傾いているだけでデッキ上に残っているピンは、立ち直ることも他のピンに当たることも
        /// あるので、対象に含めたままにする。
        /// </summary>
        public static bool AreAllAtRest(IReadOnlyList<PinSample> samples, PinJudgeSettings settings)
        {
            if (samples == null)
            {
                return true;
            }

            for (int i = 0; i < samples.Count; i++)
            {
                if (IsOutOfPlay(samples[i], settings))
                {
                    continue;
                }
                if (samples[i].linearSpeed > settings.restLinearSpeed)
                {
                    return false;
                }
                if (samples[i].angularSpeed > settings.restAngularSpeed)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
