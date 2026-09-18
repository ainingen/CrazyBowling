using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 基本のレーン。床の左右の傾きだけを設定できる。
    /// まっすぐレーンは傾き0、傾いた床レーンは数度にする。
    /// 動くものが要るレーンは、これではなく LaneBehaviour を継承して作る。
    /// </summary>
    public class BasicLane : LaneBehaviour
    {
        [Header("床の傾き")]
        [Tooltip("傾ける対象。床・壁・アンカーをまとめた入れ物を入れる。" +
                 "カメラのアンカーはここに入れない（画面ごと傾いて酔うため）。")]
        [SerializeField] private Transform tiltRoot;

        [Tooltip("左右の傾き（度）。正で右が高くなり、ボールは左へ流れる。負はその逆。" +
                 "ピンは13度を超えると自分で倒れるので、そこまでは上げないこと。")]
        [SerializeField] private float tiltDegrees = 0f;

        /// <summary>今の傾き（度）。</summary>
        public float TiltDegrees => tiltDegrees;

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);
            ApplyTilt();
        }

        /// <summary>
        /// 傾きを反映する。
        /// GameManager はアンカーを読む前にここを呼ぶので、
        /// ボールとピンは傾いたあとの位置に置かれる。
        /// </summary>
        [ContextMenu("傾きを反映する")]
        public void ApplyTilt()
        {
            if (tiltRoot == null)
            {
                return;
            }

            // レーンが進む向き（Z）を軸にして回すと、床が左右に傾く
            tiltRoot.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        }
    }
}
