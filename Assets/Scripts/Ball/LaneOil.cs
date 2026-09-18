using UnityEngine;

namespace CrazyBowling.Ball
{
    /// <summary>
    /// レーンに塗られたオイルの設定。
    /// 手前にオイルがあり、奥は乾いている、という区分けを表す。
    /// </summary>
    public struct LaneOilSettings
    {
        /// <summary>オイルを使うか。オフなら全域が乾いた状態になる。</summary>
        public bool enabled;

        /// <summary>オイルが終わる位置（レーンの手前からの奥行き・m）。</summary>
        public float oilEndZ;

        /// <summary>オイルから乾いた床へ移り変わる長さ（m）。短いと折れ線に見える。</summary>
        public float transitionLength;

        /// <summary>オイル区画の摩擦の係数。小さいほど効かない。</summary>
        public float oilFriction;

        /// <summary>乾いた区画の摩擦の係数。</summary>
        public float dryFriction;
    }

    /// <summary>
    /// レーンの位置から摩擦の効き具合を返す計算。
    ///
    /// 本物のボウリングは、レーンの手前3分の2にオイルが塗ってある。
    /// オイルの上では横回転が食いつかず、ボールはほとんど曲がらないまま滑っていく。
    /// 奥の乾いた床に入ると急に食いつき、そこで大きく曲がる。
    ///
    /// 横向きの力は摩擦から生まれるので、位置ごとの摩擦が分かれば
    /// 「手前は曲がらず、奥で曲がる」という本来の形を作れる。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class LaneOil
    {
        /// <summary>オイルを使わないときの設定。全域が乾いた床と同じ。</summary>
        public static LaneOilSettings None => new LaneOilSettings
        {
            enabled = false,
            oilEndZ = 0f,
            transitionLength = 0f,
            oilFriction = 1f,
            dryFriction = 1f,
        };

        /// <summary>
        /// その位置の摩擦の係数。
        /// オイル区画では小さく、乾いた区画では大きい。境目は滑らかにつなぐ。
        /// </summary>
        /// <param name="distanceAlongLane">レーンの手前からの奥行き（m）。</param>
        public static float GetFrictionScale(float distanceAlongLane, LaneOilSettings settings)
        {
            if (!settings.enabled)
            {
                return 1f;
            }

            float oil = Mathf.Max(0f, settings.oilFriction);
            float dry = Mathf.Max(0f, settings.dryFriction);

            if (distanceAlongLane <= settings.oilEndZ)
            {
                return oil;
            }

            float span = settings.transitionLength;
            if (span <= Mathf.Epsilon)
            {
                return dry;
            }

            float t = Mathf.Clamp01((distanceAlongLane - settings.oilEndZ) / span);

            // 両端で傾きが0になるつなぎ方。折れ線に見えないようにするため
            return Mathf.Lerp(oil, dry, t * t * (3f - 2f * t));
        }

        /// <summary>
        /// 乾いた床に完全に入る位置（m）。
        /// ここから先は曲がりが最大になる。調整の目安に使う。
        /// </summary>
        public static float GetDryStartZ(LaneOilSettings settings)
        {
            return settings.enabled
                ? settings.oilEndZ + Mathf.Max(0f, settings.transitionLength)
                : 0f;
        }
    }
}
