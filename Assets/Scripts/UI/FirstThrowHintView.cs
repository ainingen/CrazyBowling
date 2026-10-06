using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Ball;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 初めて遊ぶ人にだけ、1本目の最初の1投の構えのときに出す短いヒント（段階6）。
    ///   ・文：「押したまま、手前へ引いて、離してください」「横にずらすと、反対へ飛びます」
    ///   ・動き：押す所の輪から、指の丸が手前（画面の下）へゆっくり滑り、矢印の先で消える。点滅はさせない
    /// ★投げる操作を邪魔しない：画像も文字も当たり判定を持たない（押しても球の操作がそのまま始まる）。押し始めたら消える。
    /// ★一度投げたら、次からは出さない（<see cref="FirstThrowHintStore"/>。記録と同じく端末に残す）。
    /// 出すかどうかの決まりは <see cref="FirstThrowHintRules"/>。
    /// </summary>
    public class FirstThrowHintView : MonoBehaviour
    {
        [Header("参照（空なら同じシーンから探す）")]
        [SerializeField] private BallController ball;
        [SerializeField] private ThrowSequencer sequencer;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private CameraPreview preview;
        [SerializeField] private TuneInView tuneIn;
        [SerializeField] private TitleView title;
        [SerializeField] private RecordsView records;
        [SerializeField] private HowToPlayView howToPlay;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("ヒントを置く入れ物（画面全体）。")]
        [SerializeField] private RectTransform root;

        [Header("出し方")]
        [Tooltip("出す条件がそろってから、出し始めるまでの待ち（秒）。レーンの大見出しと重ならないように。")]
        [SerializeField] private float appearDelay = 0.8f;

        [Tooltip("出るときにゆっくり濃くなる時間（秒）。")]
        [SerializeField] private float fadeInSeconds = 0.6f;

        [Tooltip("押し始めたときに消える時間（秒）。短くして、引く操作の邪魔をしない。")]
        [SerializeField] private float fadeOutSeconds = 0.12f;

        [Header("置き場所（構えたボールとピンの間。操作の近く）")]
        [Tooltip("ヒントのまとまりの中心（画面の幅・高さに対する割合）。ボール（下から約29%）とピン（約62%）の間に置き、ボールを隠さない。")]
        [SerializeField] private Vector2 anchor = new Vector2(0.5f, 0.47f);

        [Tooltip("ヒントのまとまりの大きさ（ピクセル）。左に手前へ引く動き、右に文。")]
        [SerializeField] private Vector2 boxSize = new Vector2(900f, 190f);

        [Tooltip("文字の大きさ。")]
        [SerializeField] private float textSize = 36f;

        [Tooltip("2行目（横にずらすと…）の文字の大きさ。")]
        [SerializeField] private float subTextSize = 30f;

        [Tooltip("英語版（CB_LANG_EN）の文の枠の高さ（段階6。英語化）。外側の枠（boxSize の高さ）より小さくすること。日本語版は 140 のまま。")]
        [SerializeField] private float englishTextBoxHeight = 180f;

        [Tooltip("文字の下敷きの濃さ（0〜1）。後ろのレーンの上でも読めるように。")]
        [Range(0f, 1f)]
        [SerializeField] private float backAlpha = 0.6f;

        [Header("手前へ引く動き")]
        [Tooltip("指の丸が滑る長さ（ピクセル）。")]
        [SerializeField] private float strokeLength = 170f;

        [Tooltip("指の丸が滑る時間（秒）。ゆっくり。")]
        [SerializeField] private float strokeSeconds = 1.5f;

        [Tooltip("指の丸が現れる・消える時間（秒）。")]
        [SerializeField] private float gestureFadeSeconds = 0.45f;

        [Tooltip("1周ごとの休み（秒）。")]
        [SerializeField] private float restSeconds = 0.5f;

        [Tooltip("指の丸・矢印の色。")]
        [SerializeField] private Color gestureColor = new Color(0.55f, 0.95f, 1f);

        private CanvasGroup _group;
        private RectTransform _finger;
        private Image _fingerGlow;
        private Image _fingerRing;
        private float _visibleAlpha;
        private float _conditionSince = -1f;
        private float _showStart;
        private bool _done;
        private string _loadedKey;

        /// <summary>今ヒントが見えているか（少しでも。確かめるとき用）。</summary>
        public bool IsShowing => _visibleAlpha > 0.01f;

        private void Awake()
        {
            if (ball == null) ball = FindFirstObjectByType<BallController>();
            if (sequencer == null) sequencer = FindFirstObjectByType<ThrowSequencer>();
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (preview == null) preview = FindFirstObjectByType<CameraPreview>();
            if (tuneIn == null) tuneIn = FindFirstObjectByType<TuneInView>();
            if (title == null) title = FindFirstObjectByType<TitleView>();
            if (records == null) records = FindFirstObjectByType<RecordsView>();
            if (howToPlay == null) howToPlay = FindFirstObjectByType<HowToPlayView>();
            if (root == null || skin == null)
            {
                return;
            }

            _group = root.GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = root.gameObject.AddComponent<CanvasGroup>();
            }
            // 当たり判定を持たない（下の画面を押す操作をそのまま通す）
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;
            Build();
        }

        private void OnEnable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted += OnThrowStarted;
            }
        }

        private void OnDisable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted -= OnThrowStarted;
            }
        }

        /// <summary>投げた：次からは出さない。</summary>
        private void OnThrowStarted()
        {
            FirstThrowHintStore.MarkDone();
            _done = true;
            _loadedKey = FirstThrowHintStore.Key;
        }

        // ================= 組み立て =================

        private void Build()
        {
            RectTransform box = NeonUI.CreateRect(root, "Hint", anchor, anchor, Vector2.zero, boxSize);

            // 左：手前へ引く動き（押す所の輪 → 下向きの線と矢印 → 滑る指の丸）
            RectTransform gesture = NeonUI.CreateRect(box, "Gesture", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(100f, boxSize.y));
            float top = strokeLength * 0.5f;
            Color faint = NeonUI.WithAlpha(gestureColor, 0.35f);
            NeonUI.CreateImage(NeonUI.CreateRect(gesture, "Track", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, strokeLength)), null, faint, false);
            NeonUI.CreateImage(NeonUI.CreateRect(gesture, "PressRing", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, top), new Vector2(54f, 54f)), skin.Ring, NeonUI.WithAlpha(gestureColor, 0.5f), false);
            // 矢印の先（2本の棒を斜めに置いて、下向きの「V」にする。左の棒は「\」、右の棒は「/」で、下の端が線の先で合う）
            for (int side = -1; side <= 1; side += 2)
            {
                RectTransform bar = NeonUI.CreateRect(gesture, side < 0 ? "ArrowL" : "ArrowR", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(side * 9f, -top + 11f), new Vector2(4f, 28f));
                bar.localRotation = Quaternion.Euler(0f, 0f, -side * 40f);
                NeonUI.CreateImage(bar, null, NeonUI.WithAlpha(gestureColor, 0.7f), false);
            }
            _finger = NeonUI.CreateRect(gesture, "Finger", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, top), new Vector2(40f, 40f));
            _fingerGlow = NeonUI.CreateImage(NeonUI.CreateRect(_finger, "Glow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96f, 96f)), skin.Glow, gestureColor, false);
            _fingerRing = NeonUI.CreateImage(NeonUI.CreateRect(_finger, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f)), skin.Ring, Color.white, false);

            // 右：文（下敷きの上に、白い普通の文字で淡々と）
            // 英語版は文の枠を高くする（段階6。英語化）。140 だと3行に分けた2行目・3行目が小さく縮み、800×450 の画面で読めなかったため。
            // 日本語版は今までどおり 140（見た目を変えない）
            float textBoxHeight = GameLanguage.IsEnglish ? englishTextBoxHeight : 140f;
            RectTransform textBox = NeonUI.CreateRect(box, "Text", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(65f, 0f), new Vector2(-130f, textBoxHeight));
            NeonUI.CreateImage(NeonUI.CreateRect(textBox, "Back", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(40f, 10f)), skin.Panel, new Color(0f, 0f, 0f, backAlpha), true);
            // 3行：引いて投げる（大きく）・横にずらすと反対へ・立ち位置はゲージで（段階6で3行目を足した）
            TextMeshProUGUI pull = NeonUI.CreateText(NeonUI.CreateRect(textBox, "Pull", new Vector2(0f, 0.56f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero),
                skin.RegularFont, skin.RegularPlainMaterial, textSize, Color.white, TextAlignmentOptions.Left);
            pull.text = UIText.FirstThrowHintPull;
            pull.enableAutoSizing = true;
            pull.fontSizeMin = 18f;
            pull.fontSizeMax = textSize;
            TextMeshProUGUI side2 = NeonUI.CreateText(NeonUI.CreateRect(textBox, "Side", new Vector2(0f, 0.28f), new Vector2(1f, 0.56f), Vector2.zero, Vector2.zero),
                skin.RegularFont, skin.RegularPlainMaterial, subTextSize, new Color(0.85f, 0.9f, 0.95f), TextAlignmentOptions.Left);
            side2.text = UIText.FirstThrowHintSide;
            side2.enableAutoSizing = true;
            side2.fontSizeMin = 16f;
            side2.fontSizeMax = subTextSize;
            TextMeshProUGUI position = NeonUI.CreateText(NeonUI.CreateRect(textBox, "Position", new Vector2(0f, 0f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero),
                skin.RegularFont, skin.RegularPlainMaterial, subTextSize, new Color(0.85f, 0.9f, 0.95f), TextAlignmentOptions.Left);
            position.text = UIText.FirstThrowHintPosition;
            position.enableAutoSizing = true;
            position.fontSizeMin = 16f;
            position.fontSizeMax = subTextSize;
        }

        // ================= 動き =================

        private void Update()
        {
            if (_group == null)
            {
                return;
            }

            // 鍵が切り替わったら（確かめのとき）読み直す
            if (_loadedKey != FirstThrowHintStore.Key)
            {
                _loadedKey = FirstThrowHintStore.Key;
                _done = FirstThrowHintStore.IsDone;
            }

            bool overlay = (tuneIn != null && tuneIn.IsVisible)
                || (title != null && title.IsVisible)
                || (records != null && records.IsOpen)
                || (howToPlay != null && howToPlay.IsOpen)
                || (gameManager != null && gameManager.IsFinished);
            bool want = FirstThrowHintRules.ShouldShow(
                _done,
                gameManager != null ? gameManager.LaneNumber : 1,
                sequencer != null ? sequencer.ThrowNumber : 1,
                ball != null && ball.IsAiming,
                overlay,
                preview != null && preview.IsPlaying);

            float now = Time.unscaledTime;
            if (want)
            {
                if (_conditionSince < 0f)
                {
                    _conditionSince = now;
                }
            }
            else
            {
                _conditionSince = -1f;
            }

            bool show = want && now - _conditionSince >= appearDelay;
            if (show && _visibleAlpha <= 0.001f)
            {
                // 出し始め：指の丸の動きを頭から
                _showStart = now;
            }
            float speed = show ? 1f / Mathf.Max(fadeInSeconds, 0.01f) : 1f / Mathf.Max(fadeOutSeconds, 0.01f);
            _visibleAlpha = Mathf.MoveTowards(_visibleAlpha, show ? 1f : 0f, speed * Time.unscaledDeltaTime);
            _group.alpha = _visibleAlpha;
            // 念のため毎回：当たり判定は持たない
            _group.blocksRaycasts = false;

            if (_visibleAlpha <= 0.001f || _finger == null)
            {
                return;
            }

            FirstThrowHintRules.Gesture(now - _showStart, gestureFadeSeconds, strokeSeconds, restSeconds, out float progress, out float alpha);
            float top = strokeLength * 0.5f;
            _finger.anchoredPosition = new Vector2(0f, Mathf.Lerp(top, -top, progress));
            _fingerGlow.color = NeonUI.WithAlpha(gestureColor, 0.55f * alpha);
            _fingerRing.color = NeonUI.WithAlpha(Color.white, 0.95f * alpha);
        }
    }
}
