using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 子に付けたスポットライトを回す。ディスコの回転灯。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// ネオンだけにすると、背景（月面）が暗く沈んで見えなくなる。
    /// 「月面なのにネオン」というちぐはぐさが可笑しさの元なので、
    /// 月面側が見えなくなると、ただのネオンステージになってしまう。
    ///
    /// 常に全部が見えている必要はない。**通り過ぎる瞬間に「月だ」と分かれば十分。**
    ///
    /// この部品はレーンのプレハブの中に置く。レーンが消えれば灯も一緒に消えるので、
    /// 他のレーンには漏れない。
    ///
    /// ★ピンを照らす灯は、これとは別に固定で置くこと。
    ///   回る灯で照らすと、外れた瞬間に倒れたかどうかが読めなくなる。
    ///
    /// 当たり判定には一切関係しない。向きと色を変えるだけ。
    /// </summary>
    public class SpinningSpotlights : MonoBehaviour
    {
        [Header("回り方")]
        [Tooltip("1秒あたり何度回るか。速すぎると目が疲れる。")]
        [SerializeField] private float degreesPerSecond = 32f;

        [Tooltip("上下の首振りの幅（度）。0で回るだけ。")]
        [SerializeField] private float nodDegrees = 8f;

        [Tooltip("上下の首振りの速さ（1秒あたり何往復するか）。")]
        [SerializeField] private float nodSpeed = 0.13f;

        [Header("色")]
        [Tooltip("色を変えていくか。切ると、置いたときの色のまま。")]
        [SerializeField] private bool cycleColors = true;

        [Tooltip("色の輪をまわす速さ（1秒あたり何周するか）。")]
        [SerializeField] private float hueSpeed = 0.06f;

        [Tooltip("色の鮮やかさ。1で原色。上げすぎると月面の色が分からなくなる。")]
        [Range(0f, 1f)]
        [SerializeField] private float saturation = 0.55f;

        [Tooltip("色の輪の出発点。台ごとにずらすと、同じ色が揃わない。")]
        [Range(0f, 1f)]
        [SerializeField] private float hueOffset = 0f;

        /// <summary>回す相手と、戻すための元の向き。</summary>
        private Light[] _lights;
        private float[] _hueOffsets;
        private Quaternion _restRotation;

        private void OnEnable()
        {
            _restRotation = transform.localRotation;
            _lights = GetComponentsInChildren<Light>(true);
            _hueOffsets = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
            {
                // 灯ごとに色をずらす。全部同じ色だと回っているのが分かりにくい
                _hueOffsets[i] = hueOffset +
                    (_lights.Length == 0 ? 0f : i / (float)_lights.Length);
            }
        }

        private void OnDisable()
        {
            transform.localRotation = _restRotation;
            _lights = null;
        }

        private void Update()
        {
            if (_lights == null)
            {
                return;
            }

            float time = Time.time;

            float nod = nodDegrees * Mathf.Sin(time * nodSpeed * Mathf.PI * 2f);
            transform.localRotation =
                _restRotation * Quaternion.Euler(nod, time * degreesPerSecond, 0f);

            if (!cycleColors)
            {
                return;
            }

            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] == null)
                {
                    continue;
                }

                float hue = Mathf.Repeat(_hueOffsets[i] + time * hueSpeed, 1f);
                _lights[i].color = Color.HSVToRGB(hue, saturation, 1f);
            }
        }
    }
}
