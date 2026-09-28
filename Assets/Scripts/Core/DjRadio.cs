using CrazyBowling.Data;
using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>
    /// DJ のラジオ番組を流す係（段階6）。シーンの「Sound」に置く。
    /// CLICK TO START でタイトルのナレーションが止まったら始め、ゲーム中ずっと流す：コーナー → ID → コーナー → ID …（並びは <see cref="DjProgram"/>）。
    /// レーンが変わっても、結果画面でも、PLAY AGAIN でも止めない（頭にも戻らない）。
    /// 声を鳴らす口は1つだけなので、DJ の声が2本同時に鳴ることはない。
    /// 効果音・歓声が鳴っている間は声を少し下げ、鳴り終わったらなめらかに戻す。DJ がしゃべっている間、BGM は鳴らし係が小さくする。
    /// DJ を消す（<see cref="DjMuted"/>）か、全体の音を消すと、しゃべっている声を小さくして止め、番組も止める。戻すと次の番組から続ける。
    /// CREDITS の画面を開いている間は一時停止する（<see cref="Suspend"/>。しゃべっている声は小さくして止め、閉じたら同じ所から続ける）。
    /// 数値はすべて音の表（<see cref="SoundTable"/> の「DJ のラジオ番組」）で直す。判定・得点・物理には関わらない。
    /// </summary>
    public class DjRadio : MonoBehaviour
    {
        /// <summary>DJ を消したかを残す鍵。次に起動したときも同じにする。</summary>
        private const string MutedKey = "CrazyBowling.DjMuted";

        /// <summary>シーンに置いた番組の係。無ければ null。</summary>
        public static DjRadio Instance { get; private set; }

        [Tooltip("タイトル画面。閉じたら番組を始める。空なら探す。")]
        [SerializeField] private UI.TitleView titleView;

        [Tooltip("タイトルの前の CLICK TO TUNE IN の画面。出ている間は始めない。空なら探す。")]
        [SerializeField] private UI.TuneInView tuneInView;

        private AudioSource _voice;
        private DjProgram _program;
        private bool _started;
        private float _nextAt = -1f;
        private bool _hasCurrent;
        private float _duck = 1f;
        private float _fade = 1f;
        private bool _stopping;
        private int _holds;
        private bool _muted;
        private int _suspends;
        private bool _paused;
        private bool _justResumed;

        /// <summary>声を鳴らす口（確かめるとき用）。</summary>
        public AudioSource Voice => _voice;

        /// <summary>番組の本編が始まったか。</summary>
        public bool Started => _started;

        /// <summary>DJ がしゃべっているか（声が鳴っているか）。</summary>
        public bool IsTalking => _voice != null && _voice.isPlaying;

        /// <summary>最後にしゃべっていた時刻（unscaledTime）。まだなら負。BGM を小さいまま保つのに使う。</summary>
        public float LastTalkTime { get; private set; } = -1f;

        /// <summary>今しゃべっている（最後にしゃべった）もの。</summary>
        public DjItem Current { get; private set; }

        /// <summary>始めた番組の数（確かめるとき用）。</summary>
        public int ItemsStarted { get; private set; }

        /// <summary>効果音のために下げている倍率（1 で下げていない）。</summary>
        public float Duck => _duck;

        /// <summary>声の音量の基準（音の表の DJ の音量）。</summary>
        public float BaseVolume => SoundPlayer.Instance != null && SoundPlayer.Instance.Table != null ? SoundPlayer.Instance.Table.djVolume : 0f;

        /// <summary>外から番組を待ってもらっている数（8本目の交信の間）。</summary>
        public int Holds => _holds;

        /// <summary>DJ だけを消しているか。選んだ状態は次に起動したときも残る。</summary>
        public bool DjMuted
        {
            get => _muted;
            set
            {
                _muted = value;
                PlayerPrefs.SetInt(MutedKey, _muted ? 1 : 0);
                PlayerPrefs.Save();
                SoundPlayer.Instance?.Record(_muted ? "DJ：消した" : "DJ：戻した");
            }
        }

        /// <summary>
        /// ほかの声（8本目の交信）を今始めてよいか：DJ がしゃべっていない・止めている途中でもない。
        /// 始めるときは <see cref="Hold"/> を呼び、終わったら <see cref="Release"/> を呼ぶ（その間、DJ は次の番組を始めない）。
        /// </summary>
        public bool IsQuietForOthers => !IsTalking;

        /// <summary>一時停止してもらっているか（CREDITS の画面を開いている間）。</summary>
        public bool IsSuspended => _suspends > 0;

        /// <summary>声を一時停止の位置で止めているか（確かめるとき用）。</summary>
        public bool IsPaused => _paused;

        /// <summary>
        /// 番組を一時停止する（CREDITS の画面を開いた）。しゃべっていたら小さくしてから、その位置で止める。
        /// 止めている間は次の番組も始めない。<see cref="Resume"/> で続きから。
        /// </summary>
        public void Suspend()
        {
            _suspends++;
            if (_suspends == 1)
            {
                SoundPlayer.Instance?.Record("DJ：一時停止を頼まれた");
            }
        }

        /// <summary>一時停止をやめる（CREDITS の画面を閉じた）。止めた位置から続ける。</summary>
        public void Resume()
        {
            if (_suspends <= 0)
            {
                return;
            }
            _suspends--;
            if (_suspends == 0)
            {
                _justResumed = true;
                SoundPlayer.Instance?.Record("DJ：一時停止をやめた");
            }
        }

        /// <summary>番組を待ってもらう（8本目の交信を始めた）。</summary>
        public void Hold()
        {
            _holds++;
        }

        /// <summary>待ってもらうのをやめる（交信が終わった）。そこから決めた間をおいて次の番組を始める。</summary>
        public void Release()
        {
            _holds = Mathf.Max(0, _holds - 1);
            if (_holds == 0 && _started && _nextAt >= 0f)
            {
                _nextAt = Mathf.Max(_nextAt, Time.unscaledTime + GapSeconds());
            }
        }

        private void Awake()
        {
            _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            var child = new GameObject("DJ");
            child.transform.SetParent(transform, false);
            _voice = child.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.loop = false;
            _voice.spatialBlend = 0f;
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
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || player.Table == null)
            {
                return;
            }
            SoundTable table = player.Table;
            float now = Time.unscaledTime;
            bool on = player.IsUnlocked && !player.Muted && !_muted;

            if (IsTalking)
            {
                LastTalkTime = now;
            }

            // 始める：CLICK TO TUNE IN もタイトルも閉じて、ナレーションが止まったら
            if (!_started)
            {
                bool titleOpen = (titleView != null && titleView.IsVisible) || (tuneInView != null && tuneInView.IsVisible);
                if (on && !titleOpen && _suspends == 0 && !player.IsNarrationPlaying && HasClips(table))
                {
                    _started = true;
                    _program = new DjProgram(Count(table.djCorners), Count(table.djIds), System.Environment.TickCount, true);
                    _nextAt = now + Mathf.Max(0f, table.djFirstDelaySeconds);
                    player.Record("DJ：番組を始めた");
                }
                return;
            }

            // 効果音・歓声が鳴っている間は下げる（下げるのは速く、戻すのはゆっくり）
            float duckTarget = player.IsSeActive ? SoundLevel.DbToLinear(table.djDuckDb) : 1f;
            _duck = DuckEnvelope.Approach(_duck, duckTarget, table.djDuckAttackSeconds, table.djDuckReleaseSeconds, Time.unscaledDeltaTime);

            // DJ を消した・全体の音を消した：しゃべっていたら小さくして止め、番組も止める
            if (!on)
            {
                if (_paused)
                {
                    // 一時停止していた声は、続きを流さずに捨てる（戻したら次の番組から）
                    _voice.Stop();
                    _paused = false;
                    _fade = 1f;
                }
                if (IsTalking)
                {
                    _stopping = true;
                    _fade = Mathf.MoveTowards(_fade, 0f, Time.unscaledDeltaTime / Mathf.Max(table.djStopFadeSeconds, 0.01f));
                    _voice.volume = table.djVolume * _duck * _fade;
                    if (_fade <= 0f)
                    {
                        _voice.Stop();
                        player.Record("DJ：止めた（" + (_muted ? "DJ を消した" : "音を消した") + "）");
                    }
                }
                _hasCurrent = false;
                _nextAt = -1f;
                return;
            }
            if (_stopping && !IsTalking)
            {
                _stopping = false;
                _fade = 1f;
            }

            // 一時停止（CREDITS の画面）：しゃべっていたら小さくして、その位置で止める。次の番組も始めない
            if (_suspends > 0)
            {
                if (IsTalking)
                {
                    _fade = Mathf.MoveTowards(_fade, 0f, Time.unscaledDeltaTime / Mathf.Max(table.djStopFadeSeconds, 0.01f));
                    _voice.volume = table.djVolume * _duck * _fade;
                    if (_fade <= 0f)
                    {
                        _voice.Pause();
                        _paused = true;
                        player.Record("DJ：一時停止した（" + Label(Current) + "）");
                    }
                }
                return;
            }
            if (_paused)
            {
                // 続きから：止めた位置から流し、ゆっくり元の大きさに戻す
                _paused = false;
                _voice.UnPause();
                player.Record("DJ：続きから再開した（" + Label(Current) + "）");
            }
            else if (_justResumed && !IsTalking && _nextAt >= 0f)
            {
                // すき間の途中で止めていたら、閉じてから決めた間をおいて次の番組へ
                _nextAt = Mathf.Max(_nextAt, now + GapSeconds());
            }
            _justResumed = false;
            if (!_stopping && IsTalking && _fade < 1f)
            {
                _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / Mathf.Max(table.djStopFadeSeconds, 0.01f));
            }
            if (_nextAt < 0f && !IsTalking)
            {
                // 戻した：決めた間をおいて、次の番組から続ける
                _nextAt = now + GapSeconds();
            }

            _voice.volume = table.djVolume * _duck * _fade;

            if (IsTalking)
            {
                return;
            }
            if (_hasCurrent)
            {
                _hasCurrent = false;
                player.Record("DJ：" + Label(Current) + " 終わった");
                _nextAt = now + GapSeconds();
            }
            if (_holds > 0 || now < _nextAt)
            {
                return;
            }
            PlayNext(player, table);
        }

        /// <summary>次の番組を流す。声が入っていない欄は飛ばす。</summary>
        private void PlayNext(SoundPlayer player, SoundTable table)
        {
            for (int tries = 0; tries < 30; tries++)
            {
                DjItem item = _program.Next();
                AudioClip[] clips = item.kind == DjItemKind.Corner ? table.djCorners : table.djIds;
                AudioClip clip = clips != null && item.index >= 0 && item.index < clips.Length ? clips[item.index] : null;
                if (clip == null)
                {
                    continue;
                }
                Current = item;
                _voice.clip = clip;
                _voice.pitch = 1f;
                _fade = 1f;
                _voice.volume = table.djVolume * _duck;
                _voice.Play();
                _hasCurrent = true;
                ItemsStarted++;
                LastTalkTime = Time.unscaledTime;
                player.Record("DJ：" + Label(item) + " 始めた（" + clip.name + "）");
                return;
            }
        }

        private float GapSeconds()
        {
            SoundPlayer player = SoundPlayer.Instance;
            Vector2 range = player != null && player.Table != null ? player.Table.djGapSeconds : new Vector2(1f, 2f);
            return SoundSchedule.NextDelay(range, Random.value);
        }

        /// <summary>記録に書く名前（例：「コーナー3（1周目）」「ID2」）。</summary>
        public static string Label(DjItem item)
        {
            return item.kind == DjItemKind.Corner ? $"コーナー{item.index + 1}（{item.cycle}周目）" : $"ID{item.index + 1}";
        }

        private static bool HasClips(SoundTable table)
        {
            return Count(table.djCorners) + Count(table.djIds) > 0;
        }

        private static int Count(AudioClip[] clips)
        {
            return clips != null ? clips.Length : 0;
        }
    }
}
