using CrazyBowling.Ball;
using CrazyBowling.Core;
using CrazyBowling.Data;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 7本目（飛行機の右翼とジェットエンジン）のジェットの音（段階6）。レーンのプレハブの中に置く。
    /// ボールが筒（エンジン）に入った瞬間に鳴らす。筒の口を越えたときに筒の断面の外にいた球（外れて落ちた球など）では鳴らさない。
    /// 推力が効き始めたときは、前の音から間が空いていれば「吹き出した」としてもう一度鳴らす（重ねない・1投の上限あり）。
    /// ボールの位置と、推力の知らせを聞くだけで、ボールと推力には触らない。
    /// </summary>
    public class LaneJetSound : MonoBehaviour
    {
        [Tooltip("筒を持つレーン。空なら親から探す。")]
        [SerializeField] private TubeLane tubeLane;

        [Tooltip("筒の口を越えたとき、ボールの中心が筒の軸から「半径＋この値」以内なら、筒に入ったとみなす（m）。")]
        [SerializeField] private float entryMargin = 0.1f;

        private AudioSource _source;
        private JetSoundGate _gate;
        private float _previousZ = float.NegativeInfinity;

        private void Awake()
        {
            _source = LaneSoundUtil.CreateSource(gameObject);
            _gate = new JetSoundGate(2f, 2);
            if (tubeLane == null)
            {
                tubeLane = GetComponentInParent<TubeLane>();
            }
        }

        private void Start()
        {
            if (tubeLane != null)
            {
                tubeLane.ThrustStarted += OnThrustStarted;
            }
            else
            {
                Debug.LogWarning("音：7本目の筒（TubeLane）が見つからない", this);
            }
        }

        private void OnDestroy()
        {
            if (tubeLane != null)
            {
                tubeLane.ThrustStarted -= OnThrustStarted;
            }
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            BallController ball = player != null ? player.Ball : null;
            if (ball == null || tubeLane == null || player.Table == null)
            {
                return;
            }

            // 構えに戻ったら、新しい投として数え直す
            if (!ball.IsInPlay)
            {
                _gate.ResetThrow();
                _previousZ = float.NegativeInfinity;
                return;
            }
            if (!ball.IsRolling)
            {
                return;
            }

            Vector3 local = tubeLane.transform.InverseTransformPoint(ball.transform.position);
            if (LaneCrests.CrossedForward(_previousZ, local.z, tubeLane.TubeStartZ))
            {
                float fromAxis = new Vector2(local.x, local.y - tubeLane.CenterY).magnitude;
                if (fromAxis <= tubeLane.TubeRadius + entryMargin)
                {
                    ApplyTable(player.Table);
                    if (_gate.TryEnter(Time.time))
                    {
                        player.PlayOn(_source, player.Table.lane07Jet, "7本目：ジェット（筒に入った）");
                    }
                }
                else
                {
                    // 音ではないが、確かめるために記録する
                    player.Record($"7本目：筒に入らなかった（軸から {fromAxis:F2}m）");
                }
            }
            _previousZ = local.z;
        }

        /// <summary>推力が効き始めた。前の音から間が空いていれば、吹き出した音としてもう一度鳴らす。</summary>
        private void OnThrustStarted()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || player.Table == null)
            {
                return;
            }

            ApplyTable(player.Table);
            if (_gate.TryThrust(Time.time))
            {
                player.PlayOn(_source, player.Table.lane07Jet, "7本目：ジェット（推力で吹き出した）");
            }
            else
            {
                player.Record("7本目：推力が効いた（音は重ねない）");
            }
        }

        private void ApplyTable(SoundTable table)
        {
            _gate.RepeatGap = table.lane07JetRepeatGap;
            _gate.MaxPerThrow = table.lane07JetMaxPerThrow;
        }
    }
}
