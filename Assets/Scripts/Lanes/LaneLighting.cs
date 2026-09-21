using UnityEngine;
using UnityEngine.Rendering;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、場面の明るさを変える。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// 照明と環境光はシーンにひとつしかない共有のもので、レーンごとには用意されていない。
    /// シーンの設定を直接変えると**全レーンの明るさが変わる**。
    ///
    /// そこでレーンのプレハブにこの部品を置き、
    /// レーンが現れたときに変え、消えるときに必ず元へ戻す。
    /// 考え方は LaneBallLook・LanePinLook・LanePostFx と同じ。
    ///
    /// ネオンは暗い場所でしか読めない。明るい昼間の月面では、
    /// どれだけ光らせても白く飛ぶだけで、光っているように見えない。
    ///
    /// 物理には一切触らない。
    /// </summary>
    public class LaneLighting : MonoBehaviour
    {
        [Header("太陽（平行光源）")]
        [Tooltip("このレーンにいるあいだの明るさ。元の明るさに対する倍率。1で変えない。")]
        [SerializeField] private float sunIntensityScale = 0.22f;

        [Tooltip("太陽の色を変えるか。")]
        [SerializeField] private bool overrideSunColor = true;

        [Tooltip("このレーンにいるあいだの太陽の色。")]
        [SerializeField] private Color sunColor = new Color(0.55f, 0.6f, 0.95f);

        [Header("環境光")]
        [Tooltip("環境光を変えるか。切ると太陽だけを暗くする。")]
        [SerializeField] private bool overrideAmbient = true;

        [Tooltip("このレーンにいるあいだの環境光の色。暗くしないとネオンが沈む。")]
        [SerializeField] private Color ambientColor = new Color(0.06f, 0.07f, 0.12f);

        /// <summary>元に戻すために覚えておく。</summary>
        private Light _sun;
        private float _previousSunIntensity;
        private Color _previousSunColor;
        private AmbientMode _previousAmbientMode;
        private Color _previousAmbientColor;
        private float _previousAmbientIntensity;
        private bool _applied;

        private void OnEnable()
        {
            if (_applied)
            {
                return;
            }

            _sun = RenderSettings.sun;
            if (_sun == null)
            {
                // シーンの設定に太陽が指定されていないことがある。平行光源を探す
                foreach (Light light in Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (light.type == LightType.Directional)
                    {
                        _sun = light;
                        break;
                    }
                }
            }

            if (_sun != null)
            {
                _previousSunIntensity = _sun.intensity;
                _previousSunColor = _sun.color;
                _sun.intensity = _previousSunIntensity * sunIntensityScale;
                if (overrideSunColor)
                {
                    _sun.color = sunColor;
                }
            }

            _previousAmbientMode = RenderSettings.ambientMode;
            _previousAmbientColor = RenderSettings.ambientLight;
            _previousAmbientIntensity = RenderSettings.ambientIntensity;

            if (overrideAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambientColor;
                RenderSettings.ambientIntensity = 1f;
            }

            _applied = true;
        }

        private void OnDisable()
        {
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
        }

        /// <summary>明るさを元に戻す。戻し忘れると次のレーンが暗いままになる。</summary>
        private void Restore()
        {
            if (!_applied)
            {
                return;
            }

            if (_sun != null)
            {
                _sun.intensity = _previousSunIntensity;
                _sun.color = _previousSunColor;
                _sun = null;
            }

            RenderSettings.ambientMode = _previousAmbientMode;
            RenderSettings.ambientLight = _previousAmbientColor;
            RenderSettings.ambientIntensity = _previousAmbientIntensity;

            _applied = false;
        }
    }
}
