using CrazyBowling.Ball;
using CrazyBowling.Core;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 6本目（スキー場）の跳び台の音（段階6）。レーンのプレハブの中に置く。
    /// ボールが踏切の端（ここから先は床が無い）を奥へ越えた瞬間に鳴らす。ボールと床には触らない。
    /// </summary>
    public class LaneJumpSound : MonoBehaviour
    {
        [Tooltip("踏切を持つレーン。空なら親から探す。")]
        [SerializeField] private GapLane gapLane;

        private AudioSource _source;
        private float _previousZ = float.NegativeInfinity;

        private void Awake()
        {
            _source = LaneSoundUtil.CreateSource(gameObject);
            if (gapLane == null)
            {
                gapLane = GetComponentInParent<GapLane>();
            }
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            BallController ball = player != null ? player.Ball : null;
            if (ball == null || gapLane == null || !ball.IsRolling)
            {
                _previousZ = float.NegativeInfinity;
                return;
            }

            float z = gapLane.FloorBasis.InverseTransformPoint(ball.transform.position).z;
            if (LaneCrests.CrossedForward(_previousZ, z, gapLane.TakeoffZ))
            {
                player.PlayOn(_source, player.Table.lane06Jump, "6本目：跳んだ");
            }
            _previousZ = z;
        }
    }
}
