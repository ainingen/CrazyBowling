using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// タイトル画面（段階6）。起動したときに画面全体を覆って出る。ゲームの顔として一番派手にする。
    ///   ・後ろで光の筋が回る。ロゴ「CRAZY BOWLING」は1文字ずつ虹色が流れ、ゆっくり揺れる
    ///   ・その下に説明書口調の一言と注意書き（淡々と）
    ///   ・「CLICK TO START」はゆっくり呼吸する（点滅させない）
    ///   ・下を10本のレーン名が流れ続ける
    ///
    /// GameManager は今までどおり起動と同時に1本目を始める（後ろでうっすら見える）。
    /// 始めるボタンを押すと消えて、1本目を最初からやり直す（結果画面の PLAY AGAIN と同じ呼び出し）。
    /// スクリプトから投球が始まったとき（通しの確認）は、何もせずに閉じる。
    /// 画面全体を覆っている間は、下の画面を触れない（ボールは投げられない）。
    /// </summary>
    public class TitleView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("ゲームを始め直す相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("投球が始まったことを知らせてくる相手。空なら同じシーンから探す。")]
        [SerializeField] private ThrowSequencer sequencer;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("タイトル画面のまとまり（画面全体）。")]
        [SerializeField] private RectTransform root;

        [Tooltip("始めるボタン。")]
        [SerializeField] private Button startButton;

        [Header("文字")]
        [SerializeField] private TMP_Text logoTop;
        [SerializeField] private TMP_Text logoBottom;
        [SerializeField] private TMP_Text taglineLabel;
        [SerializeField] private TMP_Text noticeLabel;
        [SerializeField] private TMP_Text startLabel;

        [Tooltip("流れるレーン名。")]
        [SerializeField] private TMP_Text tickerLabel;

        [Tooltip("光の筋を回す入れ物。")]
        [SerializeField] private RectTransform raysParent;

        [Header("動き")]
        [Tooltip("起動したときに出すか。")]
        [SerializeField] private bool showOnLaunch = true;

        [Tooltip("消えるのにかける時間（秒）。")]
        [SerializeField] private float hideSeconds = 0.4f;

        [Tooltip("光の筋の本数。")]
        [SerializeField] private int rayCount = 20;

        [Tooltip("レーン名が流れる速さ（ピクセル/秒）。")]
        [SerializeField] private float tickerSpeed = 160f;

        private CanvasGroup _group;
        [System.NonSerialized] private Image[] _rays;
        private bool _visible;
        private float _hideStart = -100f;
        private float _showStart;

        /// <summary>タイトルが出ているか（消えかけも含む）。</summary>
        public bool IsVisible => _visible;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
            if (sequencer == null)
            {
                sequencer = FindFirstObjectByType<ThrowSequencer>();
            }
            if (root == null)
            {
                return;
            }

            _group = root.GetComponent<CanvasGroup>();
            if (_group == null)
            {
                _group = root.gameObject.AddComponent<CanvasGroup>();
            }

            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartPressed);
            }

            BuildRays();
            FillTexts();

            _visible = showOnLaunch;
            _showStart = Time.unscaledTime;
            ApplyVisible(showOnLaunch ? 1f : 0f);
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

        private void OnDestroy()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnStartPressed);
            }
        }

        /// <summary>始めるボタン：消えながら、1本目を最初からやり直す。</summary>
        private void OnStartPressed()
        {
            if (!_visible || _hideStart > 0f)
            {
                return;
            }

            _hideStart = Time.unscaledTime;
            if (gameManager != null)
            {
                gameManager.StartGame();
            }

            // 見出しと説明文を最初から出し直す（レーンは1本目のままなので、自分では気づけない）
            LaneIntroView intro = FindFirstObjectByType<LaneIntroView>();
            if (intro != null)
            {
                intro.Replay();
            }
            LaneInfoView info = FindFirstObjectByType<LaneInfoView>();
            if (info != null)
            {
                info.Replay();
            }
        }

        /// <summary>すぐに消す（スクリプトから投げたとき・撮影のとき）。</summary>
        public void HideImmediately()
        {
            _visible = false;
            ApplyVisible(0f);
        }

        private void ApplyVisible(float alpha)
        {
            if (_group == null)
            {
                return;
            }

            _group.alpha = alpha;
            bool on = alpha > 0.001f;
            _group.blocksRaycasts = on;
            _group.interactable = on;
            if (root.gameObject.activeSelf != on)
            {
                root.gameObject.SetActive(on);
            }
        }

        /// <summary>文字の中身を入れる。</summary>
        private void FillTexts()
        {
            if (logoTop != null) logoTop.text = UIText.TitleLogoTop;
            if (logoBottom != null) logoBottom.text = UIText.TitleLogoBottom;
            if (taglineLabel != null) taglineLabel.text = UIText.TitleTagline;
            if (noticeLabel != null) noticeLabel.text = UIText.TitleNotice;
            if (startLabel != null) startLabel.text = UIText.TitleStart;

            if (tickerLabel != null && gameManager != null)
            {
                var builder = new System.Text.StringBuilder();
                for (int i = 0; i < gameManager.LaneCount; i++)
                {
                    LaneData data = gameManager.GetLaneData(i);
                    builder.Append($"{i + 1:00} {(data != null ? data.LaneName : string.Empty)}     ");
                }
                // 2回並べて、途切れずに流れるようにする
                tickerLabel.text = builder.ToString() + builder.ToString();
            }
        }

        /// <summary>光の筋を作る（1回だけ）。</summary>
        private void BuildRays()
        {
            if (raysParent == null || skin == null)
            {
                return;
            }

            _rays = new Image[Mathf.Max(0, rayCount)];
            for (int i = 0; i < _rays.Length; i++)
            {
                RectTransform ray = NeonUI.CreateRect(raysParent, $"Ray{i:00}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 1500f));
                ray.pivot = new Vector2(0.5f, 1f);
                ray.localEulerAngles = new Vector3(0f, 0f, 180f + i * 360f / _rays.Length);
                _rays[i] = NeonUI.CreateImage(ray, skin.Ray, Color.clear, false);
            }
        }

        private void LateUpdate()
        {
            if (!_visible || _group == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            float t = now - _showStart;

            // 消えかけ
            if (_hideStart > 0f)
            {
                float h = (now - _hideStart) / Mathf.Max(hideSeconds, 0.01f);
                if (h >= 1f)
                {
                    _hideStart = -100f;
                    HideImmediately();
                    return;
                }
                ApplyVisible(1f - h);
                root.localScale = Vector3.one * (1f + 0.15f * NeonUI.EaseInCubic(h));
            }
            else
            {
                root.localScale = Vector3.one;
            }

            if (_rays != null)
            {
                raysParent.localEulerAngles = new Vector3(0f, 0f, now * 12f);
                for (int i = 0; i < _rays.Length; i++)
                {
                    _rays[i].color = NeonUI.WithAlpha(NeonUI.Hue(i / (float)_rays.Length + now * 0.06f, 0.8f), 0.32f);
                }
            }

            // ロゴ：ゆっくり揺れる。飛び込みは最初の一回だけ
            float enter = NeonUI.EaseOutBack(t / 0.6f);
            AnimateLogo(logoTop, enter, now, 0f, -4f);
            AnimateLogo(logoBottom, NeonUI.EaseOutBack((t - 0.15f) / 0.6f), now, 0.5f, 3f);

            if (startLabel != null && skin != null)
            {
                float breath = NeonUI.Breath(2.2f);
                startLabel.alpha = Mathf.Clamp01((t - 0.8f) / 0.4f) * (0.7f + 0.3f * breath);
                NeonUI.SetNeonColor(startLabel, NeonUI.Hue(now * 0.1f, 0.7f), 0.4f + 0.4f * breath);
            }

            if (tickerLabel != null)
            {
                float width = tickerLabel.preferredWidth * 0.5f;
                if (width > 1f)
                {
                    float x = -Mathf.Repeat(now * tickerSpeed, width);
                    tickerLabel.rectTransform.anchoredPosition = new Vector2(x, tickerLabel.rectTransform.anchoredPosition.y);
                }
                NeonUI.SetNeonColor(tickerLabel, NeonUI.Hue(now * 0.05f + 0.5f, 0.7f), 0.3f);
            }
        }

        /// <summary>ロゴの1段：飛び込んでから、ゆっくり揺れて呼吸する。</summary>
        private void AnimateLogo(TMP_Text logo, float enter, float now, float phase, float tilt)
        {
            if (logo == null || skin == null)
            {
                return;
            }

            float sway = Mathf.Sin((now * 0.6f + phase) * Mathf.PI * 2f);
            float scale = Mathf.LerpUnclamped(2.4f, 1f, enter) * (1f + 0.025f * sway);
            logo.rectTransform.localScale = new Vector3(scale, scale, 1f);
            logo.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(tilt * 4f, tilt, enter) + 1.2f * sway);
            logo.alpha = Mathf.Clamp01(enter * 2f);
            NeonUI.SetNeonColor(logo, NeonUI.Hue(now * 0.12f + phase, 0.85f), 0.55f + 0.3f * NeonUI.Breath(1.8f, phase));
        }
    }
}
