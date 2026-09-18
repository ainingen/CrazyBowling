using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// ピンデッキの両脇に立てる壁（キックバック）。
    /// 飛んだピンを跳ね返して残りのピンを倒し、ピンがガター側へ出ていくのを防ぐ。
    /// ボールはガターを通るので、ピンが届く高さの範囲だけを覆う。
    /// 配置は PinSet と同じく、Inspector の値から Awake で決める。
    /// </summary>
    public class Kickback : MonoBehaviour
    {
        [Header("壁")]
        [Tooltip("左の壁。BoxCollider と見た目を持つ子。")]
        [SerializeField] private Transform leftWall;

        [Tooltip("右の壁。BoxCollider と見た目を持つ子。")]
        [SerializeField] private Transform rightWall;

        [Header("位置と大きさ")]
        [Tooltip("レーンの中心から、壁の内側の面までの距離（m）。レーン幅1.05mなので端は0.525。")]
        [SerializeField] private float innerDistanceX = 0.525f;

        [Tooltip("壁の厚み（m）。")]
        [SerializeField] private float thickness = 0.04f;

        [Tooltip("壁の下端の高さ（m）。レーン床の上面に合わせる。")]
        [SerializeField] private float bottomY = 0.05f;

        [Tooltip("壁の高さ（m）。ピンの高さ0.38mを超えていればよい。")]
        [SerializeField] private float height = 0.50f;

        [Tooltip("壁の手前端のZ（m）。一番手前のピンより少し手前にする。")]
        [SerializeField] private float startZ = 15.9f;

        [Tooltip("壁の奥端のZ（m）。ピットの手前まで。")]
        [SerializeField] private float endZ = 18.0f;

        private void Awake()
        {
            ApplyLayout();
        }

        /// <summary>
        /// Inspector の値に従って左右の壁を置き直す。
        /// 再生時は Awake から呼ばれる。エディタでは、このコンポーネントを右クリックして実行できる。
        /// </summary>
        [ContextMenu("キックバックを並べ直す")]
        public void ApplyLayout()
        {
            float length = endZ - startZ;
            if (length <= 0f)
            {
                return;
            }

            float centerZ = (startZ + endZ) * 0.5f;
            float centerY = bottomY + height * 0.5f;

            // 内側の面を innerDistanceX にそろえたいので、中心は厚みの半分だけ外へずらす
            float centerX = innerDistanceX + thickness * 0.5f;

            PlaceWall(leftWall, -centerX, centerY, centerZ, length);
            PlaceWall(rightWall, centerX, centerY, centerZ, length);
        }

        /// <summary>壁1枚を置く。Collider は大きさ1の Box を前提に、Scale で実寸にする。</summary>
        private void PlaceWall(Transform wall, float x, float y, float z, float length)
        {
            if (wall == null)
            {
                return;
            }

            wall.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.identity);
            wall.localScale = new Vector3(thickness, height, length);
        }
    }
}
