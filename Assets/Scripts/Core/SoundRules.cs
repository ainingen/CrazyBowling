using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>判定のときに鳴らす歓声の種類（段階6）。</summary>
    public enum JudgementSoundKind
    {
        /// <summary>鳴らさない。</summary>
        None,

        /// <summary>ストライク。</summary>
        Strike,

        /// <summary>スペア。</summary>
        Spare,

        /// <summary>1本も倒れなかった投。</summary>
        Gutter,
    }

    /// <summary>判定の結果から、鳴らす歓声を選ぶ（段階6）。</summary>
    public static class JudgementSound
    {
        /// <summary>
        /// 鳴らす歓声を選ぶ。真空のレーン（8本目）では観客がいないので鳴らさない。
        /// ガターは STRIKE! などの演出と同じく「その投で1本も倒れなかった」とき。
        /// </summary>
        public static JudgementSoundKind Choose(ThrowJudgement judgement, bool vacuum)
        {
            if (vacuum)
            {
                return JudgementSoundKind.None;
            }
            if (judgement.isStrike)
            {
                return JudgementSoundKind.Strike;
            }
            if (judgement.isSpare)
            {
                return JudgementSoundKind.Spare;
            }
            return judgement.fallen == 0 ? JudgementSoundKind.Gutter : JudgementSoundKind.None;
        }
    }

    /// <summary>転がる音の数値（段階6）。</summary>
    public struct RollSoundSettings
    {
        /// <summary>これより遅いと鳴らさない（m/s）。</summary>
        public float minSpeed;

        /// <summary>この速さでいちばん大きく・高くなる（m/s）。</summary>
        public float maxSpeed;

        /// <summary>いちばん遅いときの音量の割合。</summary>
        public float volumeAtMin;

        /// <summary>いちばん遅いときの音の高さ。</summary>
        public float pitchAtMin;

        /// <summary>いちばん速いときの音の高さ。</summary>
        public float pitchAtMax;

        /// <summary>床から離れているときの音量の割合。</summary>
        public float volumeInAir;
    }

    /// <summary>転がる音の大きさと高さを、速さと床との接し方から決める（段階6）。</summary>
    public static class RollSound
    {
        /// <summary>速さを 0〜1 にならす（遅い 0 → 速い 1）。</summary>
        public static float SpeedRatio(float speed, RollSoundSettings s)
        {
            float span = Mathf.Max(s.maxSpeed - s.minSpeed, 0.001f);
            return Mathf.Clamp01((speed - s.minSpeed) / span);
        }

        /// <summary>目標の音量の割合（0〜1）。遅すぎれば 0。床から離れていれば「空中の割合」をかける。</summary>
        public static float TargetVolume(float speed, bool grounded, RollSoundSettings s)
        {
            if (speed < s.minSpeed)
            {
                return 0f;
            }
            float volume = Mathf.Lerp(s.volumeAtMin, 1f, SpeedRatio(speed, s));
            return grounded ? volume : volume * Mathf.Clamp01(s.volumeInAir);
        }

        /// <summary>音の高さ。速いほど高い。</summary>
        public static float Pitch(float speed, RollSoundSettings s)
        {
            return Mathf.Lerp(s.pitchAtMin, s.pitchAtMax, SpeedRatio(speed, s));
        }

        /// <summary>今の値を目標へなめらかに近づける（急に途切れたり跳ねたりしないため）。</summary>
        public static float Approach(float current, float target, float response, float deltaTime)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(response, 0f) * Mathf.Max(deltaTime, 0f));
            return Mathf.Lerp(current, target, k);
        }
    }

    /// <summary>ピンの当たりの音の種類（段階6）。</summary>
    public enum PinHitKind
    {
        /// <summary>鳴らさない。</summary>
        None,

        /// <summary>弱い当たり。</summary>
        Weak,

        /// <summary>強い当たり。</summary>
        Strong,
    }

    /// <summary>
    /// ピンの当たりの音を鳴らすかを決める（段階6）。
    /// 遅すぎる当たりは鳴らさない／同時に鳴っている数が上限なら鳴らさない／同じピンは間を空ける。
    /// </summary>
    public class PinHitGate
    {
        private readonly Dictionary<int, float> _lastTimes = new Dictionary<int, float>();

        /// <summary>これより遅い当たりは鳴らさない（m/s）。</summary>
        public float MinSpeed { get; set; }

        /// <summary>これ以上なら強い当たり（m/s）。</summary>
        public float StrongSpeed { get; set; }

        /// <summary>同じピンが続けて鳴らないように空ける間（秒）。</summary>
        public float Cooldown { get; set; }

        /// <summary>同時に鳴らす数の上限。</summary>
        public int MaxVoices { get; set; }

        public PinHitGate(float minSpeed, float strongSpeed, float cooldown, int maxVoices)
        {
            MinSpeed = minSpeed;
            StrongSpeed = strongSpeed;
            Cooldown = cooldown;
            MaxVoices = maxVoices;
        }

        /// <summary>
        /// 鳴らすかを決める。鳴らすときは、当たった2つ（ピン同士ならどちらも）の時刻を記録する。
        /// </summary>
        /// <param name="idA">当たったピン。</param>
        /// <param name="hasB">相手がピンか（ボールなら false）。</param>
        /// <param name="idB">相手のピン（hasB が false なら使わない）。</param>
        /// <param name="speed">ぶつかる速さ（m/s）。</param>
        /// <param name="now">今の時刻（秒）。</param>
        /// <param name="activeVoices">今鳴っているピンの音の数。</param>
        public PinHitKind Decide(int idA, bool hasB, int idB, float speed, float now, int activeVoices)
        {
            if (speed < MinSpeed)
            {
                return PinHitKind.None;
            }
            if (activeVoices >= Mathf.Max(MaxVoices, 0))
            {
                return PinHitKind.None;
            }
            if (IsCooling(idA, now) || (hasB && IsCooling(idB, now)))
            {
                return PinHitKind.None;
            }

            _lastTimes[idA] = now;
            if (hasB)
            {
                _lastTimes[idB] = now;
            }
            return speed >= StrongSpeed ? PinHitKind.Strong : PinHitKind.Weak;
        }

        /// <summary>記録を消す（レーンや投げ直しのとき）。</summary>
        public void Reset()
        {
            _lastTimes.Clear();
        }

        private bool IsCooling(int id, float now)
        {
            return _lastTimes.TryGetValue(id, out float last) && now - last < Cooldown;
        }
    }

    /// <summary>決めた間隔より短い間隔では通さない（段階6。得点の数え上げの音に使う）。</summary>
    public class IntervalGate
    {
        private float _last = float.NegativeInfinity;

        /// <summary>通す間隔の下限（秒）。</summary>
        public float MinInterval { get; set; }

        public IntervalGate(float minInterval)
        {
            MinInterval = minInterval;
        }

        /// <summary>前に通してから下限の間隔が過ぎていれば通す。</summary>
        public bool TryPass(float now)
        {
            if (now - _last < MinInterval)
            {
                return false;
            }
            _last = now;
            return true;
        }
    }

    /// <summary>
    /// 7本目のジェットの音を鳴らすかを決める（段階6）。
    /// 筒に入ったら鳴らす。推力が効き始めたときは、前の音から間が空いていれば「吹き出した」としてもう一度鳴らす。
    /// 1投に鳴らす回数に上限を付けて、うるさくならないようにする。
    /// </summary>
    public class JetSoundGate
    {
        private int _count;
        private float _last = float.NegativeInfinity;

        /// <summary>前の音からこれより短い間は、推力の音を重ねない（秒）。</summary>
        public float RepeatGap { get; set; }

        /// <summary>1投に鳴らす回数の上限。</summary>
        public int MaxPerThrow { get; set; }

        /// <summary>この投で鳴らした回数。</summary>
        public int Count => _count;

        public JetSoundGate(float repeatGap, int maxPerThrow)
        {
            RepeatGap = repeatGap;
            MaxPerThrow = maxPerThrow;
        }

        /// <summary>新しい投になった。数え直す。</summary>
        public void ResetThrow()
        {
            _count = 0;
            _last = float.NegativeInfinity;
        }

        /// <summary>筒に入った。上限に達していなければ鳴らす（鳴らすなら true）。</summary>
        public bool TryEnter(float now)
        {
            if (_count >= Mathf.Max(MaxPerThrow, 0))
            {
                return false;
            }
            _count++;
            _last = now;
            return true;
        }

        /// <summary>推力が効き始めた。前の音から間が空いていて、上限に達していなければ鳴らす（鳴らすなら true）。</summary>
        public bool TryThrust(float now)
        {
            if (_count >= Mathf.Max(MaxPerThrow, 0) || now - _last < RepeatGap)
            {
                return false;
            }
            _count++;
            _last = now;
            return true;
        }
    }

    /// <summary>タイトルのナレーションを流す・止める決まり（段階6）。</summary>
    public static class TitleNarrationRule
    {
        /// <summary>
        /// ナレーションを流し始めてよいか。音を消していたら流さない。
        /// タイトルが出ていない・閉じ始めていたら流さない。
        /// </summary>
        public static bool CanStart(bool muted, bool titleVisible, bool titleClosing)
        {
            return !muted && titleVisible && !titleClosing;
        }

        /// <summary>流れているナレーションを、小さくして止め始めるか（タイトルが閉じる・閉じ始めた）。</summary>
        public static bool ShouldFadeOut(bool titleVisible, bool titleClosing)
        {
            return !titleVisible || titleClosing;
        }

        /// <summary>
        /// 流れているナレーションを、すぐ止めるか（途中で音を消した）。
        /// 音を戻しても、途中からは流さない。
        /// </summary>
        public static bool ShouldStopNow(bool muted)
        {
            return muted;
        }
    }

    /// <summary>音量の数え方（段階6）。</summary>
    public static class SoundLevel
    {
        /// <summary>dB を音量の倍率にする（-10dB → 約0.316、0dB → 1）。</summary>
        public static float DbToLinear(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }
    }

    /// <summary>床の起伏の山（頂上）を探す（段階6。3本目の「ぽちゃん」に使う）。</summary>
    public static class LaneCrests
    {
        /// <summary>
        /// 等間隔に並べた床の高さから、山の頂上の番号を探す。
        /// 両側の低い所より minProminence 以上高い山だけを数える（小さなうねりは山にしない）。
        /// </summary>
        /// <param name="heights">奥行きの順に並べた床の高さ。</param>
        /// <param name="window">左右それぞれ、低い所を探す範囲（何個ぶん）。</param>
        /// <param name="minProminence">山とみなす高さの差の下限。</param>
        public static List<int> Find(IReadOnlyList<float> heights, int window, float minProminence)
        {
            var crests = new List<int>();
            if (heights == null || heights.Count < 3)
            {
                return crests;
            }

            for (int i = 1; i < heights.Count - 1; i++)
            {
                float h = heights[i];
                // 頂上：左より高く、右以上（平らな頂上は最初の1点だけ）
                if (!(h > heights[i - 1] && h >= heights[i + 1]))
                {
                    continue;
                }

                float leftLow = h;
                for (int k = i - 1; k >= Mathf.Max(0, i - window); k--)
                {
                    leftLow = Mathf.Min(leftLow, heights[k]);
                }
                float rightLow = h;
                for (int k = i + 1; k <= Mathf.Min(heights.Count - 1, i + window); k++)
                {
                    rightLow = Mathf.Min(rightLow, heights[k]);
                }

                if (h - leftLow >= minProminence && h - rightLow >= minProminence)
                {
                    crests.Add(i);
                }
            }
            return crests;
        }

        /// <summary>前の位置から今の位置のあいだで、奥へ向かって mark を越えたか。</summary>
        public static bool CrossedForward(float previous, float current, float mark)
        {
            return previous < mark && current >= mark;
        }
    }

    /// <summary>ランダムな間隔と、候補からの選び方（段階6。8本目の交信・2本目の汽笛）。</summary>
    public static class SoundSchedule
    {
        /// <summary>範囲の中から間隔を決める。random01 は 0〜1 の乱数。範囲の大小が逆でもよい。</summary>
        public static float NextDelay(Vector2 range, float random01)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return Mathf.Lerp(min, max, Mathf.Clamp01(random01));
        }

        /// <summary>
        /// 候補から1つ選ぶ。候補が2つ以上あれば、前回と同じものは選ばない。
        /// random01 は 0〜1 の乱数。候補が無ければ -1。
        /// </summary>
        public static int PickIndex(int count, int last, float random01)
        {
            if (count <= 0)
            {
                return -1;
            }
            if (count == 1)
            {
                return 0;
            }

            bool avoid = last >= 0 && last < count;
            int choices = avoid ? count - 1 : count;
            int pick = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(random01) * choices), choices - 1);
            if (avoid && pick >= last)
            {
                pick++;
            }
            return pick;
        }
    }
}
