using CrazyBowling.Core;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// LOOK AHEAD の見回しで振れる幅を、このレーンだけ変える（段階6）。レーンのプレハブの中に置く。
    /// 付けていないレーンは既定の幅（左右 60度・上 20度・下 20度・寄り 15度・引き 5度）。
    /// 飾りの裏や地面の途切れが見えてしまうレーンで、そのレーンだけ幅を狭めるのに使う。
    /// </summary>
    public class LaneLookAround : MonoBehaviour
    {
        [Tooltip("このレーンで振れる幅。")]
        [SerializeField] private LookAroundLimits limits = LookAroundLimits.Default;

        /// <summary>このレーンで振れる幅。</summary>
        public LookAroundLimits Limits => limits;
    }
}
