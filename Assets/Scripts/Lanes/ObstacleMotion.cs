using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 動く障害物の揺れ方の設定。
    /// </summary>
    public struct ObstacleMotionSettings
    {
        /// <summary>中心からの振れ幅（片側・m）。0なら動かない。</summary>
        public float travel;

        /// <summary>一往復にかかる時間（秒）。0以下なら動かない。</summary>
        public float period;

        /// <summary>開始位置のずらし。0で中心から右へ動き出す。0.25で右端から始まる。</summary>
        public float phase;
    }

    /// <summary>
    /// 動く障害物の位置を時間から決める計算。
    ///
    /// 折り返しは正弦波にしてある。一定速度で往復させると
    /// 端で速度が一瞬で反転し、そのときボールに触れていると
    /// 弾かれたように飛ぶため。正弦波なら端で速度が0になる。
    ///
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// 回る円盤など、別の動きのレーンからも使う。
    /// </summary>
    public static class ObstacleMotion
    {
        /// <summary>
        /// -1から1の間を往復する波。
        /// 振れ幅を掛ければ位置に、角度を掛ければ回転に使える。
        /// </summary>
        /// <param name="time">レーンに入ってからの経過（秒）。</param>
        /// <param name="period">一往復にかかる時間（秒）。</param>
        /// <param name="phase">開始位置のずらし（1で一往復ぶん）。</param>
        public static float GetWave(float time, float period, float phase)
        {
            if (period <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Sin((time / period + phase) * 2f * Mathf.PI);
        }

        /// <summary>中心からのずれ（m）。</summary>
        public static float GetOffset(float time, ObstacleMotionSettings settings)
        {
            return settings.travel * GetWave(time, settings.period, settings.phase);
        }

        /// <summary>
        /// 動きが一番速いときの速さ（m/秒）。
        /// ボールに与える横向きの勢いの目安になる。速すぎると弾き飛ばす。
        /// </summary>
        public static float GetPeakSpeed(ObstacleMotionSettings settings)
        {
            if (settings.period <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Abs(settings.travel) * 2f * Mathf.PI / settings.period;
        }

        /// <summary>
        /// 回り続ける角度（度）。0〜360 に収まる。
        ///
        /// GetWave と違い、負の period は「止まっている」ではなく逆回りとして扱う。
        /// 往復には向きの区別が無いが、回転には時計回り・反時計回りの区別があるため。
        /// </summary>
        /// <param name="time">レーンに入ってからの経過（秒）。</param>
        /// <param name="period">一周にかかる時間（秒）。負なら逆回り。</param>
        /// <param name="phase">開始角度のずらし（1で一周ぶん）。</param>
        public static float GetAngle(float time, float period, float phase)
        {
            if (Mathf.Abs(period) <= Mathf.Epsilon)
            {
                return 0f;
            }

            return Mathf.Repeat((time / period + phase) * 360f, 360f);
        }

        /// <summary>
        /// 回る速さ（ラジアン/秒）。正で上から見て反時計回り、負で時計回り。
        ///
        /// 見た目の回転と、ボールに与える力の両方でこの値を使う。
        /// 二か所で別々に計算すると、片方だけ直したときにずれるため、ここから配る。
        /// </summary>
        /// <param name="period">一周にかかる時間（秒）。負なら逆回り。</param>
        public static float GetAngularSpeed(float period)
        {
            if (Mathf.Abs(period) <= Mathf.Epsilon)
            {
                return 0f;
            }

            return 2f * Mathf.PI / period;
        }
    }
}
