using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、ボールの見た目を差し替える。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// ボールはシーンに1つしかない共有のもので、レーンごとに用意されていない。
    /// マテリアルを直接書き換えると**全レーンのボールが変わってしまう**。
    ///
    /// そこでレーンのプレハブにこの部品を置き、
    /// レーンが現れたときに差し替え、消えるときに必ず元へ戻す。
    /// 他のレーンに広げたいときは、そのレーンのプレハブに同じ部品を足して
    /// マテリアルを入れるだけでよい。
    ///
    /// 物理には一切触らない。差し替えるのは MeshRenderer のマテリアルだけ。
    /// </summary>
    public class LaneBallLook : MonoBehaviour
    {
        [Tooltip("このレーンにいるあいだ、ボールに使うマテリアル。" +
                 "空なら何もしない。")]
        [SerializeField] private Material ballMaterial;

        [Tooltip("ボールが回っているのを見せるために、見た目だけ余分に回すか。" +
                 "無地の球は回っても止まって見えるので、模様のあるマテリアルと合わせて使う。")]
        [SerializeField] private bool spinVisual = false;

        [Tooltip("見た目を余分に回す速さ（度／秒）。")]
        [SerializeField] private float spinDegreesPerSecond = 120f;

        /// <summary>差し替えた相手と、元のマテリアル。戻すために覚えておく。</summary>
        private Renderer[] _renderers;
        private Material[] _originals;
        private Transform _spinTarget;
        private Quaternion _spinRest;

        private void OnEnable()
        {
            if (ballMaterial == null)
            {
                return;
            }

            var ball = Object.FindFirstObjectByType<Ball.BallController>();
            if (ball == null)
            {
                return;
            }

            _renderers = ball.GetComponentsInChildren<Renderer>(true);
            _originals = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _originals[i] = _renderers[i].sharedMaterial;
                _renderers[i].sharedMaterial = ballMaterial;
            }

            if (spinVisual && _renderers.Length > 0)
            {
                _spinTarget = _renderers[0].transform;
                _spinRest = _spinTarget.localRotation;
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

        private void Update()
        {
            if (_spinTarget == null)
            {
                return;
            }

            // 見た目の子だけを回す。親（物理）には触らない
            _spinTarget.localRotation =
                _spinRest * Quaternion.Euler(0f, spinDegreesPerSecond * Time.time, 0f);
        }

        /// <summary>ボールの見た目を元に戻す。戻し忘れると次のレーンに持ち越してしまう。</summary>
        private void Restore()
        {
            if (_renderers == null || _originals == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _originals[i] != null)
                {
                    _renderers[i].sharedMaterial = _originals[i];
                }
            }

            if (_spinTarget != null)
            {
                _spinTarget.localRotation = _spinRest;
                _spinTarget = null;
            }

            _renderers = null;
            _originals = null;
        }
    }
}
