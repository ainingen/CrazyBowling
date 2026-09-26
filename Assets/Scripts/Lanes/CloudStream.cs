using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 子を奥行き（ローカルの +z）の向きへ一定の速さで流し、端まで行ったら反対の端へ戻す。
    /// 7本目：雲や雲の海が、飛んでいる飛行機の横を後ろへ流れていく。
    ///
    /// ── 位置は毎フレーム時刻から計算する ────────────────────
    ///
    /// 子の z は「何番目の子か」と時刻だけで決める。横（x）と高さ（y）は組み立てたときのまま触らない。
    /// レーンを始めたときの状態を覚えて、あとで戻すことはしない。
    ///
    /// 端で急に消えたり出たりしないように、両端の fadeLength の間は大きさを 0 まで縮める
    /// （形は子のさらに子で作ってあるので、子の大きさは 1 が基準）。
    ///
    /// 見た目だけ。当たり判定には一切触らない。
    /// </summary>
    public class CloudStream : MonoBehaviour
    {
        [Tooltip("流れる速さ（m/秒）。正で +z へ流れる。")]
        [SerializeField] private float speed = 18f;

        [Tooltip("流れる区間の始まりの z（ローカル）。")]
        [SerializeField] private float startZ = -120f;

        [Tooltip("流れる区間の長さ（m）。子はこの中に等間隔に並び、端まで行くと始まりへ戻る。")]
        [SerializeField] private float length = 400f;

        [Tooltip("両端で大きさを縮める長さ（m）。")]
        [SerializeField] private float fadeLength = 40f;

        [Tooltip("並びのずれの大きさ（0〜1）。0 だと等間隔、大きいほど間隔がばらつく。")]
        [SerializeField] private float jitter = 0.6f;

        private void Update()
        {
            int count = transform.childCount;
            if (count == 0 || length <= 0f)
            {
                return;
            }

            float spacing = length / count;
            float travelled = Time.time * speed;

            for (int i = 0; i < count; i++)
            {
                Transform child = transform.GetChild(i);

                // 番号から決まる、ばらつき（毎回同じ値になる）
                float offset = (Hash(i) - 0.5f) * jitter * spacing;
                float along = Mathf.Repeat(i * spacing + offset + travelled, length);

                Vector3 position = child.localPosition;
                position.z = startZ + along;
                child.localPosition = position;

                float fade = Mathf.Min(
                    Mathf.Clamp01(along / Mathf.Max(fadeLength, 0.001f)),
                    Mathf.Clamp01((length - along) / Mathf.Max(fadeLength, 0.001f)));
                child.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, fade);
            }
        }

        /// <summary>番号から 0〜1 の決まった値を作る。</summary>
        private static float Hash(int index)
        {
            float value = Mathf.Sin(index * 12.9898f + 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
