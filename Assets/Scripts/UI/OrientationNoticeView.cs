using UnityEngine;

namespace CrazyBowling.UI
{
    /// <summary>
    /// 画面が縦長のときに「横向きにしてください」と出す。
    /// レーンを奥に見るゲームなので、縦画面では視界が極端に狭くなる。
    /// </summary>
    public class OrientationNoticeView : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("出し入れするまとまり。")]
        [SerializeField] private GameObject noticeRoot;

        [Header("判定")]
        [Tooltip("横幅÷縦幅がこれ未満なら縦長とみなす。1未満で縦長、1.2ならやや横長まで縦扱い。")]
        [SerializeField] private float minAspect = 1.0f;

        private void Update()
        {
            if (noticeRoot == null)
            {
                return;
            }

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            bool needsRotation = aspect < minAspect;

            if (noticeRoot.activeSelf != needsRotation)
            {
                noticeRoot.SetActive(needsRotation);
            }
        }
    }
}
