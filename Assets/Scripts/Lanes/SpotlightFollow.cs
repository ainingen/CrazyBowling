using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 指定した相手のほうを、毎フレーム向く。人影を追いかけるスポットライト（4本目のディスコ）。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 動く壁（人影）は物理の親が動かしているので、スポットライトを人影の子にすれば
    /// 追いかけるのは簡単だが、それでは灯が人影と一緒に横へ滑ってしまい、
    /// 「天井から照らしている」ように見えない。
    /// 灯は天井に据えたまま、向きだけを相手へ回す。
    ///
    /// 子に付けたライトや光の筋のメッシュも一緒に向きが変わる。
    /// 向きを変えるだけで、前の状態は覚えない（レーンが消えれば灯も一緒に消える）。
    /// 当たり判定には一切関係しない。
    /// </summary>
    public class SpotlightFollow : MonoBehaviour
    {
        [Tooltip("追いかける相手。人影の胴など、動く見た目を入れる。")]
        [SerializeField] private Transform target;

        [Tooltip("相手の位置からのずれ（相手の Transform ではなく、ワールドの向きで足す）。" +
                 "胴の少し下を狙うと、足もとの床にも光だまりができる。")]
        [SerializeField] private Vector3 targetOffset = Vector3.zero;

        [Tooltip("向きを追いかける速さ（1秒あたり）。0 ならその場でぴたりと向く。" +
                 "少し遅らせると、人が手で操作している灯らしく見える。")]
        [SerializeField] private float followSharpness = 8f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 direction = target.position + targetOffset - transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion aim = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = followSharpness <= 0f
                ? aim
                : Quaternion.Slerp(transform.rotation, aim, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        }
    }
}
