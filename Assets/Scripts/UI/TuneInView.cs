using CrazyBowling.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyBowling.UI
{
    /// <summary>
    /// タイトルの前の「CLICK TO TUNE IN」の画面（段階6）。起動したときに1回だけ出る（PLAY AGAIN では出ない）。
    ///   ・真っ暗な背景に、古いラジオ（ネオンの枠・目盛りのダイヤル・赤い針・つまみ）と、上に「ON AIR」の札
    ///   ・光はゆっくり明るさが上下するだけ。針はゆっくり左右に動く。点滅はさせない
    ///   ・画面のどこをクリックしても、音を鳴らせる状態にしてから（ブラウザは最初のクリックまで音を出さないため）、
    ///     小さな「ヒュイーン」とナレーションを始め、タイトルを最初から出す
    /// Web でも Windows でも出す（入口の雰囲気をそろえる）。
    /// スクリプトから投球が始まったとき（通しの確認）は、何もせずに閉じる。
    /// </summary>
    public class TuneInView : MonoBehaviour, IPointerClickHandler
    {
        [Header("参照")]
        [Tooltip("見た目の材料（タイトル・得点板と同じもの）。")]
        [SerializeField] private UISkin skin;

        [Tooltip("このあとに出すタイトル画面。空なら同じシーンから探す。")]
        [SerializeField] private TitleView titleView;

        [Tooltip("投球が始まったことを知らせてくる相手。空なら同じシーンから探す。")]
        [SerializeField] private ThrowSequencer sequencer;

        [Header("動き")]
        [Tooltip("起動したときに出すか。")]
        [SerializeField] private bool showOnLaunch = true;

        [Tooltip("クリックしてから消えるまで（秒）。")]
        [SerializeField] private float hideSeconds = 0.5f;

        [Tooltip("光が明るくなって暗くなるまでの周期（秒）。点滅ではなく、ゆっくり上下する。")]
        [SerializeField] private float breathSeconds = 2.6f;

        [Tooltip("針が左右に1往復する時間（秒）。")]
        [SerializeField] private float needleSeconds = 14f;

        private CanvasGroup _group;
        private bool _visible;
        private bool _tuned;
        private float _hideStart = -100f;
        private float _showStart;

        private Image _glow;
        private Image _bodyFrame;
        private Image _dialFrame;
        private Image _needle;
        private Image _needleGlow;
        private Image[] _ticks;
        private Image[] _knobs;
        private Image _signFrame;
        private Image _signGlow;
        private TMP_Text _signLabel;
        private TMP_Text _radioName;
        private TMP_Text _prompt;
        private TMP_Text[] _numbers;

        /// <summary>この画面が出ているか（消えかけも含む）。</summary>
        public bool IsVisible => _visible;

        /// <summary>クリックされて、タイトルへ進んだか。</summary>
        public bool IsTuned => _tuned;

        private void Awake()
        {
            if (titleView == null)
            {
                titleView = FindFirstObjectByType<TitleView>(FindObjectsInactive.Include);
            }
            if (sequencer == null)
            {
                sequencer = FindFirstObjectByType<ThrowSequencer>();
            }

            _group = GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = gameObject.AddComponent<CanvasGroup>();
            }

            Build();
            _visible = showOnLaunch;
            _showStart = Time.unscaledTime;
            ApplyVisible(showOnLaunch ? 1f : 0f);
        }

        private void Start()
        {
            // この画面が出ている間は、タイトルを隠しておく（クリックしたら最初から出す）
            if (_visible && titleView != null)
            {
                titleView.HideImmediately();
            }
        }

        private void OnEnable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted += HideImmediately;
            }
        }

        private void OnDisable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted -= HideImmediately;
            }
        }

        /// <summary>画面のどこかをクリックした：音を鳴らせるようにして、タイトルへ進む。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            TuneIn();
        }

        /// <summary>タイトルへ進む（クリックと同じ。確かめるときにも使う）。</summary>
        public void TuneIn()
        {
            if (!_visible || _tuned)
            {
                return;
            }

            _tuned = true;
            _hideStart = Time.unscaledTime;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            SoundPlayer player = SoundPlayer.Instance;
            if (player != null)
            {
                player.Record("CLICK TO TUNE IN：クリックした");
                player.BeginTitleIntro();
            }
            if (titleView != null)
            {
                titleView.ShowFromStart();
            }
        }

        /// <summary>すぐに消す（スクリプトから投げたとき・撮影のとき）。</summary>
        public void HideImmediately()
        {
            _visible = false;
            _tuned = true;
            ApplyVisible(0f);
        }

        private void ApplyVisible(float alpha)
        {
            _group.alpha = alpha;
            bool on = alpha > 0.001f;
            _group.blocksRaycasts = on && !_tuned;
            _group.interactable = on && !_tuned;
            if (!on && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            if (!_visible || skin == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            float t = now - _showStart;

            // 消えかけ：少し大きくなりながら薄くなる（タイトルが後ろから出てくる）
            if (_hideStart > 0f)
            {
                float h = (now - _hideStart) / Mathf.Max(hideSeconds, 0.01f);
                if (h >= 1f)
                {
                    _hideStart = -100f;
                    HideImmediately();
                    return;
                }
                _group.alpha = 1f - NeonUI.EaseInCubic(h);
                transform.localScale = Vector3.one * (1f + 0.08f * NeonUI.EaseInCubic(h));
            }
            else
            {
                // 出てきた瞬間は、ゆっくり明るくなる（ぱっと点かない）
                _group.alpha = Mathf.Clamp01(t / 0.8f);
                transform.localScale = Vector3.one;
            }

            float breath = NeonUI.Breath(breathSeconds);
            Color amber = NeonUI.Hue(0.085f, 0.8f);
            Color red = NeonUI.Hue(0.99f, 0.85f);

            _glow.color = NeonUI.WithAlpha(amber, 0.10f + 0.06f * breath);
            _bodyFrame.color = Color.Lerp(amber, Color.white, 0.15f + 0.15f * breath);
            _dialFrame.color = NeonUI.Scale(amber, 0.75f + 0.25f * breath);
            foreach (Image tick in _ticks)
            {
                tick.color = NeonUI.WithAlpha(Color.Lerp(amber, Color.white, 0.6f), 0.55f + 0.3f * breath);
            }
            foreach (TMP_Text number in _numbers)
            {
                number.color = NeonUI.WithAlpha(Color.Lerp(amber, Color.white, 0.5f), 0.6f + 0.25f * breath);
            }
            foreach (Image knob in _knobs)
            {
                knob.color = NeonUI.Scale(amber, 0.7f + 0.3f * breath);
            }
            NeonUI.SetNeonColor(_radioName, amber, 0.3f + 0.2f * breath);

            // 針：ダイヤルの上をゆっくり左右に動く（局を探しているように）
            float sweep = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(needleSeconds, 0.1f));
            float x = 300f * sweep + 12f * Mathf.Sin(t * 1.3f);
            _needle.rectTransform.anchoredPosition = new Vector2(x, 60f);
            _needleGlow.rectTransform.anchoredPosition = new Vector2(x, 60f);
            _needle.color = red;
            _needleGlow.color = NeonUI.WithAlpha(red, 0.25f + 0.15f * breath);

            // ON AIR：赤いネオンがゆっくり呼吸する
            float signBreath = NeonUI.Breath(breathSeconds * 1.3f, 0.3f);
            _signFrame.color = Color.Lerp(red, Color.white, 0.2f * signBreath);
            _signGlow.color = NeonUI.WithAlpha(red, 0.18f + 0.14f * signBreath);
            NeonUI.SetNeonColor(_signLabel, red, 0.5f + 0.35f * signBreath);

            // CLICK TO TUNE IN：タイトルの CLICK TO START と同じように、色がゆっくり移り、呼吸する
            float promptBreath = NeonUI.Breath(2.2f);
            _prompt.alpha = Mathf.Clamp01((t - 0.6f) / 0.6f) * (0.7f + 0.3f * promptBreath);
            NeonUI.SetNeonColor(_prompt, NeonUI.Hue(now * 0.1f, 0.7f), 0.4f + 0.4f * promptBreath);
        }

        /// <summary>見た目を組み立てる（1回だけ）。</summary>
        private void Build()
        {
            if (skin == null)
            {
                return;
            }

            // 真っ暗な背景：画面のどこをクリックしても受け取る
            RectTransform bg = NeonUI.CreateStretch(transform, "Background");
            Image bgImage = NeonUI.CreateImage(bg, null, new Color(0.01f, 0.01f, 0.03f, 1f), false);
            bgImage.raycastTarget = true;

            // ラジオの後ろの淡い光
            _glow = NeonUI.CreateImage(Rect("Glow", new Vector2(0f, 20f), new Vector2(1500f, 900f)), skin.Glow, Color.clear, false);

            // ラジオの箱：木の色のパネルと、アンバーのネオンの縁
            NeonUI.CreateImage(Rect("Body", new Vector2(0f, 0f), new Vector2(940f, 380f)), skin.Panel, new Color(0.10f, 0.05f, 0.03f, 0.95f), true);
            _bodyFrame = NeonUI.CreateImage(Rect("BodyFrame", new Vector2(0f, 0f), new Vector2(960f, 400f)), skin.NeonFrame, Color.white, true);

            // ダイヤルの窓
            NeonUI.CreateImage(Rect("Dial", new Vector2(0f, 60f), new Vector2(760f, 150f)), skin.Panel, new Color(0.03f, 0.02f, 0.015f, 1f), true);
            _dialFrame = NeonUI.CreateImage(Rect("DialFrame", new Vector2(0f, 60f), new Vector2(780f, 170f)), skin.NeonFrame, Color.white, true);

            // 目盛り：41本。5本ごとに長く、その下に数字
            const int tickCount = 41;
            _ticks = new Image[tickCount];
            string[] labels = UIText.TuneInDialNumbers;
            _numbers = new TMP_Text[labels.Length];
            for (int i = 0; i < tickCount; i++)
            {
                float x = Mathf.Lerp(-320f, 320f, i / (float)(tickCount - 1));
                bool major = i % 5 == 0;
                _ticks[i] = NeonUI.CreateImage(Rect($"Tick{i:00}", new Vector2(x, major ? 100f : 106f), new Vector2(major ? 4f : 2f, major ? 40f : 24f)), null, Color.white, false);
                if (major && i / 5 < labels.Length)
                {
                    int k = i / 5;
                    _numbers[k] = NeonUI.CreateText(Rect($"Number{k}", new Vector2(x, 58f), new Vector2(90f, 36f)),
                        skin.RegularFont, skin.RegularPlainMaterial, 26f, Color.white, TextAlignmentOptions.Center);
                    _numbers[k].text = labels[k];
                }
            }
            TMP_Text unit = NeonUI.CreateText(Rect("Unit", new Vector2(362f, 58f), new Vector2(40f, 30f)),
                skin.RegularFont, skin.RegularPlainMaterial, 20f, new Color(1f, 0.85f, 0.6f, 0.7f), TextAlignmentOptions.Center);
            unit.text = UIText.TuneInDialUnit;

            // 針（赤）と、その光
            _needleGlow = NeonUI.CreateImage(Rect("NeedleGlow", new Vector2(0f, 60f), new Vector2(60f, 190f)), skin.Glow, Color.clear, false);
            _needle = NeonUI.CreateImage(Rect("Needle", new Vector2(0f, 60f), new Vector2(5f, 140f)), null, Color.red, false);

            // つまみ（左右）
            _knobs = new Image[2];
            _knobs[0] = NeonUI.CreateImage(Rect("KnobL", new Vector2(-340f, -105f), new Vector2(120f, 120f)), skin.Ring, Color.white, false);
            _knobs[1] = NeonUI.CreateImage(Rect("KnobR", new Vector2(340f, -105f), new Vector2(120f, 120f)), skin.Ring, Color.white, false);

            // ラジオの名前（架空）
            _radioName = NeonUI.CreateText(Rect("RadioName", new Vector2(0f, -105f), new Vector2(520f, 60f)),
                skin.BoldFont, skin.BoldNeonMaterial, 34f, Color.white, TextAlignmentOptions.Center);
            _radioName.text = UIText.TuneInRadioName;

            // ON AIR の札
            _signGlow = NeonUI.CreateImage(Rect("OnAirGlow", new Vector2(0f, 330f), new Vector2(760f, 330f)), skin.Glow, Color.clear, false);
            NeonUI.CreateImage(Rect("OnAir", new Vector2(0f, 330f), new Vector2(380f, 130f)), skin.Panel, new Color(0.08f, 0.01f, 0.01f, 0.95f), true);
            _signFrame = NeonUI.CreateImage(Rect("OnAirFrame", new Vector2(0f, 330f), new Vector2(400f, 150f)), skin.NeonFrame, Color.white, true);
            _signLabel = NeonUI.CreateText(Rect("OnAirLabel", new Vector2(0f, 330f), new Vector2(380f, 130f)),
                skin.BoldFont, skin.BoldNeonMaterial, 84f, Color.white, TextAlignmentOptions.Center);
            _signLabel.text = UIText.TuneInOnAir;

            // CLICK TO TUNE IN
            _prompt = NeonUI.CreateText(Rect("Prompt", new Vector2(0f, -330f), new Vector2(1200f, 110f)),
                skin.BoldFont, skin.BoldNeonMaterial, 76f, Color.white, TextAlignmentOptions.Center);
            _prompt.text = InputHints.Choose(UIText.TuneInPrompt, UIText.TuneInPromptTap, InputHints.UseTapOnThisDevice());
        }

        /// <summary>画面の真ん中を基準にした子を作る。</summary>
        private RectTransform Rect(string name, Vector2 position, Vector2 size)
        {
            return NeonUI.CreateRect(transform, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        }
    }
}
