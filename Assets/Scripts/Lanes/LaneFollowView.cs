using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、追従カメラのずれ（ボールからどれだけ離れて追うか）を変える。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// カメラはシーンに1台しかない共有のもの。輪をくぐるレーンでは、
    /// 通常のずれのままだとカメラが輪の中を通り、ガラスの帯を突き抜けて見える。
    ///
    /// 有効になったら上書きを入れ、無効になったら上書きを外す（null に戻す）。
    /// ★「入ったときの値を覚えて、出るときに戻す」作りにはしない。
    ///   上書きを外せば、カメラは Inspector の値に戻る。
    ///
    /// 物理には一切触らない。
    /// </summary>
    public class LaneFollowView : MonoBehaviour
    {
        [Tooltip("このレーンにいるあいだの、ボールから見たカメラの位置（ワールド座標のずれ）。")]
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 2.6f, -3.2f);

        private Core.CameraController _camera;

        private void OnEnable()
        {
            _camera = Object.FindFirstObjectByType<Core.CameraController>();
            if (_camera != null)
            {
                _camera.FollowOffsetOverride = followOffset;
            }
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                _camera.FollowOffsetOverride = null;
                _camera = null;
            }
        }
    }
}
