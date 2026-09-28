using CrazyBowling.Data;
using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>
    /// クレジットのあいさつの声を流すかの決まり（段階6）。MonoBehaviour に依らない（EditMode テストあり）。
    /// </summary>
    public static class CreditsSpeechRule
    {
        /// <summary>
        /// あいさつを流し始めてよいか：声のファイルが入っている・音を鳴らせる状態（Web で一度クリックした）・
        /// 全体の音を消していない・DJ を消していない。
        /// </summary>
        public static bool CanStart(bool hasClip, bool unlocked, bool muted, bool djMuted)
        {
            return hasClip && unlocked && !muted && !djMuted;
        }

        /// <summary>流している途中で止めるべきか：全体の音か DJ を消した。</summary>
        public static bool ShouldStop(bool muted, bool djMuted)
        {
            return muted || djMuted;
        }
    }

    /// <summary>
    /// クレジットのあいさつの声を流す係（段階6）。シーンの「Sound」に置く。
    ///   ・CREDITS の画面を開いたら、少し間をおいて1回流す（声は音の表の <see cref="SoundTable.creditsSpeech"/>。空なら流さない）
    ///   ・画面を開いている間は、DJ の番組を一時停止する（声が無くても止める）。閉じたら続きから再開する
    ///   ・画面を閉じたら、あいさつは短く小さくして止める。全体の音・DJ を消したときも同じ
    ///   ・タイトルのナレーションが流れていたら、小さくして止める（2つの声が重ならないように）
    /// 判定・得点・物理には関わらない。
    /// </summary>
    public class CreditsSpeechPlayer : MonoBehaviour
    {
        [Tooltip("クレジットの画面。空なら同じシーンから探す。")]
        [SerializeField] private UI.CreditsView creditsView;

        private AudioSource _voice;
        private bool _suspendingDj;
        private float _startAt = -1f;
        private bool _fading;
        private float _fadeStart;
        private float _baseVolume;

        /// <summary>あいさつが流れているか（小さくして止めている途中も含む）。</summary>
        public bool IsPlaying => _voice != null && _voice.isPlaying;

        /// <summary>あいさつをふつうに流しているか（小さくして止めている途中は含まない）。クレジットの自動送りはこの間だけ動く。</summary>
        public bool IsSpeaking => IsPlaying && !_fading;

        /// <summary>あいさつの今の位置（秒）。流していなければ 0。</summary>
        public float PlaybackTime => IsPlaying ? _voice.time : 0f;

        /// <summary>DJ の番組を止めてもらっているか（確かめるとき用）。</summary>
        public bool IsSuspendingDj => _suspendingDj;

        private void Awake()
        {
            var child = new GameObject("クレジットのあいさつ");
            child.transform.SetParent(transform, false);
            _voice = child.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.loop = false;
            _voice.spatialBlend = 0f;
            if (creditsView == null)
            {
                creditsView = FindFirstObjectByType<UI.CreditsView>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            if (creditsView != null)
            {
                creditsView.Opened += OnOpened;
                creditsView.Closed += OnClosed;
            }
        }

        private void OnDisable()
        {
            if (creditsView != null)
            {
                creditsView.Opened -= OnOpened;
                creditsView.Closed -= OnClosed;
            }
            ResumeDj();
        }

        private void OnOpened()
        {
            SoundPlayer player = SoundPlayer.Instance;
            // DJ の番組は、声があってもなくても止める（閉じたら続きから）
            if (!_suspendingDj && DjRadio.Instance != null)
            {
                DjRadio.Instance.Suspend();
                _suspendingDj = true;
            }
            if (player != null)
            {
                player.FadeOutNarration();
            }

            SoundTable table = player != null ? player.Table : null;
            bool djMuted = DjRadio.Instance != null && DjRadio.Instance.DjMuted;
            if (table == null || !CreditsSpeechRule.CanStart(table.creditsSpeech != null, player.IsUnlocked, player.Muted, djMuted))
            {
                player?.Record(table != null && table.creditsSpeech == null
                    ? "クレジットのあいさつ：声が入っていないので流さない"
                    : "クレジットのあいさつ：音か DJ を消しているので流さない");
                _startAt = -1f;
                return;
            }
            _startAt = Time.unscaledTime + Mathf.Max(0f, table.creditsSpeechDelaySeconds);
        }

        private void OnClosed()
        {
            _startAt = -1f;
            BeginFade("画面を閉じた");
            ResumeDj();
        }

        private void ResumeDj()
        {
            if (_suspendingDj)
            {
                _suspendingDj = false;
                if (DjRadio.Instance != null)
                {
                    DjRadio.Instance.Resume();
                }
            }
        }

        private void BeginFade(string reason)
        {
            if (IsPlaying && !_fading)
            {
                _fading = true;
                _fadeStart = Time.unscaledTime;
                SoundPlayer.Instance?.Record("クレジットのあいさつ：小さくし始めた（" + reason + "）");
            }
        }

        private void Update()
        {
            SoundPlayer player = SoundPlayer.Instance;
            SoundTable table = player != null ? player.Table : null;
            if (table == null)
            {
                return;
            }
            bool djMuted = DjRadio.Instance != null && DjRadio.Instance.DjMuted;

            // 流し始める（開いてから少しの間をおいて）
            if (_startAt >= 0f && Time.unscaledTime >= _startAt)
            {
                _startAt = -1f;
                bool open = creditsView != null && creditsView.IsOpen;
                if (open && CreditsSpeechRule.CanStart(table.creditsSpeech != null, player.IsUnlocked, player.Muted, djMuted))
                {
                    _voice.Stop();
                    _voice.clip = table.creditsSpeech;
                    // ★必ず頭から流す（Streaming の音は、前に止めた位置・最後まで流した位置を覚えていて、そこから始まることがある）
                    _voice.time = 0f;
                    _baseVolume = table.creditsSpeechVolume;
                    _voice.volume = _baseVolume;
                    _fading = false;
                    _voice.Play();
                    player.Record("クレジットのあいさつ：始めた（" + table.creditsSpeech.name + "）");
                }
            }

            if (!IsPlaying)
            {
                _fading = false;
                return;
            }
            if (CreditsSpeechRule.ShouldStop(player.Muted, djMuted))
            {
                BeginFade(player.Muted ? "音を消した" : "DJ を消した");
            }
            if (_fading)
            {
                float k = 1f - (Time.unscaledTime - _fadeStart) / Mathf.Max(table.creditsSpeechFadeSeconds, 0.01f);
                if (k <= 0f)
                {
                    _voice.Stop();
                    _fading = false;
                    player.Record("クレジットのあいさつ：止めた");
                    return;
                }
                _voice.volume = _baseVolume * k;
            }
        }
    }
}
