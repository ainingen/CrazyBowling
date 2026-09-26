using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;
using CrazyBowling.Data;

namespace CrazyBowling.UI
{
    /// <summary>
    /// レーンに入ったときの大見出し（段階6）。
    /// 「LANE 04」とレーン名が、傾いて大きめのまま右から飛び込んで止まり、光の帯が横切る。
    /// その下に説明文を白い普通の文字で淡々と出す（派手な見出しと淡々とした説明文の差が冗談の核）。
    /// 2秒あまりで上へ抜けて消える。下見カメラの見せ場を長く覆わない。
    ///
    /// 見た目だけ。ゲームの流れは待たせない。
    /// </summary>
    public class LaneIntroView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("レーンを読む相手。空なら同じシーンから探す。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("動かす入れ物（画面の中ほど）。")]
        [SerializeField] private RectTransform root;

        [Tooltip("「LANE 04」の札。")]
        [SerializeField] private TMP_Text tagLabel;

        [Tooltip("レーン名の大見出し。")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("説明文。")]
        [SerializeField] private TMP_Text descriptionLabel;

        [Tooltip("レーン名の後ろの光の玉。")]
        [SerializeField] private Image glow;

        [Tooltip("レーン名を横切る光の帯。")]
        [SerializeField] private RectTransform band;

        [Tooltip("説明文の後ろの暗い帯。")]
        [SerializeField] private Image descriptionBack;

        [Header("時間（秒）")]
        [Tooltip("飛び込んでくるのにかける時間。")]
        [SerializeField] private float enterSeconds = 0.4f;

        [Tooltip("出ている時間（飛び込みを含む）。")]
        [SerializeField] private float holdSeconds = 2.0f;

        [Tooltip("上へ抜けて消えるのにかける時間。")]
        [SerializeField] private float exitSeconds = 0.45f;

        [Tooltip("レーン名を横切る光の帯の速さ（1回にかける時間）。")]
        [SerializeField] private float bandSeconds = 0.7f;

        [Header("文字")]
        [Tooltip("札の書き方。{0} がレーン番号。")]
        [SerializeField] private string tagFormat = UIText.LaneTagFormat;

        /// <summary>今出している見出しのもとになったレーン。切り替わりを見るために覚える。</summary>
        private int _shownLaneNumber = -1;

        /// <summary>見出しを出し始めた時刻。</summary>
        private float _startTime = -100f;

        private CanvasGroup _group;

        /// <summary>タイトル画面。出ている間は見出しを出さない（タイトルの文と重なって読めなくなる）。</summary>
        private TitleView _title;

        private void Awake()
        {
            if (gameManager == null)
            {
                gameManager = FindFirstObjectByType<GameManager>();
            }
            _title = FindFirstObjectByType<TitleView>();

            if (root != null)
            {
                _group = root.GetComponent<CanvasGroup>();
                if (_group == null)
                {
                    _group = root.gameObject.AddComponent<CanvasGroup>();
                }
                _group.blocksRaycasts = false;
                _group.interactable = false;
                _group.alpha = 0f;
            }
        }

        /// <summary>今のレーンの見出しを最初から出し直す（タイトルから始めたとき）。</summary>
        public void Replay()
        {
            _shownLaneNumber = -1;
        }

        private void LateUpdate()
        {
            if (gameManager == null || root == null)
            {
                return;
            }

            // タイトルが出ている間は出さない。タイトルから始めると Replay() で出し直す
            if (_title != null && _title.IsVisible)
            {
                _group.alpha = 0f;
                return;
            }

            LaneData lane = gameManager.CurrentLane;
            if (lane != null && gameManager.LaneNumber != _shownLaneNumber)
            {
                _shownLaneNumber = gameManager.LaneNumber;
                Begin(lane, gameManager.LaneNumber);
            }

            Animate(Time.unscaledTime - _startTime);
        }

        /// <summary>見出しの中身を入れて、動きを始める。</summary>
        private void Begin(LaneData lane, int laneNumber)
        {
            _startTime = Time.unscaledTime;

            if (tagLabel != null)
            {
                tagLabel.text = string.Format(tagFormat, laneNumber);
            }
            if (nameLabel != null)
            {
                nameLabel.text = lane.LaneName;
            }
            if (descriptionLabel != null)
            {
                descriptionLabel.text = !string.IsNullOrEmpty(lane.HintText) ? lane.HintText : lane.Description;
            }

            if (skin != null)
            {
                Color accent = skin.GetAccent(laneNumber);
                NeonUI.SetNeonColor(nameLabel, accent, 0.6f);
                NeonUI.SetNeonColor(tagLabel, skin.Gold, 0.5f);
                if (glow != null)
                {
                    glow.color = NeonUI.WithAlpha(accent, 0f);
                }
            }
        }

        /// <summary>経過時間 t（秒）に合わせて動かす。</summary>
        private void Animate(float t)
        {
            float total = holdSeconds + exitSeconds;
            if (t < 0f || t > total)
            {
                _group.alpha = 0f;
                return;
            }

            // 飛び込み：右から、少し傾いて大きいまま入り、行きすぎて戻る
            float enter = NeonUI.EaseOutBack(t / Mathf.Max(enterSeconds, 0.01f));
            float exit = t > holdSeconds ? NeonUI.EaseInCubic((t - holdSeconds) / Mathf.Max(exitSeconds, 0.01f)) : 0f;

            _group.alpha = Mathf.Clamp01(t / 0.12f) * (1f - exit);

            if (nameLabel != null)
            {
                RectTransform rect = nameLabel.rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.LerpUnclamped(1100f, 0f, enter), 20f + 160f * exit);
                float scale = Mathf.LerpUnclamped(1.5f, 1f, enter);
                rect.localScale = new Vector3(scale, scale, 1f);
                rect.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpUnclamped(-9f, 0f, enter));

                // 光を少しずつ強くして、そのままゆっくり呼吸させる
                if (skin != null)
                {
                    float breath = NeonUI.Breath(1.4f);
                    NeonUI.SetNeonColor(nameLabel, skin.GetAccent(_shownLaneNumber), 0.45f + 0.35f * breath);
                }
            }

            if (tagLabel != null)
            {
                float drop = NeonUI.EaseOutBack((t - 0.1f) / Mathf.Max(enterSeconds, 0.01f));
                tagLabel.rectTransform.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(260f, 120f, drop) + 160f * exit);
                tagLabel.alpha = Mathf.Clamp01((t - 0.1f) / 0.2f);
            }

            if (descriptionLabel != null)
            {
                // 説明文は動かさず、淡々と出るだけ
                float fade = Mathf.Clamp01((t - 0.45f) / 0.3f);
                descriptionLabel.alpha = fade;
                if (descriptionBack != null)
                {
                    descriptionBack.color = NeonUI.WithAlpha(Color.black, 0.55f * fade);
                }
            }

            if (glow != null && skin != null)
            {
                float swell = Mathf.Clamp01(t / 0.35f);
                glow.color = NeonUI.WithAlpha(skin.GetAccent(_shownLaneNumber), 0.45f * swell);
                glow.rectTransform.localScale = new Vector3(Mathf.Lerp(0.4f, 1f, NeonUI.EaseOutCubic(t / 0.5f)), 1f, 1f);
            }

            if (band != null)
            {
                // 光の帯が左から右へ1回だけ横切る
                float b = (t - enterSeconds * 0.8f) / Mathf.Max(bandSeconds, 0.01f);
                band.gameObject.SetActive(b > 0f && b < 1f);
                band.anchoredPosition = new Vector2(Mathf.Lerp(-900f, 900f, b), band.anchoredPosition.y);
            }
        }
    }
}
