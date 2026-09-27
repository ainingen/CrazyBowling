using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Data
{
    /// <summary>
    /// 効果音1つぶんの設定（段階6）。鳴らす音と、その音量・高さの揺れ。
    /// </summary>
    [System.Serializable]
    public class SoundEntry
    {
        [Tooltip("鳴らす音。複数入れると、鳴らすたびにその中から選ぶ。")]
        public AudioClip[] clips = new AudioClip[0];

        [Tooltip("音量（0〜1）。1 がファイルそのままの大きさ。")]
        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("鳴らすたびに音の高さを変える幅（0 なら変えない）。0.05 で ±5%。")]
        [Range(0f, 0.3f)] public float pitchRandom = 0f;

        /// <summary>鳴らせる音が入っているか。</summary>
        public bool HasClip
        {
            get
            {
                if (clips == null)
                {
                    return false;
                }
                foreach (AudioClip clip in clips)
                {
                    if (clip != null)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>レーンごとの BGM（段階6。曲は揃ってから入れる）。</summary>
    [System.Serializable]
    public class LaneBgm
    {
        [Tooltip("どのレーンの曲か。")]
        public LaneData lane;

        [Tooltip("流す曲。空ならこのレーンでは曲を流さない。")]
        public AudioClip clip;

        [Tooltip("曲の音量（0〜1）。")]
        [Range(0f, 1f)] public float volume = 0.6f;
    }

    /// <summary>
    /// 音の表（段階6）。効果音の音量・鳴らし方の数値と、BGM の割り当てを1か所にまとめる。
    /// Project ウィンドウでこのアセットを選ぶと、Inspector で音量を直せる（Play 中に直した値は止めても残る）。
    /// 判定・得点・物理には関わらない。
    /// </summary>
    [CreateAssetMenu(fileName = "SoundTable", menuName = "CrazyBowling/音の表", order = 10)]
    public class SoundTable : ScriptableObject
    {
        [Header("全体")]
        [Tooltip("効果音ぜんぶにかける音量（0〜1）。")]
        [Range(0f, 1f)] public float seMaster = 1f;

        [Tooltip("BGM ぜんぶにかける音量（0〜1）。")]
        [Range(0f, 1f)] public float bgmMaster = 1f;

        [Header("ボール")]
        [Tooltip("投げた瞬間。")]
        public SoundEntry ballRelease = new SoundEntry();

        [Tooltip("転がる音（繰り返し鳴らす）。音量は下の「転がる音」の速さで変わる。")]
        public SoundEntry ballRoll = new SoundEntry();

        [Header("転がる音")]
        [Tooltip("この速さ（m/s）より遅いと鳴らさない。")]
        public float rollMinSpeed = 0.4f;

        [Tooltip("この速さ（m/s）でいちばん大きく・高くなる。")]
        public float rollMaxSpeed = 9f;

        [Tooltip("いちばん遅いときの音量の割合（0〜1）。")]
        [Range(0f, 1f)] public float rollVolumeAtMin = 0.25f;

        [Tooltip("いちばん遅いときの音の高さ（1 がそのまま）。")]
        public float rollPitchAtMin = 0.75f;

        [Tooltip("いちばん速いときの音の高さ（1 がそのまま）。")]
        public float rollPitchAtMax = 1.15f;

        [Tooltip("床から離れているとき（跳んでいる・筒の壁を登っている）の音量の割合（0 で止める）。")]
        [Range(0f, 1f)] public float rollVolumeInAir = 0f;

        [Tooltip("音量が目標へ近づく速さ（1秒あたり）。大きいほど急に変わる。")]
        public float rollVolumeResponse = 12f;

        [Tooltip("この角度より上向きの面に触れていれば「床に接している」とみなす（面の向きの上向き成分、0〜1）。")]
        [Range(0f, 1f)] public float rollFloorNormalMinY = 0.55f;

        [Tooltip("床から離れてから、離れたとみなすまでの間（秒）。小さな跳ねで音が途切れないように。")]
        public float rollAirGraceSeconds = 0.08f;

        [Header("ピン")]
        [Tooltip("強い当たり（ボールがピンに厚く当たったときなど）。")]
        public SoundEntry pinHitStrong = new SoundEntry();

        [Tooltip("弱い当たり（ピン同士が軽く当たったときなど）。")]
        public SoundEntry pinHitWeak = new SoundEntry();

        [Tooltip("ぶつかる速さ（m/s）がこれより遅いと鳴らさない。")]
        public float pinMinSpeed = 0.8f;

        [Tooltip("ぶつかる速さ（m/s）がこれ以上なら「強」の音。")]
        public float pinStrongSpeed = 3f;

        [Tooltip("ピンの音を同時に鳴らす数の上限。")]
        public int pinMaxVoices = 4;

        [Tooltip("同じピンが続けて鳴らないように空ける間（秒）。")]
        public float pinCooldownSeconds = 0.15f;

        [Tooltip("「強」の音を鳴らす長さの上限（秒）。元の音は約4秒あるので、途中でなめらかに消す。")]
        public float pinStrongMaxSeconds = 1.4f;

        [Tooltip("「弱」の音を鳴らす長さの上限（秒）。")]
        public float pinWeakMaxSeconds = 0.6f;

        [Tooltip("長さの上限に来てから消えるまでの時間（秒）。")]
        public float pinFadeSeconds = 0.25f;

        [Header("判定の歓声")]
        public SoundEntry strike = new SoundEntry();
        public SoundEntry spare = new SoundEntry();

        [Tooltip("1本も倒れなかった投。")]
        public SoundEntry gutter = new SoundEntry();

        [Header("画面")]
        [Tooltip("ボタンを押したとき。")]
        public SoundEntry uiButton = new SoundEntry();

        [Tooltip("得点が数え上がるとき。")]
        public SoundEntry scoreCount = new SoundEntry();

        [Tooltip("数え上げの音を鳴らす間隔の下限（秒）。数字が1つ増えるたびに鳴らさないため。")]
        public float scoreCountMinInterval = 0.09f;

        [Tooltip("結果画面で RANK を出す前のドラムロール。")]
        public SoundEntry drumroll = new SoundEntry();

        [Tooltip("ドラムロールを入れるか。入れると、RANK が出るのがドラムロールの長さぶん遅れる。")]
        public bool useDrumroll = true;

        [Tooltip("RANK が出る瞬間。")]
        public SoundEntry rank = new SoundEntry();

        [Header("レーン専用")]
        [Tooltip("2本目：汽笛。")]
        public SoundEntry lane02Horn = new SoundEntry();

        [Tooltip("2本目：レーンに入ってから最初の汽笛までの間（秒）。")]
        public float lane02FirstHornDelay = 1.2f;

        [Tooltip("2本目：そのあと汽笛を鳴らす間隔（秒）の最小と最大。この間でばらつかせる。")]
        public Vector2 lane02HornInterval = new Vector2(16f, 24f);

        [Tooltip("3本目：ボールが起伏の山を越えるときの「ぽちゃん」。")]
        public SoundEntry lane03Water = new SoundEntry();

        [Tooltip("6本目：跳び台で跳んだとき。")]
        public SoundEntry lane06Jump = new SoundEntry();

        [Tooltip("7本目：ボールが筒（エンジン）に入ったとき。止まりかけてから推力で押し出されたときも、もう一度鳴らす。")]
        public SoundEntry lane07Jet = new SoundEntry();

        [Tooltip("7本目：前のジェットの音からこれより短い間に推力が効いたら、重ねて鳴らさない（秒）。")]
        public float lane07JetRepeatGap = 2f;

        [Tooltip("7本目：1投にジェットの音を鳴らす回数の上限。")]
        public int lane07JetMaxPerThrow = 2;

        [Tooltip("9本目：神殿が吹き飛ぶとき。")]
        public SoundEntry lane09Blast = new SoundEntry();

        [Tooltip("10本目：ピンに当たった瞬間の閃光。")]
        public SoundEntry lane10Zap = new SoundEntry();

        [Header("8本目（月面）の無線")]
        [Tooltip("ずっと小さく流す雑音（繰り返し鳴らす）。")]
        public SoundEntry lane08Noise = new SoundEntry();

        [Tooltip("交信の前の「ヒュイーン」。入れた中から毎回選ぶ。")]
        public SoundEntry lane08Sweep = new SoundEntry();

        [Tooltip("交信の始まりと終わりの「ピッ」。")]
        public SoundEntry lane08Beep = new SoundEntry();

        [Tooltip("交信の声。入れた中から毎回選ぶ。")]
        public SoundEntry lane08Voice = new SoundEntry();

        [Tooltip("レーンに入ってから最初の交信までの間（秒）の最小と最大。1投で終わっても1回は聞こえるよう、早めにしてある。")]
        public Vector2 lane08FirstDelay = new Vector2(3f, 6f);

        [Tooltip("交信と交信の間隔（秒）の最小と最大。この間でばらつかせる。")]
        public Vector2 lane08Interval = new Vector2(12f, 25f);

        [Tooltip("交信の中の音と音の間（秒）。ヒュイーン→ピッ、ピッ→声、声→ピッ。")]
        public float lane08Gap = 0.15f;

        [Header("BGM（曲は揃ってから入れる。空なら流さない）")]
        [Tooltip("タイトル画面の曲。")]
        public AudioClip titleBgm;

        [Range(0f, 1f)] public float titleBgmVolume = 0.6f;

        [Tooltip("結果画面の曲。")]
        public AudioClip resultBgm;

        [Range(0f, 1f)] public float resultBgmVolume = 0.6f;

        [Tooltip("レーンごとの曲。★真空のレーン（8本目）では、入っていても流さない。")]
        public List<LaneBgm> laneBgm = new List<LaneBgm>();

        [Tooltip("曲が変わるときに、前の曲を小さくし次の曲を大きくする時間（秒）。")]
        public float bgmCrossfadeSeconds = 1.5f;

        /// <summary>このレーンの曲の設定を探す。無ければ null。</summary>
        public LaneBgm FindLaneBgm(LaneData lane)
        {
            if (lane == null || laneBgm == null)
            {
                return null;
            }
            foreach (LaneBgm entry in laneBgm)
            {
                if (entry != null && entry.lane == lane)
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
