using System.Collections.Generic;
using CrazyBowling.Ball;
using CrazyBowling.Data;
using CrazyBowling.Pins;
using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>
    /// 効果音と BGM の鳴らし係（段階6）。シーンに1つ置く。
    /// 既存の知らせ（投げた瞬間・判定）を聞いて鳴らすだけで、判定・得点・物理には関わらない。
    /// 音量はすべて <see cref="SoundTable"/> で直す。
    /// レーン専用の音はレーンのプレハブの中の部品が鳴らす（レーンを出ると一緒に消える）。
    /// ここはそれらの部品にも、音の表・鳴らせるか（真空・ブラウザ）・記録を貸す。
    /// </summary>
    public class SoundPlayer : MonoBehaviour
    {
        /// <summary>鳴らした音の記録1件。確かめるときに数える。</summary>
        public struct SoundRecord
        {
            /// <summary>鳴らした時刻（ゲームの時刻、秒）。</summary>
            public float time;

            /// <summary>何本目のレーンか。</summary>
            public int lane;

            /// <summary>何投目か。</summary>
            public int throwNumber;

            /// <summary>何の音か。</summary>
            public string name;
        }

        /// <summary>ピンの音を鳴らしている1本。長さの上限が来たらなめらかに消す。</summary>
        private class PinVoice
        {
            public AudioSource source;
            public float stopTime;
            public float baseVolume;
        }

        /// <summary>選んだ状態を残すための鍵。</summary>
        private const string MutedKey = "CrazyBowling.SoundMuted";

        /// <summary>シーンに置いた鳴らし係。無ければ null。</summary>
        public static SoundPlayer Instance { get; private set; }

        [Header("参照")]
        [Tooltip("音の表。音量はここで直す。")]
        [SerializeField] private SoundTable table;

        [SerializeField] private GameManager gameManager;
        [SerializeField] private ThrowSequencer throwSequencer;
        [SerializeField] private BallController ballController;

        [Tooltip("タイトル画面。出ている間はタイトルの曲にする。空なら探す。")]
        [SerializeField] private UI.TitleView titleView;

        [Tooltip("タイトルの前の CLICK TO TUNE IN の画面。出ている間は BGM を流さない。空なら探す。")]
        [SerializeField] private UI.TuneInView tuneInView;

        [Header("同時に鳴らせる数")]
        [Tooltip("画面・判定などの音を同時に鳴らせる数。")]
        [SerializeField] private int oneShotVoices = 10;

        [Tooltip("ピンの音のために用意する数。実際の上限は音の表の「ピンの音を同時に鳴らす数の上限」。")]
        [SerializeField] private int pinVoicePool = 8;

        [Header("記録")]
        [Tooltip("鳴らした音を1つずつ Console に出す（確かめるとき用。ふだんは切っておく）。")]
        [SerializeField] private bool logEachSound;

        [Tooltip("記録を残す件数の上限。")]
        [SerializeField] private int maxRecords = 20000;

        private readonly List<SoundRecord> _records = new List<SoundRecord>();
        private readonly List<AudioSource> _oneShots = new List<AudioSource>();
        private readonly List<PinVoice> _pinVoices = new List<PinVoice>();
        private PinHitGate _pinGate;
        private IntervalGate _scoreGate;
        private AudioSource[] _bgm;
        private int _bgmCurrent;
        private AudioClip _bgmClip;
        private float _bgmTargetVolume;
        private bool _muted;
        private bool _unlocked;
        private AudioSource _narration;
        private bool _narrationFading;
        private float _narrationFadeStart;
        private float _narrationBaseVolume;
        private float _bgmDuck = 1f;
        private Coroutine _intro;

        /// <summary>音の表。</summary>
        public SoundTable Table => table;

        /// <summary>ボール（レーンの音の部品が使う）。</summary>
        public BallController Ball => ballController;

        /// <summary>鳴らした音の記録。</summary>
        public IReadOnlyList<SoundRecord> Records => _records;

        /// <summary>ピンの音が同時に鳴った数の最大。</summary>
        public int MaxPinVoicesAtOnce { get; private set; }

        /// <summary>上限などで鳴らさなかったピンの当たりの数（遅すぎる当たりは数えない）。</summary>
        public int PinHitsSkipped { get; private set; }

        /// <summary>タイトルのナレーションが流れているか（小さくして止めている途中も含む）。</summary>
        public bool IsNarrationPlaying => _narration != null && _narration.isPlaying;

        /// <summary>今の BGM にかけている倍率（ナレーションの間は小さくする）。確かめるとき用。</summary>
        public float BgmDuck => _bgmDuck;

        /// <summary>今のレーンが真空か（8本目）。ボール・ピン・歓声の音と BGM を鳴らさない。</summary>
        public bool IsVacuum
        {
            get
            {
                LaneData lane = gameManager != null ? gameManager.CurrentLane : null;
                return lane != null && lane.Vacuum;
            }
        }

        /// <summary>
        /// 音を鳴らせる状態か。ブラウザ（Web）では、プレイヤーが一度クリックするまで鳴らさない
        /// （ブラウザがそう決めている）。Windows とエディタでは最初から鳴らせる。
        /// </summary>
        public bool IsUnlocked => _unlocked;

        /// <summary>音を消しているか。選んだ状態は次に起動したときも残る。</summary>
        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                AudioListener.volume = _muted ? 0f : 1f;
                PlayerPrefs.SetInt(MutedKey, _muted ? 1 : 0);
                PlayerPrefs.Save();
                Record(_muted ? "音を消した" : "音を戻した");
            }
        }

        /// <summary>
        /// 結果画面で RANK を出すのを遅らせる時間（秒）。ドラムロールを入れるときだけ、その長さ。
        /// 見た目の時間だけで、得点には関わらない。
        /// </summary>
        public float RankRevealDelay
        {
            get
            {
                if (table == null || !table.useDrumroll || !table.drumroll.HasClip)
                {
                    return 0f;
                }
                AudioClip clip = FirstClip(table.drumroll);
                return clip != null ? clip.length : 0f;
            }
        }

        private void Awake()
        {
            _unlocked = Application.platform != RuntimePlatform.WebGLPlayer;
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            AudioListener.volume = _muted ? 0f : 1f;

            for (int i = 0; i < Mathf.Max(1, oneShotVoices); i++)
            {
                _oneShots.Add(CreateSource("効果音 " + (i + 1)));
            }
            for (int i = 0; i < Mathf.Max(1, pinVoicePool); i++)
            {
                _pinVoices.Add(new PinVoice { source = CreateSource("ピン " + (i + 1)) });
            }
            _bgm = new[] { CreateSource("BGM A"), CreateSource("BGM B") };
            _narration = CreateSource("ナレーション");
            foreach (AudioSource source in _bgm)
            {
                source.loop = true;
            }

            _pinGate = new PinHitGate(0f, 0f, 0f, 0);
            _scoreGate = new IntervalGate(0f);
            ApplyTableToGates();

            if (titleView == null)
            {
                titleView = FindFirstObjectByType<UI.TitleView>(FindObjectsInactive.Include);
            }
            if (tuneInView == null)
            {
                tuneInView = FindFirstObjectByType<UI.TuneInView>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            Instance = this;
            if (throwSequencer != null)
            {
                throwSequencer.ThrowStarted += OnThrowStarted;
                throwSequencer.ThrowJudged += OnThrowJudged;
            }
        }

        private void OnDisable()
        {
            if (throwSequencer != null)
            {
                throwSequencer.ThrowStarted -= OnThrowStarted;
                throwSequencer.ThrowJudged -= OnThrowJudged;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 音を鳴らせるようにする。タイトルの CLICK TO START など、プレイヤーがクリックしたときに呼ぶ。
        /// ブラウザは、クリックの中で音を鳴らし始めないと音を出させない。
        /// </summary>
        public void Unlock()
        {
            if (_unlocked)
            {
                return;
            }
            _unlocked = true;
            Record("音を鳴らせるようにした");
        }

        /// <summary>鳴らした音の記録を消す（確かめるとき用）。</summary>
        public void ClearRecords()
        {
            _records.Clear();
            MaxPinVoicesAtOnce = 0;
            PinHitsSkipped = 0;
        }

        /// <summary>記録に1件足す（鳴らし続ける音の始まり・終わりなども残す）。</summary>
        public void Record(string name)
        {
            if (_records.Count >= Mathf.Max(1, maxRecords))
            {
                _records.RemoveAt(0);
            }
            var record = new SoundRecord
            {
                time = Time.time,
                lane = gameManager != null ? gameManager.LaneNumber : 0,
                throwNumber = throwSequencer != null ? throwSequencer.ThrowNumber : 0,
                name = name,
            };
            _records.Add(record);
            if (logEachSound)
            {
                Debug.Log($"音｜{record.lane}本目 {record.throwNumber}投目｜{name}", this);
            }
        }

        /// <summary>音を1回鳴らす（画面・判定・ボールなど、どこにも属さない音）。鳴らしたら true。</summary>
        public bool Play(SoundEntry entry, string name, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (!CanPlay(entry))
            {
                return false;
            }
            AudioSource source = FindFreeSource();
            return PlayOn(source, entry, name, volumeScale, pitchScale);
        }

        /// <summary>
        /// 指定の AudioSource で音を1回鳴らす。レーンのプレハブの中の部品が使う
        /// （レーンを出るとその AudioSource ごと消えるので、音も止まる）。鳴らしたら true。
        /// </summary>
        public bool PlayOn(AudioSource source, SoundEntry entry, string name, float volumeScale = 1f, float pitchScale = 1f)
        {
            if (source == null || !CanPlay(entry))
            {
                return false;
            }
            AudioClip clip = PickClip(entry);
            if (clip == null)
            {
                return false;
            }

            source.clip = clip;
            source.loop = false;
            source.volume = SeVolume(entry) * Mathf.Max(0f, volumeScale);
            source.pitch = RandomPitch(entry) * pitchScale;
            source.Play();
            Record(name);
            return true;
        }

        /// <summary>
        /// 指定の AudioSource で、入っている音のうち index 番目を鳴らす（8本目の交信で、前回と違う音を選ぶため）。
        /// 鳴らしたら、その音の長さ（秒。高さを変えたぶんも入れる）を返す。鳴らせなければ 0。
        /// </summary>
        public float PlayIndexOn(AudioSource source, SoundEntry entry, int index, string name)
        {
            if (source == null || !CanPlay(entry))
            {
                return 0f;
            }
            AudioClip clip = PickClip(entry, index);
            if (clip == null)
            {
                return 0f;
            }
            source.clip = clip;
            source.loop = false;
            source.volume = SeVolume(entry);
            source.pitch = RandomPitch(entry);
            source.Play();
            Record(name);
            return clip.length / Mathf.Max(source.pitch, 0.01f);
        }

        /// <summary>繰り返し鳴らす音を AudioSource に入れる（転がる音・8本目の雑音）。鳴らし始めたら true。</summary>
        public bool StartLoop(AudioSource source, SoundEntry entry, string name, float volumeScale = 1f)
        {
            if (source == null || !CanPlay(entry))
            {
                return false;
            }
            AudioClip clip = PickClip(entry);
            if (clip == null)
            {
                return false;
            }
            source.clip = clip;
            source.loop = true;
            source.volume = SeVolume(entry) * Mathf.Max(0f, volumeScale);
            source.pitch = 1f;
            source.Play();
            Record(name);
            return true;
        }

        /// <summary>効果音の音量（表の音量 × 効果音ぜんぶの音量）。</summary>
        public float SeVolume(SoundEntry entry)
        {
            return entry != null && table != null ? entry.volume * table.seMaster : 0f;
        }

        /// <summary>この音を今鳴らせるか（音が入っている・ブラウザで鳴らせる状態）。</summary>
        public bool CanPlay(SoundEntry entry)
        {
            return _unlocked && table != null && entry != null && entry.HasClip;
        }

        /// <summary>ボタンを押した。音を鳴らせるようにしてから鳴らす。</summary>
        public void PlayButton()
        {
            Unlock();
            if (table != null)
            {
                Play(table.uiButton, "ボタン");
            }
        }

        /// <summary>得点の数字が変わった。間隔の下限より短ければ鳴らさない。</summary>
        public void PlayScoreTick()
        {
            if (table == null)
            {
                return;
            }
            _scoreGate.MinInterval = table.scoreCountMinInterval;
            if (!_scoreGate.TryPass(Time.unscaledTime))
            {
                return;
            }
            Play(table.scoreCount, "数え上げ");
        }

        /// <summary>結果画面：数え終わった。ドラムロールを入れるなら鳴らす。</summary>
        public void PlayDrumroll()
        {
            if (table != null && table.useDrumroll)
            {
                Play(table.drumroll, "ドラムロール");
            }
        }

        /// <summary>結果画面：RANK が出た。</summary>
        public void PlayRank()
        {
            if (table != null)
            {
                Play(table.rank, "ランク");
            }
        }

        /// <summary>
        /// ピンが何かに当たった（ピンの部品が知らせる）。真空のレーンでは鳴らさない。
        /// </summary>
        /// <param name="pinId">当たったピン。</param>
        /// <param name="otherPinId">相手のピン（相手がボールなら使わない）。</param>
        /// <param name="speed">ぶつかる速さ（m/s）。</param>
        /// <param name="withBall">相手がボールか。</param>
        public void ReportPinHit(int pinId, int otherPinId, float speed, bool withBall)
        {
            if (table == null || IsVacuum || !_unlocked)
            {
                return;
            }

            ApplyTableToGates();
            int active = ActivePinVoices();
            PinHitKind kind = _pinGate.Decide(pinId, !withBall, otherPinId, speed, Time.time, active);
            if (kind == PinHitKind.None)
            {
                if (speed >= table.pinMinSpeed)
                {
                    PinHitsSkipped++;
                }
                return;
            }

            SoundEntry entry = kind == PinHitKind.Strong ? table.pinHitStrong : table.pinHitWeak;
            PinVoice voice = FindFreePinVoice();
            if (voice == null || !CanPlay(entry))
            {
                return;
            }

            // 弱い当たりは速さに合わせて少し小さく（強はそのまま）
            float scale = kind == PinHitKind.Strong ? 1f
                : Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(table.pinMinSpeed, table.pinStrongSpeed, speed));
            string label = (kind == PinHitKind.Strong ? "ピン（強）" : "ピン（弱）") + (withBall ? "・ボールと" : "・ピン同士");
            if (!PlayOn(voice.source, entry, label, scale))
            {
                return;
            }
            voice.baseVolume = voice.source.volume;
            voice.stopTime = Time.time + (kind == PinHitKind.Strong ? table.pinStrongMaxSeconds : table.pinWeakMaxSeconds);
            MaxPinVoicesAtOnce = Mathf.Max(MaxPinVoicesAtOnce, active + 1);
        }

        /// <summary>今鳴っているピンの音の数。</summary>
        public int ActivePinVoices()
        {
            int count = 0;
            foreach (PinVoice voice in _pinVoices)
            {
                if (voice.source.isPlaying)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>今鳴っている音の数（BGM を除く。確かめるとき用）。</summary>
        public int ActiveOneShots()
        {
            int count = 0;
            foreach (AudioSource source in _oneShots)
            {
                if (source.isPlaying)
                {
                    count++;
                }
            }
            return count + ActivePinVoices();
        }

        private void OnThrowStarted()
        {
            _pinGate.Reset();
            if (table != null && !IsVacuum)
            {
                Play(table.ballRelease, "投げた瞬間");
            }
        }

        private void OnThrowJudged(ThrowJudgement judgement)
        {
            if (table == null)
            {
                return;
            }
            switch (JudgementSound.Choose(judgement, IsVacuum))
            {
                case JudgementSoundKind.Strike:
                    Play(table.strike, "歓声（ストライク）");
                    break;
                case JudgementSoundKind.Spare:
                    Play(table.spare, "歓声（スペア）");
                    break;
                case JudgementSoundKind.Gutter:
                    Play(table.gutter, "歓声（ガター）");
                    break;
            }
        }

        /// <summary>
        /// CLICK TO TUNE IN を押した：音を鳴らせるようにして、小さな「ヒュイーン」のあとにナレーションを流す。
        /// 音を消していたら流さない（途中で音を戻しても、途中からは流さない）。
        /// </summary>
        public void BeginTitleIntro()
        {
            Unlock();
            if (table == null)
            {
                return;
            }
            if (_muted)
            {
                Record("ナレーション：音を消しているので流さない");
                return;
            }
            if (_intro != null)
            {
                StopCoroutine(_intro);
            }
            _intro = StartCoroutine(TitleIntro());
        }

        private System.Collections.IEnumerator TitleIntro()
        {
            // 小さなヒュイーン（8本目の周波数を合わせる音から選ぶ）
            int count = table.tuneInSweep.clips != null ? table.tuneInSweep.clips.Length : 0;
            int index = SoundSchedule.PickIndex(count, -1, Random.value);
            float length = 0f;
            if (index >= 0)
            {
                length = PlayIndexOn(FindFreeSource(), table.tuneInSweep, index, "CLICK TO TUNE IN：ヒュイーン（" + (index + 1) + "）");
            }
            yield return new WaitForSecondsRealtime(length + Mathf.Max(0f, table.narrationDelayAfterSweep));
            _intro = null;

            bool visible = titleView != null && titleView.IsVisible;
            bool closing = titleView != null && titleView.IsClosing;
            if (!TitleNarrationRule.CanStart(_muted, visible, closing))
            {
                Record("ナレーション：タイトルが閉じた・音を消したので流さない");
                yield break;
            }
            AudioClip clip = PickClip(table.titleNarration, 0);
            if (clip == null)
            {
                yield break;
            }
            _narration.clip = clip;
            _narration.loop = false;
            _narration.pitch = 1f;
            _narrationBaseVolume = SeVolume(table.titleNarration);
            _narration.volume = _narrationBaseVolume;
            _narrationFading = false;
            _narration.Play();
            Record("ナレーション：始めた");
        }

        /// <summary>ナレーション：タイトルが閉じたら小さくして止める。音を消したらすぐ止める。</summary>
        private void UpdateNarration()
        {
            if (_narration == null || table == null)
            {
                return;
            }

            if (!_narration.isPlaying)
            {
                if (_narrationBaseVolume > 0f)
                {
                    // 止める処理を通らずに鳴り終わった＝最後まで流れた
                    Record("ナレーション：最後まで流れた");
                    _narrationBaseVolume = 0f;
                }
                return;
            }

            if (TitleNarrationRule.ShouldStopNow(_muted))
            {
                _narration.Stop();
                _narrationBaseVolume = 0f;
                Record("ナレーション：音を消したので止めた");
                return;
            }

            bool visible = titleView != null && titleView.IsVisible;
            bool closing = titleView != null && titleView.IsClosing;
            if (!_narrationFading && TitleNarrationRule.ShouldFadeOut(visible, closing))
            {
                _narrationFading = true;
                _narrationFadeStart = Time.unscaledTime;
                Record("ナレーション：タイトルを閉じたので小さくし始めた");
            }
            if (_narrationFading)
            {
                float k = 1f - (Time.unscaledTime - _narrationFadeStart) / Mathf.Max(table.narrationFadeSeconds, 0.01f);
                if (k <= 0f)
                {
                    _narration.Stop();
                    _narrationBaseVolume = 0f;
                    _narrationFading = false;
                    Record("ナレーション：止めた");
                    return;
                }
                _narration.volume = _narrationBaseVolume * k;
            }
        }

        private void Update()
        {
            UpdatePinVoices();
            UpdateNarration();
            UpdateBgm();
        }

        /// <summary>ピンの音：長さの上限が来たら、なめらかに小さくして止める。</summary>
        private void UpdatePinVoices()
        {
            float fade = table != null ? Mathf.Max(table.pinFadeSeconds, 0.01f) : 0.25f;
            foreach (PinVoice voice in _pinVoices)
            {
                if (!voice.source.isPlaying || Time.time < voice.stopTime)
                {
                    continue;
                }
                float k = 1f - (Time.time - voice.stopTime) / fade;
                if (k <= 0f)
                {
                    voice.source.Stop();
                    continue;
                }
                voice.source.volume = voice.baseVolume * k;
            }
        }

        /// <summary>
        /// BGM の口：タイトル・レーン・結果画面に合った曲へ、なめらかにつなぐ。
        /// 曲が入っていなければ何も流れない。真空のレーンでは流さない。
        /// </summary>
        private void UpdateBgm()
        {
            if (table == null || _bgm == null)
            {
                return;
            }

            ResolveBgm(out AudioClip clip, out float volume);
            if (!_unlocked)
            {
                clip = null;
            }

            if (clip != _bgmClip)
            {
                _bgmClip = clip;
                _bgmCurrent = 1 - _bgmCurrent;
                AudioSource next = _bgm[_bgmCurrent];
                next.Stop();
                next.clip = clip;
                next.volume = 0f;
                if (clip != null)
                {
                    next.Play();
                    Record("BGM：" + clip.name);
                }
                else
                {
                    Record("BGM：なし");
                }
            }
            _bgmTargetVolume = volume * table.bgmMaster;

            // ナレーションの間は BGM を小さくし、終わったらゆっくり戻す
            float duckTarget = IsNarrationPlaying && !_narrationFading ? SoundLevel.DbToLinear(table.bgmDuckDb) : 1f;
            float duckSeconds = duckTarget < _bgmDuck ? 0.3f : Mathf.Max(table.bgmDuckReleaseSeconds, 0.01f);
            _bgmDuck = Mathf.MoveTowards(_bgmDuck, duckTarget, Time.unscaledDeltaTime / duckSeconds);

            float step = Time.unscaledDeltaTime / Mathf.Max(table.bgmCrossfadeSeconds, 0.01f);
            for (int i = 0; i < _bgm.Length; i++)
            {
                AudioSource source = _bgm[i];
                float target = i == _bgmCurrent && _bgmClip != null ? _bgmTargetVolume * _bgmDuck : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, step * Mathf.Max(_bgmTargetVolume, 0.01f));
                if (i != _bgmCurrent && source.isPlaying && source.volume <= 0f)
                {
                    source.Stop();
                }
            }
        }

        /// <summary>今流すべき曲を決める。</summary>
        private void ResolveBgm(out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 0f;

            // CLICK TO TUNE IN の画面の間は、曲を流さない
            if (tuneInView != null && tuneInView.IsVisible)
            {
                return;
            }
            if (titleView != null && titleView.IsVisible)
            {
                clip = table.titleBgm;
                volume = table.titleBgmVolume;
                return;
            }
            if (gameManager == null)
            {
                return;
            }
            if (gameManager.IsFinished)
            {
                clip = table.resultBgm;
                volume = table.resultBgmVolume;
                return;
            }

            LaneData lane = gameManager.CurrentLane;
            if (lane == null || lane.Vacuum)
            {
                return;
            }
            LaneBgm entry = table.FindLaneBgm(lane);
            if (entry != null)
            {
                clip = entry.clip;
                volume = entry.volume;
            }
        }

        private void ApplyTableToGates()
        {
            if (table == null)
            {
                return;
            }
            _pinGate.MinSpeed = table.pinMinSpeed;
            _pinGate.StrongSpeed = table.pinStrongSpeed;
            _pinGate.Cooldown = table.pinCooldownSeconds;
            _pinGate.MaxVoices = Mathf.Min(table.pinMaxVoices, _pinVoices.Count);
        }

        private AudioSource CreateSource(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private AudioSource FindFreeSource()
        {
            foreach (AudioSource source in _oneShots)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }
            // 全部鳴っていたら、いちばん長く鳴っているものを止めて使う
            AudioSource oldest = _oneShots[0];
            foreach (AudioSource source in _oneShots)
            {
                if (source.time > oldest.time)
                {
                    oldest = source;
                }
            }
            oldest.Stop();
            return oldest;
        }

        private PinVoice FindFreePinVoice()
        {
            foreach (PinVoice voice in _pinVoices)
            {
                if (!voice.source.isPlaying)
                {
                    return voice;
                }
            }
            return null;
        }

        private static AudioClip FirstClip(SoundEntry entry)
        {
            foreach (AudioClip clip in entry.clips)
            {
                if (clip != null)
                {
                    return clip;
                }
            }
            return null;
        }

        /// <summary>入っている音から1つ選ぶ。</summary>
        public static AudioClip PickClip(SoundEntry entry, int index = -1)
        {
            if (entry == null || entry.clips == null || entry.clips.Length == 0)
            {
                return null;
            }
            if (index < 0)
            {
                index = Random.Range(0, entry.clips.Length);
            }
            AudioClip clip = entry.clips[Mathf.Clamp(index, 0, entry.clips.Length - 1)];
            return clip != null ? clip : FirstClip(entry);
        }

        private static float RandomPitch(SoundEntry entry)
        {
            return entry.pitchRandom > 0f ? 1f + Random.Range(-entry.pitchRandom, entry.pitchRandom) : 1f;
        }
    }
}
