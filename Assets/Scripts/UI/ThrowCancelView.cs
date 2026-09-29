using UnityEngine;
using TMPro;
using CrazyBowling.Ball;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 引いている途中で投げない範囲に戻したとき、予測線の代わりにボールの上へ「CANCEL」と出す（段階6）。
    /// このまま離すと投げずに構えに戻ることを分かるようにする。出す・出さないの決まりは <see cref="BallController.ShowsCancel"/>
    /// （押しただけの瞬間には出さない）。BallController の状態を読むだけで、投球には一切干渉しない。
    /// </summary>
    public class ThrowCancelView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("状態を読む相手。")]
        [SerializeField] private BallController ballController;

        [Tooltip("CANCEL の文字。当たり判定は持たせない（押す操作を邪魔しない）。")]
        [SerializeField] private TMP_Text label;

        [Tooltip("ボールの位置を画面の座標に直すカメラ。空なら Main Camera。")]
        [SerializeField] private Camera viewCamera;

        [Header("置き方")]
        [Tooltip("ボールの中心から、画面の上へずらす量（Canvas の座標。高さ1080の画面のとき）。")]
        [SerializeField] private float offsetAboveBall = 90f;

        [Tooltip("出し始めてから、はっきり見えるまでの時間（秒）。")]
        [SerializeField] private float fadeInSeconds = 0.08f;

        /// <summary>今の見え方（0〜1）。</summary>
        private float _alpha;

        private void Awake()
        {
            if (label != null)
            {
                label.text = UIText.Cancel;
                label.raycastTarget = false;
                label.alpha = 0f;
            }
        }

        private void LateUpdate()
        {
            if (ballController == null || label == null)
            {
                return;
            }

            bool show = ballController.ShowsCancel;
            float speed = fadeInSeconds > 0f ? Time.unscaledDeltaTime / fadeInSeconds : 1f;
            _alpha = show ? Mathf.Min(1f, _alpha + speed) : 0f;
            label.alpha = _alpha;
            if (!show)
            {
                return;
            }

            // ボールの真上に置く（Screen Space Overlay の Canvas なので、画面の座標から Canvas の中の座標に直す）
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            RectTransform rect = label.rectTransform;
            RectTransform parent = rect.parent as RectTransform;
            if (cam == null || parent == null)
            {
                return;
            }
            Vector3 screen = cam.WorldToScreenPoint(ballController.transform.position);
            if (screen.z <= 0f)
            {
                return;
            }
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local))
            {
                rect.anchoredPosition = local + new Vector2(0f, offsetAboveBall) - OffsetForAnchor(parent, rect);
            }
        }

        /// <summary>
        /// 親の中の座標（中心が原点）から、anchoredPosition の基準（アンカーの位置）へのずれ。
        /// アンカーを親の真ん中にしておけば 0。
        /// </summary>
        private static Vector2 OffsetForAnchor(RectTransform parent, RectTransform rect)
        {
            Vector2 anchor = (rect.anchorMin + rect.anchorMax) * 0.5f;
            Vector2 size = parent.rect.size;
            return new Vector2((anchor.x - parent.pivot.x) * size.x, (anchor.y - parent.pivot.y) * size.y);
        }
    }
}
