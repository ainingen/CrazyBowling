using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーンのプレハブが持つ「目印」。
    /// ボール・ピン・ピット・カメラは共通のものを使い回すので、
    /// レーン側は「どこに置くか」だけを教える。
    /// こうしておくと、ピンの設定を10本ぶん複製しなくて済む。
    /// </summary>
    public class LaneAnchors : MonoBehaviour
    {
        [Tooltip("ボールの構え位置。向きがそのまま投げる正面になる。")]
        [SerializeField] private Transform spawnAnchor;

        [Tooltip("ピン台の位置と向き。ここを基準に10本が三角に並ぶ。")]
        [SerializeField] private Transform pinAnchor;

        [Tooltip("ピット（ボールとピンの落ち先）の位置。")]
        [SerializeField] private Transform pitAnchor;

        [Tooltip("構え中のカメラの位置と向き。")]
        [SerializeField] private Transform cameraAnchor;

        /// <summary>ボールの構え位置。</summary>
        public Transform SpawnAnchor => spawnAnchor;

        /// <summary>ピン台の位置と向き。</summary>
        public Transform PinAnchor => pinAnchor;

        /// <summary>ピットの位置。</summary>
        public Transform PitAnchor => pitAnchor;

        /// <summary>構え中のカメラの位置と向き。</summary>
        public Transform CameraAnchor => cameraAnchor;
    }
}
