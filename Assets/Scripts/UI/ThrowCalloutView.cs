using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 1投を判定した瞬間の大文字の演出（段階6）。ThrowSequencer.ThrowJudged を聞くだけ。
    ///   STRIKE!   … 1文字ずつ虹色が流れる大文字が回りながら飛び込み、光の筋が回り、紙吹雪と星が散る
    ///   SPARE!    … 水色の大文字が飛び込み、光の輪が広がる
    ///   GUTTER... … 1本も倒れなかった投。灰色の文字が落ちてきて弾み、しょんぼり傾いて沈む（情けない演出）
    ///               ガターを見分ける仕組みは無いので「その投で0本」で出す
    /// どれも1.5秒ほどで消える。画面の中ほどより上に出し、ピンを長く覆わない。
    ///
    /// 見た目だけ。判定と次の投球は待たせない。
    /// ★画面全体を明滅させない。光は回る・広がる・散るで出す。
    /// </summary>
    public class ThrowCalloutView : MonoBehaviour
    {
        private enum Kind
        {
            None,
            Strike,
            Spare,
            Gutter,
        }

        [Header("参照")]
        [Tooltip("判定を知らせてくる相手。空なら同じシーンから探す。")]
        [SerializeField] private ThrowSequencer sequencer;

        [Tooltip("差し色を読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("演出を出す入れ物（画面の中ほどより上）。")]
        [SerializeField] private RectTransform root;

        [Tooltip("大文字。")]
        [SerializeField] private TMP_Text label;

        [Header("時間（秒）")]
        [Tooltip("ストライクの演出の長さ。")]
        [SerializeField] private float strikeSeconds = 1.6f;

        [Tooltip("スペアの演出の長さ。")]
        [SerializeField] private float spareSeconds = 1.4f;

        [Tooltip("ガターの演出の長さ。")]
        [SerializeField] private float gutterSeconds = 1.5f;

        [Header("量")]
        [Tooltip("光の筋の本数。")]
        [SerializeField] private int rayCount = 14;

        [Tooltip("紙吹雪の枚数。")]
        [SerializeField] private int confettiCount = 70;

        [Tooltip("星の数。")]
        [SerializeField] private int starCount = 10;

        [Tooltip("紙吹雪にかかる重力（ピクセル/秒²）。")]
        [SerializeField] private float confettiGravity = 1400f;

        [Header("色")]
        [Tooltip("スペアの色。")]
        [SerializeField] private Color spareColor = new Color(0.25f, 0.9f, 1f);

        [Tooltip("ガターの色。")]
        [SerializeField] private Color gutterColor = new Color(0.55f, 0.6f, 0.72f);

        private struct Piece
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float spin;
            public float hue;
        }

        private RectTransform _rays;
        [System.NonSerialized] private Image[] _rayImages;
        private Image _ring;
        private Image _glow;
        [System.NonSerialized] private Piece[] _confetti;
        [System.NonSerialized] private Piece[] _stars;
        private NeonTextRainbow _rainbow;
        private CanvasGroup _group;

        private Kind _kind;
        private float _startTime = -100f;

        private void Awake()
        {
            if (sequencer == null)
            {
                sequencer = FindFirstObjectByType<ThrowSequencer>();
            }
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }

            if (root == null || skin == null)
            {
                return;
            }

            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            Build();
            if (label != null)
            {
                _rainbow = label.GetComponent<NeonTextRainbow>();
                label.transform.SetAsLastSibling();
            }
        }

        private void OnEnable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowJudged += OnThrowJudged;
            }
        }

        private void OnDisable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowJudged -= OnThrowJudged;
            }
        }

        /// <summary>光の筋・光の輪・紙吹雪・星を作っておく（見えない状態で）。</summary>
        private void Build()
        {
            RectTransform glowRect = NeonUI.CreateRect(root, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 520f));
            _glow = NeonUI.CreateImage(glowRect, skin.Glow, Color.clear, false);

            _rays = NeonUI.CreateRect(root, "Rays", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _rayImages = new Image[Mathf.Max(0, rayCount)];
            for (int i = 0; i < _rayImages.Length; i++)
            {
                RectTransform ray = NeonUI.CreateRect(_rays, $"Ray{i:00}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 620f));
                ray.pivot = new Vector2(0.5f, 1f);   // 明るい端（画像の上）を中心に置き、外へ伸ばす
                ray.localEulerAngles = new Vector3(0f, 0f, 180f + i * 360f / _rayImages.Length);
                _rayImages[i] = NeonUI.CreateImage(ray, skin.Ray, Color.clear, false);
            }

            RectTransform ringRect = NeonUI.CreateRect(root, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 400f));
            _ring = NeonUI.CreateImage(ringRect, skin.Ring, Color.clear, false);

            _confetti = BuildPieces("Confetti", confettiCount, skin.Confetti, new Vector2(18f, 27f));
            _stars = BuildPieces("Star", starCount, skin.Star, new Vector2(90f, 90f));
        }

        private Piece[] BuildPieces(string name, int count, Sprite sprite, Vector2 size)
        {
            var pieces = new Piece[Mathf.Max(0, count)];
            for (int i = 0; i < pieces.Length; i++)
            {
                RectTransform rect = NeonUI.CreateRect(root, $"{name}{i:00}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
                pieces[i] = new Piece { rect = rect, image = NeonUI.CreateImage(rect, sprite, Color.clear, false) };
            }
            return pieces;
        }

        /// <summary>判定の知らせ。ストライク・スペア・0本のときだけ演出を始める。</summary>
        private void OnThrowJudged(ThrowJudgement judgement)
        {
            Kind kind = judgement.isStrike ? Kind.Strike
                : judgement.isSpare ? Kind.Spare
                : judgement.fallen == 0 ? Kind.Gutter
                : Kind.None;
            if (kind == Kind.None || label == null || skin == null)
            {
                return;
            }

            _kind = kind;
            _startTime = Time.unscaledTime;
            label.text = kind == Kind.Strike ? UIText.CalloutStrike
                : kind == Kind.Spare ? UIText.CalloutSpare
                : UIText.CalloutGutter;
            if (_rainbow != null)
            {
                _rainbow.enabled = kind == Kind.Strike;
            }

            // 紙吹雪と星の飛び方をそのつど決める
            for (int i = 0; i < _confetti.Length; i++)
            {
                float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad;
                float speed = Random.Range(700f, 1500f);
                _confetti[i].velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);
                _confetti[i].spin = Random.Range(-720f, 720f);
                _confetti[i].hue = Random.value;
                _confetti[i].rect.anchoredPosition = new Vector2(Random.Range(-120f, 120f), Random.Range(-40f, 40f));
            }
            for (int i = 0; i < _stars.Length; i++)
            {
                float angle = (i + Random.value * 0.6f) / _stars.Length * Mathf.PI * 2f;
                float distance = Random.Range(260f, 560f);
                _stars[i].velocity = new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance * 0.45f);
                _stars[i].hue = Random.value;
            }
        }

        private void LateUpdate()
        {
            if (_group == null)
            {
                return;
            }

            float t = Time.unscaledTime - _startTime;
            float length = _kind == Kind.Strike ? strikeSeconds : _kind == Kind.Spare ? spareSeconds : gutterSeconds;
            if (_kind == Kind.None || t > length)
            {
                _group.alpha = 0f;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            float fadeOut = t > length - 0.3f ? (length - t) / 0.3f : 1f;
            _group.alpha = Mathf.Clamp01(fadeOut);

            switch (_kind)
            {
                case Kind.Strike:
                    AnimateStrike(t, dt);
                    break;
                case Kind.Spare:
                    AnimateSpare(t);
                    break;
                default:
                    AnimateGutter(t);
                    break;
            }
        }

        private void AnimateStrike(float t, float dt)
        {
            Color accent = gameManager != null ? skin.GetAccent(gameManager.LaneNumber) : skin.DefaultAccent;

            // 大文字：大きく回りながら飛び込み、行きすぎて戻る
            float enter = NeonUI.EaseOutBack(t / 0.38f);
            RectTransform rect = label.rectTransform;
            float scale = Mathf.LerpUnclamped(3.2f, 1f, enter);
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(28f, 0f, enter));
            rect.anchoredPosition = Vector2.zero;
            label.color = Color.white;
            NeonUI.SetNeonColor(label, Color.Lerp(skin.Gold, accent, NeonUI.Breath(0.9f)), 0.85f);

            // 光の筋：回りながら、色を筋ごとにずらす
            float rayAlpha = Mathf.Clamp01(t / 0.2f) * 0.8f;
            _rays.localEulerAngles = new Vector3(0f, 0f, t * 70f);
            float rayScale = Mathf.Lerp(0.3f, 1.2f, NeonUI.EaseOutCubic(t / 0.5f));
            _rays.localScale = new Vector3(rayScale, rayScale, 1f);
            for (int i = 0; i < _rayImages.Length; i++)
            {
                _rayImages[i].color = NeonUI.WithAlpha(NeonUI.Hue(i / (float)_rayImages.Length + t * 0.3f, 0.7f), rayAlpha);
            }

            _glow.color = NeonUI.WithAlpha(skin.Gold, 0.45f * Mathf.Clamp01(t / 0.2f));
            _ring.color = Color.clear;

            // 紙吹雪：上へ弾けて、落ちながら回る
            for (int i = 0; i < _confetti.Length; i++)
            {
                Piece p = _confetti[i];
                p.velocity.y -= confettiGravity * dt;
                p.velocity *= 1f - 0.8f * dt;
                p.rect.anchoredPosition += p.velocity * dt;
                p.rect.localEulerAngles = new Vector3(0f, 0f, p.spin * t);
                p.image.color = NeonUI.Hue(p.hue + t * 0.2f, 0.75f);
                _confetti[i] = p;
            }

            AnimateStars(t, 1f);
        }

        private void AnimateSpare(float t)
        {
            float enter = NeonUI.EaseOutBack(t / 0.35f);
            RectTransform rect = label.rectTransform;
            float scale = Mathf.LerpUnclamped(2.2f, 1f, enter);
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(-10f, 0f, enter));
            rect.anchoredPosition = Vector2.zero;
            label.color = Color.white;
            NeonUI.SetNeonColor(label, spareColor, 0.8f);

            // 光の輪が広がって消える
            float ring = NeonUI.EaseOutCubic(t / 0.8f);
            _ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 4.2f, ring);
            _ring.color = NeonUI.WithAlpha(spareColor, 0.9f * (1f - ring));
            _glow.color = NeonUI.WithAlpha(spareColor, 0.35f * Mathf.Clamp01(t / 0.2f));

            HideRays();
            HideConfetti();
            AnimateStars(t, 0.7f);
        }

        private void AnimateGutter(float t)
        {
            // 上から落ちてきて弾み、そのあと しょんぼり傾いて少し沈む
            float fall = NeonUI.EaseOutBounce(t / 0.6f);
            float droop = NeonUI.EaseOutCubic((t - 0.65f) / 0.6f);
            RectTransform rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(420f, 0f, fall) - 45f * droop);
            rect.localScale = Vector3.one * 0.8f;
            rect.localEulerAngles = new Vector3(0f, 0f, -11f * droop);
            label.color = gutterColor;
            NeonUI.SetNeonColor(label, new Color(0.2f, 0.25f, 0.4f), 0.15f);

            _glow.color = Color.clear;
            _ring.color = Color.clear;
            HideRays();
            HideConfetti();
            AnimateStars(t, 0f);
        }

        /// <summary>星：中心から外へ飛び、光ってから消える。</summary>
        private void AnimateStars(float t, float amount)
        {
            float move = NeonUI.EaseOutCubic(t / 0.6f);
            for (int i = 0; i < _stars.Length; i++)
            {
                Piece s = _stars[i];
                s.rect.anchoredPosition = s.velocity * move;
                float size = Mathf.Sin(Mathf.Clamp01(t / 0.9f) * Mathf.PI);
                s.rect.localScale = Vector3.one * (0.4f + size);
                s.rect.localEulerAngles = new Vector3(0f, 0f, t * 90f);
                s.image.color = NeonUI.WithAlpha(NeonUI.Hue(s.hue, 0.4f), amount * size);
            }
        }

        private void HideRays()
        {
            for (int i = 0; i < _rayImages.Length; i++)
            {
                _rayImages[i].color = Color.clear;
            }
        }

        private void HideConfetti()
        {
            for (int i = 0; i < _confetti.Length; i++)
            {
                _confetti[i].image.color = Color.clear;
            }
        }
    }
}
