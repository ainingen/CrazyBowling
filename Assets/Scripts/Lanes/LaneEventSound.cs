using CrazyBowling.Core;
using CrazyBowling.Data;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンの仕掛けの知らせを聞いて1回鳴らす音（段階6）。レーンのプレハブの中に置く。
    /// 7本目：推力が効き始めた／9本目：神殿が吹き飛んだ／10本目：ピンに当たった閃光。
    /// 知らせを聞くだけで、仕掛けの動きには触らない。レーンを出ると一緒に消える。
    /// </summary>
    public class LaneEventSound : MonoBehaviour
    {
        /// <summary>どの仕掛けの知らせを聞くか。</summary>
        public enum Trigger
        {
            /// <summary>7本目：エンジンの推力で押し出され始めた。</summary>
            JetThrust,

            /// <summary>9本目：神殿が吹き飛んだ。</summary>
            TempleBlast,

            /// <summary>10本目：ピンに当たった瞬間の閃光。</summary>
            ImpactFlash,
        }

        [Tooltip("どの仕掛けの知らせを聞くか。")]
        [SerializeField] private Trigger trigger;

        private AudioSource _source;
        private TubeLane _tube;
        private TempleBlast _temple;
        private CoasterImpactFlash _flash;

        private void Awake()
        {
            _source = LaneSoundUtil.CreateSource(gameObject);
        }

        private void Start()
        {
            Transform root = LaneSoundUtil.LaneRoot(transform);
            switch (trigger)
            {
                case Trigger.JetThrust:
                    _tube = root.GetComponentInChildren<TubeLane>(true);
                    if (_tube != null)
                    {
                        _tube.ThrustStarted += OnFired;
                    }
                    break;
                case Trigger.TempleBlast:
                    _temple = root.GetComponentInChildren<TempleBlast>(true);
                    if (_temple != null)
                    {
                        _temple.Exploded += OnFired;
                    }
                    break;
                case Trigger.ImpactFlash:
                    _flash = root.GetComponentInChildren<CoasterImpactFlash>(true);
                    if (_flash != null)
                    {
                        _flash.Fired += OnFired;
                    }
                    break;
            }

            if (_tube == null && _temple == null && _flash == null)
            {
                Debug.LogWarning($"音：{trigger} の知らせを出す仕掛けがこのレーンに見つからない", this);
            }
        }

        private void OnDestroy()
        {
            if (_tube != null)
            {
                _tube.ThrustStarted -= OnFired;
            }
            if (_temple != null)
            {
                _temple.Exploded -= OnFired;
            }
            if (_flash != null)
            {
                _flash.Fired -= OnFired;
            }
        }

        private void OnFired()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || player.Table == null)
            {
                return;
            }

            SoundTable table = player.Table;
            switch (trigger)
            {
                case Trigger.JetThrust:
                    player.PlayOn(_source, table.lane07Jet, "7本目：ジェット");
                    break;
                case Trigger.TempleBlast:
                    player.PlayOn(_source, table.lane09Blast, "9本目：爆発");
                    break;
                case Trigger.ImpactFlash:
                    player.PlayOn(_source, table.lane10Zap, "10本目：閃光");
                    break;
            }
        }
    }
}
