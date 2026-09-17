using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// レーン奥のピット（受け皿）。ボールが入ったら、その投球は終わりとみなす。
    /// ピンもここに落ちるが、ピンの扱いは PinSet の落下判定に任せる。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BallPit : MonoBehaviour
    {
        [Tooltip("ピットに入ったことを Console に出す。")]
        [SerializeField] private bool logEvents = true;

        private void Reset()
        {
            // コンポーネントを付けたときに、トリガーにしておく
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            BallController ball = other.GetComponentInParent<BallController>();
            if (ball == null)
            {
                return;
            }

            if (logEvents)
            {
                Debug.Log("ボールがピットに入った", this);
            }

            ball.MarkSettled("ピットに入った");
        }
    }
}
