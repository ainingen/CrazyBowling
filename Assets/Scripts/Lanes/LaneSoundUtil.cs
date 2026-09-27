using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>レーン専用の音の部品で共通に使う小さな道具（段階6）。</summary>
    public static class LaneSoundUtil
    {
        /// <summary>
        /// 部品と同じ物に AudioSource を作る。レーンのプレハブの中の物なので、レーンを出ると一緒に消える。
        /// </summary>
        public static AudioSource CreateSource(GameObject owner)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        /// <summary>この部品が属するレーンのルート（LaneBehaviour の付いた物）。無ければ一番上の親。</summary>
        public static Transform LaneRoot(Transform from)
        {
            var lane = from.GetComponentInParent<LaneBehaviour>();
            return lane != null ? lane.transform : from.root;
        }
    }
}
