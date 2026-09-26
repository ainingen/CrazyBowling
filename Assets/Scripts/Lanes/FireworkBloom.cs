using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 窓の外の花火。光の花がゆっくり開いて、なめらかに消える（5本目の中華料理店の円窓）。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 光に敏感な人への配慮で、**閃光にはしない**。明るさは 0 から正弦の山で上がって下がるだけで、
    /// いきなり明るくなる瞬間を作らない。1回ごとに休みを挟む。
    ///
    /// 子の見た目（足し算で重ねる光の花）に、大きさと MaterialPropertyBlock の色を当てるだけ。
    /// 共有のマテリアルは書き換えない。大きさは Inspector の値から、明るさは時刻から毎フレーム決めるので、
    /// 前の状態は覚えない（レーンが消えれば花火も一緒に消える）。
    /// 当たり判定には一切関係しない。
    /// </summary>
    public class FireworkBloom : MonoBehaviour
    {
        [Tooltip("光の花。子の見た目を順に入れる（1つずつ時刻をずらして開く）。")]
        [SerializeField] private Renderer[] flowers;

        [Tooltip("光の花の色。花ごとに順に使う。")]
        [SerializeField] private Color[] colors =
        {
            new Color(1f, 0.55f, 0.2f),
            new Color(1f, 0.3f, 0.5f),
            new Color(0.5f, 0.8f, 1f),
        };

        [Tooltip("開いて消えるまでの長さ（秒）。長いほど穏やか。")]
        [SerializeField] private float bloomSeconds = 3.2f;

        [Tooltip("消えてから次が開くまでの休み（秒）。")]
        [SerializeField] private float restSeconds = 1.6f;

        [Tooltip("いちばん明るいときの明るさ（色に掛ける）。上げすぎると白く飛ぶ。")]
        [SerializeField] private float peakBrightness = 1.4f;

        [Tooltip("開き切ったときの大きさ（m）。")]
        [SerializeField] private float fullSize = 1.4f;

        [Tooltip("開き始めの大きさ（開き切ったときに対する割合）。")]
        [Range(0f, 1f)]
        [SerializeField] private float startScale = 0.25f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            _block = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            if (flowers == null)
            {
                return;
            }

            // 付けた色を外しておく（NeonFlow と同じ。組み立て直したときに前の色が残って見えるのを防ぐ）
            for (int i = 0; i < flowers.Length; i++)
            {
                if (flowers[i] != null)
                {
                    flowers[i].SetPropertyBlock(null);
                }
            }
        }

        private void Update()
        {
            if (flowers == null || _block == null)
            {
                return;
            }

            float cycle = Mathf.Max(0.1f, bloomSeconds + restSeconds);
            for (int i = 0; i < flowers.Length; i++)
            {
                Renderer flower = flowers[i];
                if (flower == null)
                {
                    continue;
                }

                // 花ごとに時刻をずらす（同時に全部が開かない）
                float local = Mathf.Repeat(Time.time + i * cycle * 0.37f, cycle);
                float t = Mathf.Clamp01(local / Mathf.Max(bloomSeconds, 0.01f));
                bool blooming = local < bloomSeconds;

                // 明るさは正弦の山：0 から上がって 0 へ下がる。いきなり明るくならない
                float brightness = blooming ? Mathf.Sin(t * Mathf.PI) * peakBrightness : 0f;
                // 大きさは、開き始めから速く広がり、あとはゆっくり
                float grow = Mathf.Lerp(startScale, 1f, 1f - (1f - t) * (1f - t));

                flower.transform.localScale = Vector3.one * (fullSize * grow);
                Color color = colors != null && colors.Length > 0 ? colors[i % colors.Length] : Color.white;
                _block.Clear();
                _block.SetColor(BaseColorId, color * brightness);
                flower.SetPropertyBlock(_block);
            }
        }
    }
}
