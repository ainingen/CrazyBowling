using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// ネオンの龍を、龍舞のように体をうねらせながら楕円の道すじで一周させる（5本目の中華料理店の天井）。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 体の節は、頭から一定の間隔で同じ道すじの後ろをたどる。
    /// 時刻から各節の位置と向きを毎フレーム決めるだけなので、前の状態は覚えない
    /// （レーンが消えれば龍も一緒に消える）。
    ///
    /// 色は NeonFlow に任せる（この部品は動かすだけ）。
    /// 当たり判定には一切関係しない。
    /// </summary>
    public class NeonDragonDance : MonoBehaviour
    {
        [Header("体")]
        [Tooltip("頭。道すじのいちばん前に置く。")]
        [SerializeField] private Transform head;

        [Tooltip("胴の節。頭のすぐ後ろから尾へ順に並べる。")]
        [SerializeField] private Transform[] segments;

        [Tooltip("節と節の間隔（道すじに沿った長さ。m）。")]
        [SerializeField] private float spacing = 0.45f;

        [Header("道すじ（この Transform の中の楕円）")]
        [Tooltip("楕円の半径（x：左右、y：奥行き。m）。柱や天井の飾りにかからない大きさにする。")]
        [SerializeField] private Vector2 radii = new Vector2(4.6f, 8f);

        [Tooltip("泳ぐ速さ（道すじに沿って m/秒）。負で逆回り。")]
        [SerializeField] private float speed = 1.6f;

        [Header("うねり")]
        [Tooltip("上下のうねりの幅（m）。")]
        [SerializeField] private float waveHeight = 0.3f;

        [Tooltip("うねりの波長（道すじに沿って m）。")]
        [SerializeField] private float waveLength = 3.2f;

        [Tooltip("うねりが体を伝わる速さ（1秒あたりの波の数）。")]
        [SerializeField] private float waveSpeed = 0.6f;

        private void LateUpdate()
        {
            float time = Time.time;
            float headDistance = time * speed;

            if (head != null)
            {
                Place(head, headDistance + spacing, time);
            }

            if (segments == null)
            {
                return;
            }

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] != null)
                {
                    Place(segments[i], headDistance - i * spacing, time);
                }
            }
        }

        /// <summary>道すじに沿った距離の位置に置き、進む向きを向かせる。</summary>
        private void Place(Transform part, float distance, float time)
        {
            Vector3 position = PointAt(distance, time);
            Vector3 ahead = PointAt(distance + 0.05f * Mathf.Sign(speed == 0f ? 1f : speed), time);
            part.localPosition = position;

            Vector3 direction = ahead - position;
            if (direction.sqrMagnitude > Mathf.Epsilon)
            {
                part.localRotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        /// <summary>
        /// 道すじに沿った距離から、楕円の上の位置を出す。
        /// 楕円の周の長さを平均の半径で近似して角度に直す（速さが場所で少し変わるが、見た目には分からない）。
        /// </summary>
        private Vector3 PointAt(float distance, float time)
        {
            float meanRadius = Mathf.Max(0.01f, (radii.x + radii.y) * 0.5f);
            float angle = distance / meanRadius;
            float wave = waveLength <= Mathf.Epsilon
                ? 0f
                : Mathf.Sin((distance / waveLength - time * waveSpeed) * Mathf.PI * 2f);
            return new Vector3(Mathf.Cos(angle) * radii.x, wave * waveHeight, Mathf.Sin(angle) * radii.y);
        }
    }
}
