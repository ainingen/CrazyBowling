using UnityEngine;
using UnityEngine.EventSystems;

namespace CrazyBowling.UI
{
    /// <summary>
    /// カーブの表示の文字（STRAIGHT・LEFT・RIGHT）を押したら、STRAIGHT に戻す（段階6）。
    /// <see cref="CurveSliderView"/> が文字に付ける。文字の上はボタンの上と同じ扱いになるので、押しても球は投げられない。
    /// </summary>
    public class CurveLabelTap : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>押されたときに呼ぶ。</summary>
        public System.Action Tapped;

        public void OnPointerClick(PointerEventData eventData)
        {
            Tapped?.Invoke();
        }
    }
}
