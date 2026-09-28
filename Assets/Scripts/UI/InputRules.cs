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
}
