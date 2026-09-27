using CrazyBowling.Core;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 2本目（客船）の汽笛（段階6）。レーンのプレハブの中に置く。
    /// 入って少ししたら1回鳴らし、そのあとはときどき鳴らす。レーンを出ると一緒に消えるので止まる。
    /// </summary>
    public class LaneHornSound : MonoBehaviour
    {
        private AudioSource _source;
        private float _next = -1f;

        private void Awake()
        {
            _source = LaneSoundUtil.CreateSource(gameObject);
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || player.Table == null)
            {
                return;
            }

            if (_next < 0f)
            {
                _next = Time.time + player.Table.lane02FirstHornDelay;
                return;
            }
            if (Time.time < _next)
            {
                return;
            }

            player.PlayOn(_source, player.Table.lane02Horn, "2本目：汽笛");
            _next = Time.time + SoundSchedule.NextDelay(player.Table.lane02HornInterval, Random.value);
        }
    }
}
