using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CrazyBowling.Core;

namespace CrazyBowling.UI
{
    /// <summary>
    /// ボタンや表示の枠をネオンにする（LOOK AHEAD・PLAY AGAIN・CLICK TO START・カーブの表示など）。
    /// 起動時に、光の玉と光る枠を子として作り、色をゆっくり流す。
    /// ボタンの働き（押したときの処理）には触らない。
    /// </summary>
    public class NeonButtonSkin : MonoBehaviour
    {
        [Tooltip("見た目の材料。")]
        [SerializeField] private UISkin skin;

        [Tooltip("差し色をもらう相手。空なら差し色を使わず、色を流し続ける。")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("光らせる文字（空でもよい）。")]
        [SerializeField] private TMP_Text label;

        [Tooltip("色を流すか。オフなら差し色のまま。")]
        [SerializeField] private bool flowColors = true;

        [Tooltip("色が一周する時間（秒）。")]
        [SerializeField] private float hueCycleSeconds = 6f;

        [Tooltip("光の呼吸の周期（秒）。点滅ではなく、ゆっくり明るさが上下する。")]
        [SerializeField] private float breathSeconds = 2.2f;

        [Tooltip("枠の外へにじむ光の大きさ（ピクセル）。")]
        [SerializeField] private float glowSpread = 36f;

        private Image _frame;
        private Image _glow;
        private float _hueOffset;

        private void Awake()
        {
            if (skin == null)
            {
                return;
            }

            _hueOffset = Random.value;
            var rect = (RectTransform)transform;

            RectTransform glowRect = NeonUI.CreateRect(rect, "NeonGlow", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(glowSpread * 2f, glowSpread * 2f));
            glowRect.SetAsFirstSibling();
            _glow = NeonUI.CreateImage(glowRect, skin.Glow, Color.clear, false);

            RectTransform frameRect = NeonUI.CreateRect(rect, "NeonFrame", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(16f, 16f));
            _frame = NeonUI.CreateImage(frameRect, skin.NeonFrame, Color.white, true);
            _frame.pixelsPerUnitMultiplier = 1.6f;

            // 文字が枠の下に隠れないように、文字を最後に描く
            if (label != null)
            {
                label.transform.SetAsLastSibling();
            }
        }

        private void LateUpdate()
        {
            if (_frame == null)
            {
                return;
            }

            Color accent = flowColors || gameManager == null
                ? NeonUI.Hue(Time.unscaledTime / Mathf.Max(hueCycleSeconds, 0.1f) + _hueOffset, 0.75f)
                : skin.GetAccent(gameManager.LaneNumber);
            float breath = NeonUI.Breath(breathSeconds, _hueOffset);

            _frame.color = Color.Lerp(accent, Color.white, 0.25f);
            _glow.color = NeonUI.WithAlpha(accent, 0.18f + 0.14f * breath);
            NeonUI.SetNeonColor(label, accent, 0.35f + 0.25f * breath);
        }
    }
}
