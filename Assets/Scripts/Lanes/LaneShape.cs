using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 起伏のある床の形を決める設定。
    /// レーンは原点から +Z 方向に伸び、左右は X の ±(width/2)。
    /// </summary>
    public struct LaneShapeSettings
    {
        /// <summary>レーンの長さ（m）。</summary>
        public float length;

        /// <summary>レーンの幅（m）。</summary>
        public float width;

        /// <summary>床の厚み（m）。横から見たときの見た目のため。</summary>
        public float thickness;

        /// <summary>レーンに沿った中心の高さ。横軸は0〜1（手前〜奥）、縦軸はm。</summary>
        public AnimationCurve heightAlongLane;

        /// <summary>レーンに沿った左右の傾き。横軸は0〜1、縦軸は度。正で右が高い。</summary>
        public AnimationCurve tiltAlongLane;

        /// <summary>横断面の形。横軸は-1〜1（左端〜右端）、縦軸はm。谷型やかまぼこ型にできる。</summary>
        public AnimationCurve crossSection;

        /// <summary>ここから先は平らに寄せ始める位置（m）。ピン台を水平に保つため。</summary>
        public float flatStartZ;

        /// <summary>ここから先は完全に平らにする位置（m）。</summary>
        public float flatEndZ;
    }

    /// <summary>
    /// 起伏のある床の高さと向きを求める計算。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class LaneShape
    {
        /// <summary>法線を求めるときに前後左右をどれだけずらして見るか（m）。</summary>
        private const float NormalStep = 0.01f;

        /// <summary>曲線が入っていなければ0として扱う。</summary>
        private static float Evaluate(AnimationCurve curve, float t)
        {
            return curve == null || curve.length == 0 ? 0f : curve.Evaluate(t);
        }

        /// <summary>
        /// ピン台を平らに保つための効き具合。
        /// flatStartZ までは1（起伏そのまま）、flatEndZ から先は0（完全に平ら）。
        /// 境目で折れないよう滑らかにつなぐ。
        /// </summary>
        public static float FlatFade(float z, LaneShapeSettings settings)
        {
            if (z <= settings.flatStartZ)
            {
                return 1f;
            }
            if (z >= settings.flatEndZ)
            {
                return 0f;
            }

            float span = settings.flatEndZ - settings.flatStartZ;
            if (span <= Mathf.Epsilon)
            {
                return 0f;
            }

            float t = (z - settings.flatStartZ) / span;
            return 1f - (t * t * (3f - 2f * t));
        }

        /// <summary>
        /// レーンの上の1点の高さ（m）。
        /// x はレーンの中心からの左右のずれ、z は手前からの奥行き。
        /// </summary>
        public static float SampleHeight(float x, float z, LaneShapeSettings settings)
        {
            float length = Mathf.Max(settings.length, Mathf.Epsilon);
            float halfWidth = Mathf.Max(settings.width * 0.5f, Mathf.Epsilon);

            // 中心の高さは、平らにし始める位置から先は手前の値を保つ。
            // そうしないとピン台が斜めのまま持ち上がってしまう
            float flatStartV = Mathf.Clamp01(settings.flatStartZ / length);
            float v = Mathf.Clamp01(z / length);
            float center = Evaluate(settings.heightAlongLane, Mathf.Min(v, flatStartV));

            float fade = FlatFade(z, settings);
            float tiltDegrees = Evaluate(settings.tiltAlongLane, v) * fade;
            float cross = Evaluate(settings.crossSection, Mathf.Clamp(x / halfWidth, -1f, 1f)) * fade;

            return center + x * Mathf.Tan(tiltDegrees * Mathf.Deg2Rad) + cross;
        }

        /// <summary>
        /// レーンの上の1点の向き（上向きの単位ベクトル）。
        /// 前後左右を少しずらして高さを測り、その傾きから求める。
        /// </summary>
        public static Vector3 SampleNormal(float x, float z, LaneShapeSettings settings)
        {
            float dx = SampleHeight(x + NormalStep, z, settings)
                     - SampleHeight(x - NormalStep, z, settings);
            float dz = SampleHeight(x, z + NormalStep, settings)
                     - SampleHeight(x, z - NormalStep, settings);

            // 高さの傾きから法線を作る。両側の差なので 2×ステップで割る
            var normal = new Vector3(
                -dx / (2f * NormalStep),
                1f,
                -dz / (2f * NormalStep));

            return normal.normalized;
        }

        /// <summary>
        /// 床の底の高さ（m）。スカートをどこまで下ろすかに使う。
        /// いちばん低い所からさらに厚みぶん下げる。
        /// </summary>
        public static float CalculateBottomY(LaneShapeSettings settings, int samplesAlong = 64)
        {
            float halfWidth = Mathf.Max(settings.width * 0.5f, Mathf.Epsilon);
            float lowest = float.PositiveInfinity;

            int steps = Mathf.Max(2, samplesAlong);
            for (int j = 0; j < steps; j++)
            {
                float z = settings.length * j / (steps - 1f);
                foreach (float x in new[] { -halfWidth, 0f, halfWidth })
                {
                    float y = SampleHeight(x, z, settings);
                    if (y < lowest)
                    {
                        lowest = y;
                    }
                }
            }

            if (float.IsInfinity(lowest))
            {
                lowest = 0f;
            }

            return lowest - Mathf.Max(settings.thickness, 0.001f);
        }
    }
}
