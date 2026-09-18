using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>通過点1つぶんの位置と向き。</summary>
    public struct CameraWaypoint
    {
        public Vector3 position;
        public Quaternion rotation;

        public CameraWaypoint(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }

    /// <summary>
    /// 下見カメラの経路をたどる計算。
    /// 通過点の間を滑らかにつなぎ、行って戻るところまで面倒を見る。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class CameraPathSampler
    {
        /// <summary>
        /// 往復の進み具合を、片道の進み具合に直す。
        /// 0で出発点、0.5で折り返し点、1で出発点に戻る。
        /// </summary>
        public static float ToOneWayProgress(float roundTripProgress)
        {
            float t = Mathf.Clamp01(roundTripProgress);
            return t <= 0.5f ? t * 2f : (1f - t) * 2f;
        }

        /// <summary>
        /// 経路上の1点。progress は0で先頭、1で末尾。
        /// 通過点が1つしかなければ、その1点を返す。
        /// </summary>
        public static CameraWaypoint Sample(CameraWaypoint[] waypoints, float progress)
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                return new CameraWaypoint(Vector3.zero, Quaternion.identity);
            }

            if (waypoints.Length == 1)
            {
                return waypoints[0];
            }

            float t = Mathf.Clamp01(progress) * (waypoints.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(t), waypoints.Length - 2);
            float local = t - index;

            return new CameraWaypoint(
                SamplePosition(waypoints, index, local),
                Quaternion.Slerp(waypoints[index].rotation, waypoints[index + 1].rotation, local));
        }

        /// <summary>
        /// 位置を滑らかにつなぐ。前後の点も見て曲線にするので、角が立たない。
        /// 端では隣の点を折り返して使う。
        /// </summary>
        private static Vector3 SamplePosition(CameraWaypoint[] waypoints, int index, float local)
        {
            Vector3 p1 = waypoints[index].position;
            Vector3 p2 = waypoints[index + 1].position;
            Vector3 p0 = index > 0 ? waypoints[index - 1].position : p1 + (p1 - p2);
            Vector3 p3 = index + 2 < waypoints.Length ? waypoints[index + 2].position : p2 + (p2 - p1);

            return CatmullRom(p0, p1, p2, p3, local);
        }

        /// <summary>4点を通る滑らかな曲線。p1 と p2 の間を返す。</summary>
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                2f * p1
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// 経路が用意されていないレーンのために、単純な経路を作る。
        /// 構えの視点から、レーンに沿ってピンの手前まで進む。
        /// </summary>
        /// <param name="home">構え中のカメラの位置と向き。</param>
        /// <param name="target">見せたい場所（ピンのあたり）。</param>
        /// <param name="travelHeight">途中の高さ（m）。</param>
        public static CameraWaypoint[] BuildDefaultPath(
            CameraWaypoint home, Vector3 target, float travelHeight)
        {
            Vector3 toTarget = target - home.position;
            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude <= Mathf.Epsilon)
            {
                return new[] { home };
            }

            Vector3 direction = flat.normalized;
            float distance = flat.magnitude;

            // 途中の2点と、ピンの手前の1点
            var points = new CameraWaypoint[4];
            points[0] = home;

            for (int i = 1; i < 4; i++)
            {
                float ratio = i / 3f;
                Vector3 position = home.position + direction * (distance * ratio);
                position.y = Mathf.Lerp(home.position.y, travelHeight, ratio);

                Vector3 look = target - position;
                points[i] = new CameraWaypoint(
                    position,
                    look.sqrMagnitude > Mathf.Epsilon
                        ? Quaternion.LookRotation(look, Vector3.up)
                        : home.rotation);
            }

            return points;
        }
    }
}
