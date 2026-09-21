using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// そのレーンにいるあいだだけ、ピンの見た目を差し替える。
    ///
    /// ── なぜこの作りか ──────────────────────────────
    ///
    /// ピンはシーンに1組しかない共有のもので、レーンごとには用意されていない。
    /// マテリアルを直接書き換えると**全レーンのピンが変わってしまう**。
    ///
    /// そこでレーンのプレハブにこの部品を置き、
    /// レーンが現れたときに差し替え、消えるときに必ず元へ戻す。
    /// 考え方は <see cref="LaneBallLook"/> と同じ。
    ///
    /// マテリアルを複数入れると、ピンの並び順に配っていく（虹色のラックになる）。
    /// 物理には一切触らない。差し替えるのは MeshRenderer のマテリアルだけ。
    /// </summary>
    public class LanePinLook : MonoBehaviour
    {
        [Tooltip("このレーンにいるあいだ、ピンに使うマテリアル。" +
                 "複数入れるとピンの並び順に配る。空なら何もしない。")]
        [SerializeField] private Material[] pinMaterials;

        /// <summary>差し替えた相手と、元のマテリアル。戻すために覚えておく。</summary>
        private Renderer[] _renderers;
        private Material[] _originals;

        private void OnEnable()
        {
            if (pinMaterials == null || pinMaterials.Length == 0)
            {
                return;
            }

            var pinSet = Object.FindFirstObjectByType<Pins.PinSet>();
            if (pinSet == null)
            {
                return;
            }

            // ピンは PinSet の下に並んでいる。取り出す順番は毎回同じになる
            Pins.Pin[] pins = pinSet.GetComponentsInChildren<Pins.Pin>(true);
            var renderers = new System.Collections.Generic.List<Renderer>();
            var originals = new System.Collections.Generic.List<Material>();

            for (int i = 0; i < pins.Length; i++)
            {
                Material material = pinMaterials[i % pinMaterials.Length];
                if (material == null)
                {
                    continue;
                }

                foreach (Renderer meshRenderer in pins[i].GetComponentsInChildren<Renderer>(true))
                {
                    renderers.Add(meshRenderer);
                    originals.Add(meshRenderer.sharedMaterial);
                    meshRenderer.sharedMaterial = material;
                }
            }

            _renderers = renderers.ToArray();
            _originals = originals.ToArray();
        }

        private void OnDisable()
        {
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
        }

        /// <summary>ピンの見た目を元に戻す。戻し忘れると次のレーンに持ち越してしまう。</summary>
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

            _renderers = null;
            _originals = null;
        }
    }
}
