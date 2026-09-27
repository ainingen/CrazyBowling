using System.Collections;
using CrazyBowling.Core;
using CrazyBowling.Data;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 8本目（月面）の無線（段階6）。レーンのプレハブの中に置く。
    /// 合成の雑音をずっと小さく繰り返し、ときどき交信を1回入れる：ヒュイーン → ピッ → 声 → ピッ。
    /// ヒュイーンと声は、入れた候補から毎回選ぶ（前回と同じものは選ばない）。
    /// 構えている間も投げている間も流れる。レーンを出ると一緒に消えるので止まる。
    /// </summary>
    public class LaneRadioSound : MonoBehaviour
    {
        private AudioSource _noise;
        private AudioSource _talk;
        private float _next = -1f;
        private int _lastSweep = -1;
        private int _lastVoice = -1;
        private bool _talking;

        /// <summary>交信を入れた回数（確かめるとき用）。</summary>
        public int TransmissionCount { get; private set; }

        private void Awake()
        {
            _noise = LaneSoundUtil.CreateSource(gameObject);
            _noise.loop = true;
            _talk = LaneSoundUtil.CreateSource(gameObject);
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || player.Table == null)
            {
                return;
            }
            SoundTable table = player.Table;

            // 雑音：鳴らせる状態になったら鳴らし始め、音量は表に合わせ続ける
            if (!_noise.isPlaying)
            {
                player.StartLoop(_noise, table.lane08Noise, "8本目：雑音 鳴らし始めた");
            }
            else
            {
                _noise.volume = player.SeVolume(table.lane08Noise);
            }

            if (_next < 0f)
            {
                _next = Time.time + SoundSchedule.NextDelay(table.lane08FirstDelay, Random.value);
                return;
            }
            if (_talking || Time.time < _next)
            {
                return;
            }

            StartCoroutine(Transmit(player, table));
        }

        /// <summary>交信を1回入れる。</summary>
        private IEnumerator Transmit(SoundPlayer player, SoundTable table)
        {
            _talking = true;
            TransmissionCount++;
            float gap = Mathf.Max(0f, table.lane08Gap);

            int sweep = SoundSchedule.PickIndex(table.lane08Sweep.clips.Length, _lastSweep, Random.value);
            _lastSweep = sweep;
            float length = player.PlayIndexOn(_talk, table.lane08Sweep, sweep, $"8本目：交信 ヒュイーン（{sweep + 1}）");
            yield return new WaitForSeconds(length + gap);

            length = player.PlayIndexOn(_talk, table.lane08Beep, 0, "8本目：交信 ピッ（始まり）");
            yield return new WaitForSeconds(length + gap);

            int voice = SoundSchedule.PickIndex(table.lane08Voice.clips.Length, _lastVoice, Random.value);
            _lastVoice = voice;
            length = player.PlayIndexOn(_talk, table.lane08Voice, voice, $"8本目：交信 声（{voice + 1}）");
            yield return new WaitForSeconds(length + gap);

            player.PlayIndexOn(_talk, table.lane08Beep, 0, "8本目：交信 ピッ（終わり）");

            _next = Time.time + SoundSchedule.NextDelay(table.lane08Interval, Random.value);
            _talking = false;
        }

        private void OnDestroy()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player != null && _noise != null && _noise.isPlaying)
            {
                player.Record("8本目：雑音 止めた（レーンを出た）");
            }
        }
    }
}
