using CrazyBowling.Ball;
using CrazyBowling.Core;
using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// ピンが当たったときの音（段階6）。ピン（Rigidbody の付いた親）に付ける。
    /// ボールとピン、ピン同士の当たりを鳴らし係に知らせるだけで、ピンの動きには触らない。
    /// 鳴らすか・強か弱かは鳴らし係（同時に鳴らす数の上限・同じピンの間隔）が決める。
    /// </summary>
    public class PinHitSound : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            SoundPlayer player = SoundPlayer.Instance;
            if (player == null || collision.rigidbody == null || collision.contactCount == 0)
            {
                return;
            }

            Pin otherPin = collision.rigidbody.GetComponent<Pin>();
            bool withBall = otherPin == null && collision.rigidbody.GetComponent<BallController>() != null;
            if (otherPin == null && !withBall)
            {
                return;
            }

            // ピン同士は両方のピンで呼ばれるので、片方だけが知らせる
            int id = gameObject.GetInstanceID();
            int otherId = otherPin != null ? otherPin.gameObject.GetInstanceID() : 0;
            if (otherPin != null && otherId < id)
            {
                return;
            }

            // ぶつかる速さ：面に向かう向きの速さ（ピンの連鎖爆発と同じ測り方）
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            player.ReportPinHit(id, otherId, speed, withBall);
        }
    }
}
