using UnityEngine;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 画面の案内の言葉を、端末に合わせて選ぶ（段階6）。MonoBehaviour に依存しない。
    /// </summary>
    public static class InputHints
    {
        /// <summary>
        /// 「CLICK」ではなく「TAP」と出すか。スマホ・タブレット（ブラウザも含む）か、指の入力だけでマウスが無い端末なら TAP。
        /// </summary>
        /// <param name="isMobilePlatform">スマホ・タブレットか（Application.isMobilePlatform）。</param>
        /// <param name="hasTouchscreen">指の入力があるか。</param>
        /// <param name="hasMouse">マウスがあるか。</param>
        public static bool UseTap(bool isMobilePlatform, bool hasTouchscreen, bool hasMouse)
        {
            return isMobilePlatform || (hasTouchscreen && !hasMouse);
        }

        /// <summary>今の端末で TAP を使うか（実際の端末を見る）。</summary>
        public static bool UseTapOnThisDevice()
        {
            return UseTap(Application.isMobilePlatform,
                UnityEngine.InputSystem.Touchscreen.current != null,
                UnityEngine.InputSystem.Mouse.current != null);
        }

        /// <summary>「CLICK TO …」を、TAP を使う端末では「TAP TO …」にする。</summary>
        public static string Choose(string clickText, string tapText, bool useTap)
        {
            return useTap ? tapText : clickText;
        }
    }

    /// <summary>
    /// カーブのスライダーを離したときに、真ん中付近なら STRAIGHT（0）に吸い付ける（段階6）。MonoBehaviour に依存しない。
    /// </summary>
    public static class CurveSnap
    {
        /// <summary>|値| が幅より小さければ 0 にする。幅は 0〜1（0.05 で ±5%）。</summary>
        public static float Apply(float value, float width)
        {
            return Mathf.Abs(value) < Mathf.Abs(width) ? 0f : value;
        }
    }

    /// <summary>
    /// 立ち位置のゲージ（POSITION）の値と、ボールの立ち位置（m）の対応（段階6）。MonoBehaviour に依存しない。
    /// ゲージの値は -1（左いっぱい）〜 +1（右いっぱい）で、立ち位置の範囲（±limit m）に比例させる。
    /// </summary>
    public static class PositionGauge
    {
        /// <summary>ゲージの値（-1〜+1）を立ち位置（m）にする。範囲の外は端に収める。</summary>
        public static float ToOffset(float value, float limit)
        {
            return Mathf.Clamp(value, -1f, 1f) * Mathf.Abs(limit);
        }

        /// <summary>立ち位置（m）をゲージの値（-1〜+1）にする。範囲が 0 なら 0。</summary>
        public static float ToValue(float offset, float limit)
        {
            float l = Mathf.Abs(limit);
            return l > Mathf.Epsilon ? Mathf.Clamp(offset / l, -1f, 1f) : 0f;
        }

        /// <summary>立ち位置をセンチメートルの整数にする（表示用）。</summary>
        public static int ToCentimeters(float offset)
        {
            return Mathf.RoundToInt(Mathf.Abs(offset) * 100f);
        }
    }

    /// <summary>
    /// 引いている途中の「CANCEL」の表示の決まり（段階6）。MonoBehaviour に依存しない。
    /// 投げない範囲（引き幅が足りない・上へ動かした）にあり、しかも押した所から一度は動かしたときだけ出す。
    /// 押しただけの瞬間に毎回 CANCEL が出ないようにするため。
    /// </summary>
    public static class ThrowCancelRule
    {
        /// <summary>押した所から、この距離（ピクセル）以上離れたことがあるか。</summary>
        public static bool HasMoved(Vector2 start, Vector2 now, float thresholdPixels)
        {
            return (now - start).sqrMagnitude >= thresholdPixels * thresholdPixels;
        }

        /// <summary>CANCEL を出すか。引いている最中・投げない範囲・一度は動かした、の3つが揃ったとき。</summary>
        public static bool ShouldShow(bool isPulling, bool wouldThrow, bool hasMoved)
        {
            return isPulling && !wouldThrow && hasMoved;
        }
    }
}
