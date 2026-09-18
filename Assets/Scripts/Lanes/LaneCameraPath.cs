using UnityEngine;
using CrazyBowling.Core;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンが持つ下見カメラの経路。
    /// 空の GameObject を並べるだけでよく、シーンビューで掴んで動かせる。
    /// 回転レーンやジェットコースターでは、通過点を増やして見せ場を作れる。
    ///
    /// 付けなくてもよい。その場合は、構えの視点とピンの位置から自動で作る。
    /// </summary>
    public class LaneCameraPath : MonoBehaviour
    {
        [Header("通過点")]
        [Tooltip("行きの通過点。手前から奥の順に並べる。戻りは同じ道を逆にたどる。")]
        [SerializeField] private Transform[] waypoints;

        [Header("先頭の扱い")]
        [Tooltip("先頭を構えの視点にそろえる。オンにすると、下見の終わりが構えの視点とぴったり一致する。")]
        [SerializeField] private bool startFromCameraAnchor = true;

        /// <summary>通過点の数（構えの視点を足す前）。</summary>
        public int WaypointCount => waypoints == null ? 0 : waypoints.Length;

        /// <summary>
        /// 経路を組み立てる。
        /// 先頭を構えの視点にそろえるので、戻ったときに画面が飛ばない。
        /// </summary>
        /// <param name="home">構え中のカメラの位置と向き。</param>
        public CameraWaypoint[] BuildPath(CameraWaypoint home)
        {
            int count = 0;
            if (waypoints != null)
            {
                foreach (Transform point in waypoints)
                {
                    if (point != null)
                    {
                        count++;
                    }
                }
            }

            if (count == 0)
            {
                return null;
            }

            int offset = startFromCameraAnchor ? 1 : 0;
            var path = new CameraWaypoint[count + offset];

            if (startFromCameraAnchor)
            {
                path[0] = home;
            }

            int index = offset;
            foreach (Transform point in waypoints)
            {
                if (point == null)
                {
                    continue;
                }
                path[index] = new CameraWaypoint(point.position, point.rotation);
                index++;
            }

            return path;
        }
    }
}
