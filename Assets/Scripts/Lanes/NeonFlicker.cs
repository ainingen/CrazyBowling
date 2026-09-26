using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 古いネオン看板や蛍光灯が、ときどき一瞬だけ暗くなる（またたく）。見た目だけ。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 「壊れかけ」を出したいが、画面全体を明滅させると光に敏感な人の負担になる。
    /// そこで、指定した見た目だけを、数秒に一度、ごく短く、少しだけ暗くする。
    /// 真っ暗にはしない（暗くなっても minLevel までしか下げない）。
    ///
    /// マテリアルは他と共有しているので書き換えない。MaterialPropertyBlock で発光の色だけを差し替える。
    /// 元の発光の色は、共有のマテリアルから読む（シーンの状態を覚えて戻す作りではない）。
    ///
    /// 物理には一切触らない。
    /// </summary>
    public class NeonFlicker : MonoBehaviour
    {
        [Tooltip("またたかせる見た目。発光（_EMISSION）のあるマテリアルを使っていること。")]
        [SerializeField] private Renderer[] targets;

        [Tooltip("次にまたたくまでの間隔（秒）の最短と最長。この間で乱数。")]
        [SerializeField] private Vector2 intervalSeconds = new Vector2(3f, 8f);

        [Tooltip("1回のまたたきで暗くなる回数の最大。1〜この数で乱数。")]
        [SerializeField] private int maxBlinks = 3;

        [Tooltip("暗くなっている長さ（秒）。")]
        [SerializeField] private float dipSeconds = 0.07f;

        [Tooltip("暗くなるときの明るさ（元の何倍か）。0にしない（画面の明滅を強くしないため）。")]
        [SerializeField, Range(0.2f, 1f)] private float minLevel = 0.4f;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _block;
        private Color[] _baseEmission;
        private float _nextAt;
        private int _blinksLeft;
        private float _dipUntil = -1f;
        private bool _dim;

        private void OnEnable()
        {
            _block = new MaterialPropertyBlock();
            int n = targets == null ? 0 : targets.Length;
            _baseEmission = new Color[n];
            for (int i = 0; i < n; i++)
            {
                Material material = targets[i] != null ? targets[i].sharedMaterial : null;
                _baseEmission[i] = material != null && material.HasProperty(EmissionColorId)
                    ? material.GetColor(EmissionColorId)
                    : Color.black;
            }

            _nextAt = Time.time + Random.Range(intervalSeconds.x, intervalSeconds.y);
            _blinksLeft = 0;
            SetLevel(1f);
        }

        private void OnDisable()
        {
            // 付けた色を外しておく（マテリアルの元の色に戻る）
            if (targets == null)
            {
                return;
            }

            foreach (Renderer target in targets)
            {
                if (target != null)
                {
                    target.SetPropertyBlock(null);
                }
            }
        }

        private void Update()
        {
            float now = Time.time;

            if (_dim)
            {
                if (now < _dipUntil)
                {
                    return;
                }

                // 暗い時間が終わった。明るく戻して、続きがあれば少しあけて次へ
                SetLevel(1f);
                _dim = false;
                _nextAt = _blinksLeft > 0
                    ? now + Random.Range(0.05f, 0.15f)
                    : now + Random.Range(intervalSeconds.x, intervalSeconds.y);
                return;
            }

            if (now < _nextAt)
            {
                return;
            }

            if (_blinksLeft <= 0)
            {
                _blinksLeft = Random.Range(1, Mathf.Max(1, maxBlinks) + 1);
            }

            _blinksLeft--;
            _dim = true;
            _dipUntil = now + dipSeconds * Random.Range(0.7f, 1.4f);
            SetLevel(minLevel);
        }

        private void SetLevel(float level)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    continue;
                }

                targets[i].GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _baseEmission[i] * level);
                targets[i].SetPropertyBlock(_block);
            }
        }
    }
}
