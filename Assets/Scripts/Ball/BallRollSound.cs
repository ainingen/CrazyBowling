using CrazyBowling.Core;
using CrazyBowling.Data;
using CrazyBowling.Pins;
using UnityEngine;

namespace CrazyBowling.Ball
{
    /// <summary>
    /// ボールが転がる音（段階6）。ボール（Rigidbody の付いたもの）に付ける。
    /// 速いほど高く大きく鳴らす。床に接しているときだけ鳴らし、跳んでいる間・筒の壁を登っている間は小さくする。
    /// 10本目のレールの上（外から動かされている間）は転がっているとみなす。
    /// 決着したら止める。真空のレーン（8本目）では鳴らさない。
    /// 音を聞くだけで、ボールの動きには一切触らない。
    /// </summary>
    public class BallRollSound : MonoBehaviour
    {
        [Tooltip("転がる状態を読むボール。空なら同じ物から探す。")]
        [SerializeField] private BallController ball;

        private Rigidbody _body;
        private AudioSource _source;
        private Vector3 _previousPosition;
        private bool _hasPrevious;
        private float _speed;
        private float _lastFloorContact = float.NegativeInfinity;
        private float _volume;
        private bool _inAir;

        /// <summary>今の速さ（位置の変わり方から測る。レールの上でも正しく出る）。</summary>
        public float Speed => _speed;

        /// <summary>今、床に接しているとみなしているか。</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>転がる音が鳴っているか（確かめるとき用）。</summary>
        public bool IsPlaying => _source != null && _source.isPlaying;

        private void Awake()
        {
            if (ball == null)
            {
                ball = GetComponent<BallController>();
            }
            _body = GetComponent<Rigidbody>();

            var child = new GameObject("転がる音");
            child.transform.SetParent(transform, false);
            _source = child.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        private void FixedUpdate()
        {
            Vector3 position = _body != null ? _body.position : transform.position;
            if (_hasPrevious && Time.fixedDeltaTime > 0f)
            {
                _speed = (position - _previousPosition).magnitude / Time.fixedDeltaTime;
            }
            _previousPosition = position;
            _hasPrevious = true;
        }

        private void OnCollisionStay(Collision collision)
        {
            // ピンに触れているのは床ではない
            if (collision.rigidbody != null && collision.rigidbody.GetComponent<Pin>() != null)
            {
                return;
            }

            SoundPlayer player = SoundPlayer.Instance;
            float minY = player != null && player.Table != null ? player.Table.rollFloorNormalMinY : 0.55f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= minY)
                {
                    _lastFloorContact = Time.time;
                    return;
                }
            }
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || player.Table == null || ball == null)
            {
                StopNow(null);
                return;
            }

            SoundTable table = player.Table;
            SoundEntry entry = table.ballRoll;
            var settings = new RollSoundSettings
            {
                minSpeed = table.rollMinSpeed,
                maxSpeed = table.rollMaxSpeed,
                volumeAtMin = table.rollVolumeAtMin,
                pitchAtMin = table.rollPitchAtMin,
                pitchAtMax = table.rollPitchAtMax,
                volumeInAir = table.rollVolumeInAir,
            };

            bool rolling = ball.IsRolling;
            IsGrounded = ball.IsExternallyDriven || Time.time - _lastFloorContact <= table.rollAirGraceSeconds;
            bool allowed = rolling && !player.IsVacuum && player.CanPlay(entry);
            float target = allowed ? RollSound.TargetVolume(_speed, IsGrounded, settings) : 0f;
            _volume = RollSound.Approach(_volume, target, table.rollVolumeResponse, Time.deltaTime);

            if (!_source.isPlaying)
            {
                if (target > 0.001f && player.StartLoop(_source, entry, "転がる：鳴らし始めた", 0f))
                {
                    _inAir = false;
                }
                else
                {
                    return;
                }
            }

            _source.volume = player.SeVolume(entry) * _volume;
            _source.pitch = RollSound.Pitch(_speed, settings);

            // 床から離れた・戻ったを記録する（跳んでいる間に止めているかを確かめるため）
            if (rolling && !IsGrounded && !_inAir)
            {
                _inAir = true;
                player.Record("転がる：床から離れた");
            }
            else if (rolling && IsGrounded && _inAir)
            {
                _inAir = false;
                player.Record("転がる：床に戻った");
            }

            if (!rolling && _volume < 0.002f)
            {
                StopNow(player);
            }
        }

        private void OnDisable()
        {
            StopNow(SoundPlayer.Instance);
        }

        private void StopNow(SoundPlayer player)
        {
            if (_source == null || !_source.isPlaying)
            {
                return;
            }
            _source.Stop();
            _volume = 0f;
            if (player != null)
            {
                player.Record("転がる：止めた");
            }
        }
    }
}
