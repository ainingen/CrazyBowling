using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーン終端の通過判定。段階1では Console にログを出すだけで、
    /// 段階2でピンの判定に置き換える土台になる。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LaneGoal : MonoBehaviour
    {
        [Tooltip("通過したことを Console に出す。")]
        [SerializeField] private bool logPass = true;

        private void Reset()
        {
            // コンポーネントを付けたときに、トリガーにしておく
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!logPass)
            {
                return;
            }

            BallController ball = other.GetComponentInParent<BallController>();
            if (ball == null)
            {
                return;
            }

            Rigidbody rigidbody = other.attachedRigidbody;
            float speed = rigidbody != null ? rigidbody.linearVelocity.magnitude : 0f;
            Debug.Log($"ゴール通過：{ball.name} ／ 速度 {speed:F2} m/s", this);
        }
    }
}
