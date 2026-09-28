using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Ball;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// タイトルへ戻るボタンの決まり（段階6）。MonoBehaviour に依らない（EditMode テストあり）。
    /// </summary>
    public static class TitleReturnRules
    {
        /// <summary>
        /// ゲームの途中の TITLE ボタンを出すか：タイトル（CLICK TO TUNE IN も）が出ていない・結果画面ではない
        /// （結果画面には専用の TITLE ボタンがある）・記録・遊び方・クレジットの画面が開いていない。
        /// </summary>
        public static bool ShowInGameButton(bool titleVisible, bool finished, bool overlayOpen)
        {
            return !titleVisible && !finished && !overlayOpen;
        }

        /// <summary>
        /// ゲームの途中の TITLE ボタンを押せるか：球が転がっていない（決着待ちも含む）・下見のカメラが動いていない。
        /// 押せないときは薄く出す。
        /// </summary>
        public static bool CanPressInGame(bool ballInPlay, bool previewPlaying)
        {
            return !ballInPlay && !previewPlaying;
        }
    }

    /// <summary>
    /// タイトルへ戻る（段階6）。
    ///   ・結果画面の TITLE：PLAY AGAIN と並ぶ。押すとすぐタイトルへ（成績はもう記録してある）
    ///   ・ゲームの途中の TITLE：画面の左下（音・DJ のボタンの上）に小さく。確認の窓（YES / NO）を挟む。球が転がっている間は押せない
    ///   ・戻ると：CLICK TO TUNE IN とタイトルのナレーションは出さない（最初の起動の1回だけ）。DJ の番組は流れ続け、レーンの BGM は止まる
    ///     （タイトルが出ている間はタイトルの曲にする鳴らし係の決まりのまま）。後ろは起動したときと同じく1本目に戻し、前のレーンは片付ける。
    ///     CLICK TO START で1本目から新しいゲームを始める
    /// ★途中で戻ったゲームは記録しない（全レーンを終えたときにしか記録の知らせが出ないため。今の決まりのまま）。判定・得点・物理には関わらない。
    /// </summary>
    public class TitleReturnView : MonoBehaviour
    {
        [Header("参照（空なら同じシーンから探す）")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BallController ball;
        [SerializeField] private ThrowSequencer sequencer;
        [SerializeField] private CameraPreview preview;
        [SerializeField] private TitleView title;
        [SerializeField] private TuneInView tuneIn;
        [SerializeField] private RecordsView records;
        [SerializeField] private HowToPlayView howToPlay;
        [SerializeField] private CreditsView credits;

        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Header("ボタン")]
        [Tooltip("ゲームの途中の TITLE ボタン（画面の左下）。")]
        [SerializeField] private Button inGameButton;

        [Tooltip("結果画面の TITLE ボタン（PLAY AGAIN と並ぶ）。")]
        [SerializeField] private Button resultButton;

        [Tooltip("結果画面の PLAY AGAIN ボタン。TITLE と並べた段の後ろに下敷きを敷くのに使う。")]
        [SerializeField] private RectTransform playAgainButton;

        [Tooltip("結果画面の PLAY AGAIN と TITLE の段の後ろに敷く下敷きの色。すき間から後ろのカーブの表示が覗かないように（タイトルのボタンの段と同じ）。")]
        [SerializeField] private Color resultRowBackColor = new Color(0.01f, 0.01f, 0.04f, 1f);

        [Tooltip("押せないとき（球が転がっている間・下見の間）の濃さ（0〜1）。")]
        [Range(0f, 1f)]
        [SerializeField] private float disabledAlpha = 0.3f;

        [Header("確認の窓")]
        [Tooltip("確認の窓を置く入れ物（画面全体）。")]
        [SerializeField] private RectTransform confirmRoot;

        [Tooltip("確認の文の大きさ。")]
        [SerializeField] private float messageSize = 34f;

        [Tooltip("板の下に敷く濃い色（後ろが透けないように）。")]
        [SerializeField] private Color solidColor = new Color(0.01f, 0.01f, 0.04f, 0.95f);

        /// <summary>ネオンにするボタン1つぶん。</summary>
        private struct NeonButton
        {
            public Image frame;
            public Image glow;
            public TMP_Text label;
            public float offset;
        }

        private readonly List<NeonButton> _buttons = new List<NeonButton>();
        private CanvasGroup _inGameGroup;
        private CanvasGroup _confirmGroup;
        private RectTransform _box;
        private float _confirmOpenTime;

        /// <summary>確認の窓を出しているか。</summary>
        public bool IsConfirming => _confirmGroup != null && _confirmGroup.blocksRaycasts;

        /// <summary>ゲームの途中の TITLE ボタンが出ているか・押せるか（確かめるとき用）。</summary>
        public bool InGameButtonShown => _inGameGroup != null && _inGameGroup.alpha > 0.01f;
        public bool InGameButtonPressable => _inGameGroup != null && _inGameGroup.interactable;

        /// <summary>タイトルへ戻った回数（確かめるとき用）。</summary>
        public int ReturnCount { get; private set; }

        private void Awake()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (ball == null) ball = FindFirstObjectByType<BallController>();
            if (sequencer == null) sequencer = FindFirstObjectByType<ThrowSequencer>();
            if (preview == null) preview = FindFirstObjectByType<CameraPreview>();
            if (title == null) title = FindFirstObjectByType<TitleView>(FindObjectsInactive.Include);
            if (tuneIn == null) tuneIn = FindFirstObjectByType<TuneInView>(FindObjectsInactive.Include);
            if (records == null) records = FindFirstObjectByType<RecordsView>(FindObjectsInactive.Include);
            if (howToPlay == null) howToPlay = FindFirstObjectByType<HowToPlayView>(FindObjectsInactive.Include);
            if (credits == null) credits = FindFirstObjectByType<CreditsView>(FindObjectsInactive.Include);

            if (inGameButton != null)
            {
                inGameButton.onClick.AddListener(RequestReturn);
                SetLabel(inGameButton, UIText.TitleReturn);
                _inGameGroup = inGameButton.GetComponent<CanvasGroup>();
                if (_inGameGroup == null)
                {
                    _inGameGroup = inGameButton.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (resultButton != null)
            {
                resultButton.onClick.AddListener(ReturnToTitle);
                SetLabel(resultButton, UIText.TitleReturn);
                BuildResultRowBack();
            }
            if (confirmRoot != null && skin != null)
            {
                BuildConfirm();
            }
        }

        private void OnDestroy()
        {
            if (inGameButton != null)
            {
                inGameButton.onClick.RemoveListener(RequestReturn);
            }
            if (resultButton != null)
            {
                resultButton.onClick.RemoveListener(ReturnToTitle);
            }
        }

        private void OnEnable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted += CancelReturn;
            }
        }

        private void OnDisable()
        {
            if (sequencer != null)
            {
                sequencer.ThrowStarted -= CancelReturn;
            }
        }

        private static void SetLabel(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        // ================= 動き =================

        /// <summary>ゲームの途中の TITLE：押せるときだけ確認の窓を出す。</summary>
        public void RequestReturn()
        {
            if (!CanPressNow())
            {
                return;
            }
            SetConfirmVisible(true);
        }

        /// <summary>確認の窓の NO：何もせずに窓を閉じる（ゲームはそのまま続く）。</summary>
        public void CancelReturn()
        {
            SetConfirmVisible(false);
        }

        /// <summary>確認の窓の YES。念のため、押せる状態かをもう一度見る。</summary>
        public void ConfirmReturn()
        {
            if (!CanPressNow())
            {
                SetConfirmVisible(false);
                return;
            }
            ReturnToTitle();
        }

        /// <summary>タイトルへ戻る（結果画面の TITLE・確認の窓の YES）。</summary>
        public void ReturnToTitle()
        {
            SetConfirmVisible(false);
            if (records != null) records.Close();
            if (howToPlay != null) howToPlay.Close();
            if (credits != null) credits.Close();

            // 先にタイトルを出す（大見出しを出さないため。CLICK TO TUNE IN とナレーションは出さない）
            if (title != null)
            {
                title.ShowFromStart();
            }
            // 後ろは起動したときと同じく1本目に戻す（前のレーンの部品・音はここで片付く）。記録はしない
            if (gameManager != null)
            {
                gameManager.StartGame();
            }
            ReturnCount++;
            SoundPlayer.Instance?.Record("タイトルへ戻った");
            Debug.Log("タイトルへ戻った（このゲームは記録しない）", this);
        }

        private bool CanPressNow()
        {
            if (gameManager == null || gameManager.IsFinished)
            {
                return gameManager != null;
            }
            return TitleReturnRules.CanPressInGame(ball != null && ball.IsInPlay, preview != null && preview.IsPlaying);
        }

        private void Update()
        {
            if (_inGameGroup == null)
            {
                return;
            }
            bool titleVisible = (title != null && title.IsVisible) || (tuneIn != null && tuneIn.IsVisible);
            bool overlay = (records != null && records.IsOpen) || (howToPlay != null && howToPlay.IsOpen) || (credits != null && credits.IsOpen);
            bool finished = gameManager != null && gameManager.IsFinished;
            bool show = TitleReturnRules.ShowInGameButton(titleVisible, finished, overlay);
            bool pressable = show && !IsConfirming && CanPressNow();

            _inGameGroup.alpha = !show ? 0f : pressable || IsConfirming ? 1f : disabledAlpha;
            _inGameGroup.interactable = pressable;
            _inGameGroup.blocksRaycasts = show;

            // 窓を出している間に、戻れない状態になったら（転がり始めた・タイトルが出た）閉じる
            if (IsConfirming && (!show || (!finished && !CanPressNow())))
            {
                SetConfirmVisible(false);
            }
        }

        /// <summary>
        /// 結果画面の PLAY AGAIN と TITLE の段の後ろに、不透明な下敷きを敷く（1回だけ）。
        /// 無いと 21:9 で、2つのボタンのすき間から後ろのカーブの表示「STRAIGHT」が覗いた。
        /// </summary>
        private void BuildResultRowBack()
        {
            var titleRect = resultButton.transform as RectTransform;
            if (playAgainButton == null || titleRect == null || titleRect.parent != playAgainButton.parent)
            {
                return;
            }
            // 2つのボタンの外側の端から端まで（ボタンの枠の内側に収める）
            float left = Mathf.Min(playAgainButton.anchoredPosition.x, titleRect.anchoredPosition.x) - playAgainButton.sizeDelta.x * playAgainButton.pivot.x;
            float right = Mathf.Max(playAgainButton.anchoredPosition.x, titleRect.anchoredPosition.x) + titleRect.sizeDelta.x * (1f - titleRect.pivot.x);
            RectTransform back = NeonUI.CreateRect(titleRect.parent, "ResultButtonRowBack", playAgainButton.anchorMin, playAgainButton.anchorMax, Vector2.zero, Vector2.zero);
            back.pivot = playAgainButton.pivot;
            back.sizeDelta = new Vector2(right - left - 24f, playAgainButton.sizeDelta.y - 8f);
            back.anchoredPosition = new Vector2((left + right) * 0.5f, playAgainButton.anchoredPosition.y + 4f);
            NeonUI.CreateImage(back, null, resultRowBackColor, false);
            back.SetSiblingIndex(Mathf.Min(playAgainButton.GetSiblingIndex(), titleRect.GetSiblingIndex()));
        }

        // ================= 確認の窓 =================

        private void SetConfirmVisible(bool on)
        {
            if (_confirmGroup == null)
            {
                return;
            }
            if (on && !IsConfirming)
            {
                _confirmOpenTime = Time.unscaledTime;
            }
            _confirmGroup.alpha = on ? 1f : 0f;
            _confirmGroup.blocksRaycasts = on;
            _confirmGroup.interactable = on;
            if (confirmRoot.gameObject.activeSelf != on)
            {
                confirmRoot.gameObject.SetActive(on);
            }
        }

        /// <summary>確認の窓（記録の画面の CLEAR の窓と同じ作り）。はじめは隠しておく。</summary>
        private void BuildConfirm()
        {
            _confirmGroup = confirmRoot.GetComponent<CanvasGroup>();
            if (_confirmGroup == null)
            {
                _confirmGroup = confirmRoot.gameObject.AddComponent<CanvasGroup>();
            }

            // 後ろを暗くして、下を触れないようにする（押しても球は投げられない）
            Image dim = NeonUI.CreateImage(NeonUI.CreateStretch(confirmRoot, "Dim"), null, new Color(0f, 0f, 0f, 0.7f), false);
            dim.raycastTarget = true;

            _box = NeonUI.CreateRect(confirmRoot, "Box", new Vector2(0.27f, 0.32f), new Vector2(0.73f, 0.68f), Vector2.zero, Vector2.zero);
            NeonUI.CreateImage(NeonUI.CreateRect(_box, "Solid", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-12f, -12f)), null, solidColor, false);
            NeonUI.CreateImage(NeonUI.CreateStretch(_box, "Plate"), skin.Panel, new Color(0.02f, 0.02f, 0.06f, 0.96f), true);
            Image frame = NeonUI.CreateImage(NeonUI.CreateRect(_box, "Frame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12f, 12f)), skin.NeonFrame, Color.white, true);
            frame.pixelsPerUnitMultiplier = 1.6f;
            _buttons.Add(new NeonButton { frame = frame, offset = 0.2f });

            TMP_Text heading = Bold(_box, "Title", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.94f), UIText.TitleReturn, 44f, TextAlignmentOptions.Center, true);
            _buttons.Add(new NeonButton { frame = null, label = heading, offset = 0.5f });

            RectTransform messageRect = NeonUI.CreateRect(_box, "Message", new Vector2(0.06f, 0.32f), new Vector2(0.94f, 0.74f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI message = NeonUI.CreateText(messageRect, skin.RegularFont, skin.RegularPlainMaterial, messageSize, new Color(0.9f, 0.9f, 0.95f), TextAlignmentOptions.Center);
            message.textWrappingMode = TextWrappingModes.Normal;
            message.enableAutoSizing = true;
            message.fontSizeMin = 16f;
            message.fontSizeMax = messageSize;
            message.text = UIText.TitleReturnConfirm;

            CreateButton(NeonUI.CreateRect(_box, "Yes", new Vector2(0.1f, 0.08f), new Vector2(0.45f, 0.28f), Vector2.zero, Vector2.zero), UIText.RecordsYes, 36f, ConfirmReturn);
            CreateButton(NeonUI.CreateRect(_box, "No", new Vector2(0.55f, 0.08f), new Vector2(0.9f, 0.28f), Vector2.zero, Vector2.zero), UIText.RecordsNo, 36f, CancelReturn);
            SetConfirmVisible(false);
        }

        private TMP_Text Bold(RectTransform parent, string name, Vector2 min, Vector2 max, string text, float size, TextAlignmentOptions alignment, bool neon)
        {
            RectTransform rect = NeonUI.CreateRect(parent, name, min, max, Vector2.zero, Vector2.zero);
            TextMeshProUGUI label = NeonUI.CreateText(rect, skin.BoldFont, neon ? skin.BoldNeonMaterial : skin.BoldPlainMaterial, size, Color.white, alignment);
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Min(12f, size);
            label.fontSizeMax = size;
            label.text = text;
            return label;
        }

        /// <summary>ネオンのボタンを作る（光の玉・光る枠・文字・押したときの音）。</summary>
        private void CreateButton(RectTransform rect, string text, float size, UnityAction onClick)
        {
            RectTransform glowRect = NeonUI.CreateRect(rect, "NeonGlow", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(56f, 56f));
            Image glow = NeonUI.CreateImage(glowRect, skin.Glow, Color.clear, false);
            Image plate = NeonUI.CreateImage(NeonUI.CreateStretch(rect, "Plate"), skin.Panel, new Color(0f, 0f, 0f, 0.55f), true);
            plate.raycastTarget = true;
            Image frame = NeonUI.CreateImage(NeonUI.CreateRect(rect, "NeonFrame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(12f, 12f)), skin.NeonFrame, Color.white, true);
            frame.pixelsPerUnitMultiplier = 1.6f;
            TMP_Text label = Bold(rect, "Label", new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.9f), text, size, TextAlignmentOptions.Center, true);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.onClick.AddListener(onClick);
            rect.gameObject.AddComponent<ButtonSound>();
            _buttons.Add(new NeonButton { frame = frame, glow = glow, label = label, offset = Random.value });
        }

        private void LateUpdate()
        {
            if (!IsConfirming)
            {
                return;
            }
            float now = Time.unscaledTime;
            foreach (NeonButton b in _buttons)
            {
                Color accent = NeonUI.Hue(now / 6f + b.offset, 0.75f);
                float breath = NeonUI.Breath(2.2f, b.offset);
                if (b.frame != null)
                {
                    b.frame.color = Color.Lerp(accent, Color.white, 0.25f);
                }
                if (b.glow != null)
                {
                    b.glow.color = NeonUI.WithAlpha(accent, 0.18f + 0.14f * breath);
                }
                if (b.label != null)
                {
                    NeonUI.SetNeonColor(b.label, accent, 0.35f + 0.25f * breath);
                }
            }
            // 開くとき：少し大きいところから縮んで止まる
            if (_box != null)
            {
                float t = (now - _confirmOpenTime) / 0.3f;
                float scale = Mathf.LerpUnclamped(1.06f, 1f, NeonUI.EaseOutBack(t));
                _box.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }
}
