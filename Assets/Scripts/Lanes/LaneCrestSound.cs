using System.Collections.Generic;
using CrazyBowling.Ball;
using CrazyBowling.Core;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 3本目（ウォーターベッド）の「ぽちゃん」（段階6）。レーンのプレハブの中に置く。
    /// 床の形から起伏の山（頂上）を探しておき、ボールがそこを奥へ越えたときに鳴らす。
    /// 鳴らすたびに音の高さを少し変える（元の音が1つしかないため。変える幅は音の表で直す）。
    /// ボールと床には触らない。
    /// </summary>
    public class LaneCrestSound : MonoBehaviour
    {
        [Tooltip("床の形を持つレーン。空なら親から探す。")]
        [SerializeField] private ShapedLane shapedLane;

        [Tooltip("山を探すときに床の高さを測る間隔（m）。")]
        [SerializeField] private float sampleStep = 0.05f;

        [Tooltip("左右それぞれ、低い所を探す範囲（m）。3本目の山は 15m ほどかけてゆるく上がるので、広く見る。")]
        [SerializeField] private float searchRange = 8f;

        [Tooltip("山とみなす高さの差の下限（m）。小さなうねりは山にしない。")]
        [SerializeField] private float minProminence = 0.01f;

        private AudioSource _source;
        private readonly List<float> _crestZ = new List<float>();
        private float _previousZ = float.NegativeInfinity;

        /// <summary>見つけた山の位置（レーンの奥行き、m）。確かめるとき用。</summary>
        public IReadOnlyList<float> CrestZ => _crestZ;

        private void Awake()
        {
            _source = LaneSoundUtil.CreateSource(gameObject);
            if (shapedLane == null)
            {
                shapedLane = GetComponentInParent<ShapedLane>();
            }
        }

        private void Start()
        {
            FindCrests();
        }

        /// <summary>床の中央の線に沿って高さを測り、山を探す。</summary>
        private void FindCrests()
        {
            _crestZ.Clear();
            if (shapedLane == null)
            {
                return;
            }

            LaneShapeSettings shape = shapedLane.Shape;
            float step = Mathf.Max(sampleStep, 0.01f);
            var heights = new List<float>();
            for (float z = 0f; z <= shape.length; z += step)
            {
                heights.Add(LaneShape.SampleHeight(0f, z, shape));
            }

            int window = Mathf.Max(1, Mathf.RoundToInt(searchRange / step));
            foreach (int index in LaneCrests.Find(heights, window, minProminence))
            {
                _crestZ.Add(index * step);
            }
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            BallController ball = player != null ? player.Ball : null;
            if (ball == null || shapedLane == null || !ball.IsRolling)
            {
                _previousZ = float.NegativeInfinity;
                return;
            }

            float z = shapedLane.FloorBasis.InverseTransformPoint(ball.transform.position).z;
            foreach (float crest in _crestZ)
            {
                if (LaneCrests.CrossedForward(_previousZ, z, crest))
                {
                    player.PlayOn(_source, player.Table.lane03Water, $"3本目：ぽちゃん（{crest:F1}m の山）");
                }
            }
            _previousZ = z;
        }
    }
}
