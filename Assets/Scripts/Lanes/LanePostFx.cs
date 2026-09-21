using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、カメラのポストプロセスを入れる。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// カメラはシーンに1台しかない共有のもので、レーンごとには用意されていない。
    /// エディタでポストプロセスを入れてしまうと**全レーンの見た目が変わる**。
    ///
    /// そこでレーンのプレハブにこの部品を置き、
    /// レーンが現れたときに入れ、消えるときに必ず元へ戻す。
    /// Bloom などの中身は、同じレーンのプレハブに置いた Volume が持つので、
    /// そのレーンが無い場面では設定そのものが存在しない。
    ///
    /// 物理には一切触らない。
    /// </summary>
    public class LanePostFx : MonoBehaviour
    {
        [Tooltip("このレーンにいるあいだ、カメラのポストプロセスを入れるか。")]
        [SerializeField] private bool enablePostProcessing = true;

        [Tooltip("ポストプロセスと一緒に、輪郭のギザギザ取りも入れるか。" +
                 "光る細い棒が多いとギザギザが目立つため。")]
        [SerializeField] private bool enableAntialiasing = true;

        /// <summary>元に戻すために覚えておく。</summary>
        private UniversalAdditionalCameraData _cameraData;
        private bool _previousPostProcessing;
        private AntialiasingMode _previousAntialiasing;

        private void OnEnable()
        {
            if (!enablePostProcessing)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // URPでは、この追加データが無いカメラはポストプロセスを通さない。
            // 無ければ付ける（再生を止めれば消える一時的なもの）。
            _cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (_cameraData == null)
            {
                _cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            _previousPostProcessing = _cameraData.renderPostProcessing;
            _previousAntialiasing = _cameraData.antialiasing;

            _cameraData.renderPostProcessing = true;
            if (enableAntialiasing)
            {
                _cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
        }

        private void OnDisable()
        {
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
        }

        /// <summary>カメラを元に戻す。戻し忘れると次のレーンに持ち越してしまう。</summary>
        private void Restore()
        {
            if (_cameraData == null)
            {
                return;
            }

            _cameraData.renderPostProcessing = _previousPostProcessing;
            _cameraData.antialiasing = _previousAntialiasing;
            _cameraData = null;
        }
    }
}
