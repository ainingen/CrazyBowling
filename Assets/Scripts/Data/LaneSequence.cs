using UnityEngine;

namespace CrazyBowling.Data
{
    /// <summary>
    /// 1ゲームで回るレーンの並び。Project ウィンドウで右クリック →
    /// Create → CrazyBowling → レーンの並び から作る。
    /// 同じ LaneData を何度入れてもよい（仕組みを試すときのダミーに使う）。
    /// </summary>
    [CreateAssetMenu(fileName = "LaneSequence", menuName = "CrazyBowling/レーンの並び", order = 1)]
    public class LaneSequence : ScriptableObject
    {
        [Tooltip("回る順番。先頭から1レーン目。")]
        [SerializeField] private LaneData[] lanes;

        /// <summary>レーンの本数。</summary>
        public int Count => lanes == null ? 0 : lanes.Length;

        /// <summary>
        /// 指定した位置のレーン。範囲外や空の枠なら null。
        /// 呼ぶ側で null を確かめること。
        /// </summary>
        public LaneData GetLane(int index)
        {
            if (lanes == null || index < 0 || index >= lanes.Length)
            {
                return null;
            }
            return lanes[index];
        }
    }
}
