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
    ///   ・右下に作り手の名乗り「produced by 夜中のBBQ」。ネオン管の署名のように、1文字ずつ灯りがともって現れる
    ///     （ロゴより小さく控えめに。主役はロゴ。一度ともったら消えず、点滅もしない）
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

        [Header("名義（右下の署名）")]
        [Tooltip("「produced by」（細く控えめに）。")]
        [SerializeField] private TMP_Text creditPrefix;

        [Tooltip("「夜中のBBQ」（太いネオン）。")]
        [SerializeField] private TMP_Text creditName;

        [Tooltip("名義の下に引くネオン管の線。左から右へ伸びる。")]
        [SerializeField] private Image creditLine;

        [Tooltip("名義の後ろの光の玉。")]
        [SerializeField] private Image creditGlow;

        [Tooltip("タイトルが出てから名義がともり始めるまで（秒）。ロゴが飛び込んだあとにする。")]
        [SerializeField] private float creditDelay = 1.2f;

        [Tooltip("名義が左から右へ全部ともるまで（秒）。")]
        [SerializeField] private float creditRevealSeconds = 1.4f;

        [Tooltip("名義の色（炭火の橙〜赤）の色相の範囲。色相はこの間をゆっくり行き来する。")]
        [SerializeField] private Vector2 creditHueRange = new Vector2(0.97f, 1.07f);

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
            if (creditPrefix != null) creditPrefix.text = UIText.CreditPrefix;
            if (creditName != null) creditName.text = UIText.CreditName;

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
            AnimateCredit(t, now);

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

        /// <summary>
        /// 右下の名義：左から1文字ずつ灯りがともり、下の線が伸びる。ともったあとは炭火のように色がゆっくり揺らぎ、光が呼吸する。
        /// 明るさは0から上がるだけで、消えたり点滅したりはしない。
        /// </summary>
        private void AnimateCredit(float t, float now)
        {
            if (skin == null || creditName == null)
            {
                return;
            }

            float reveal = (t - creditDelay) / Mathf.Max(creditRevealSeconds, 0.01f);
            float wave = 0.5f + 0.5f * Mathf.Sin(now * 0.7f);
            Color fire = NeonUI.Hue(Mathf.Lerp(creditHueRange.x, creditHueRange.y, wave), 0.85f);
            float breath = NeonUI.Breath(3f);

            // 「produced by」が先に、「夜中のBBQ」が少し遅れてともる
            RevealCharacters(creditPrefix, reveal * 1.6f);
            RevealCharacters(creditName, reveal - 0.25f);
            NeonUI.SetNeonColor(creditName, fire, Mathf.Clamp01(reveal) * (0.45f + 0.25f * breath));

            if (creditLine != null)
            {
                float grow = NeonUI.EaseOutCubic(reveal - 0.2f);
                creditLine.rectTransform.localScale = new Vector3(grow, 1f, 1f);
                creditLine.color = Color.Lerp(fire, Color.white, 0.3f * breath);
            }
            if (creditGlow != null)
            {
                creditGlow.color = NeonUI.WithAlpha(fire, Mathf.Clamp01(reveal) * (0.16f + 0.08f * breath));
            }
        }

        /// <summary>
        /// 文字を左から順にともす。progress が 0 で全部暗く、1 で全部ともる。
        /// 1文字ずつ、なめらかに明るくなる（ぱっと点いたり消えたりしない）。
        /// </summary>
        private static void RevealCharacters(TMP_Text label, float progress)
        {
            if (label == null)
            {
                return;
            }

            label.ForceMeshUpdate();
            TMP_TextInfo info = label.textInfo;
            int count = Mathf.Max(info.characterCount, 1);
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                // その文字の番が来てから、文字2つぶんの間でなめらかに明るくなる
                float lit = Mathf.Clamp01((progress * (count + 2f) - i) / 2f);
                byte alpha = (byte)Mathf.RoundToInt(lit * label.color.a * 255f);
                Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
                int v = character.vertexIndex;
                for (int k = 0; k < 4; k++)
                {
                    colors[v + k].a = alpha;
                }
            }
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
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
