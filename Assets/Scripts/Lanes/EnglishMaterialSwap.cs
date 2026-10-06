using UnityEngine;
using CrazyBowling.UI;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 英語版（言語の印 CB_LANG_EN）のときだけ、ゲームの中の看板のマテリアルを英語のものに差し替える（段階6。英語化）。
    /// 日本語版では何もしないので、見た目は変わらない。
    /// 看板（MeshRenderer）と同じ物に付け、日本語のマテリアルと英語のマテリアルを Inspector で組にして入れる。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class EnglishMaterialSwap : MonoBehaviour
    {
        [Tooltip("差し替える元の、日本語の文字が入ったマテリアル。")]
        [SerializeField] private Material japaneseMaterial;

        [Tooltip("英語版で代わりに使うマテリアル。")]
        [SerializeField] private Material englishMaterial;

        private void Awake()
        {
            if (!GameLanguage.IsEnglish)
            {
                return;
            }

            var target = GetComponent<Renderer>();
            Material[] swapped = Swap(target.sharedMaterials, japaneseMaterial, englishMaterial);
            if (swapped != null)
            {
                target.sharedMaterials = swapped;
            }
        }

        /// <summary>
        /// マテリアルの並びのうち、日本語のものを英語のものに替えた新しい並びを返す。
        /// 替えるものが無いとき（組が欠けている・使っていない）は null を返す。
        /// </summary>
        public static Material[] Swap(Material[] materials, Material japanese, Material english)
        {
            if (materials == null || japanese == null || english == null)
            {
                return null;
            }

            Material[] result = null;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != japanese)
                {
                    continue;
                }
                if (result == null)
                {
                    result = (Material[])materials.Clone();
                }
                result[i] = english;
            }
            return result;
        }
    }
}
