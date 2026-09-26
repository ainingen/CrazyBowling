using UnityEngine;

namespace CrazyBowling.Pins
{
    /// <summary>
    /// ピンの見た目1種類ぶん（段階6）。形（Mesh）と、形の部分（サブメッシュ）ごとのマテリアルを持つ。
    /// レーンの設定（LaneData）が持ち、GameManager がレーンに入るたびに PinSet.ApplyLook で付ける。
    ///
    /// ── 作り方の決まり ──────────────────────────────
    ///
    /// ★形は当たり判定にぴったり合わせる。足もとは y=0、てっぺんは y=0.38、いちばん太いところは半径0.055。
    ///   当たり判定は「足もとの箱（幅0.06・高さ0.08）＋胴のカプセル（半径0.055・y 0.08〜0.38）」なので、
    ///   高さ0.08〜0.135 では下の丸みに合わせて細くする。
    ///   見た目が当たり判定より大きいと「見た目では当たっているのに倒れない」ことになる（6本目のコブの教訓）。
    ///
    /// ★見た目だけ。当たり判定・重さ・重心には一切関わらない。
    /// </summary>
    [CreateAssetMenu(fileName = "PinLook", menuName = "CrazyBowling/ピンの見た目", order = 1)]
    public class PinLook : ScriptableObject
    {
        [Tooltip("ピンの形。原点が足もと（y=0）。")]
        [SerializeField] private Mesh mesh;

        [Tooltip("形の部分（サブメッシュ）ごとのマテリアル。順番は形の部分の順。")]
        [SerializeField] private Material[] materials;

        [Tooltip("ピンごとに色を変える部分の番号（サブメッシュ）。-1 なら変えない。")]
        [SerializeField] private int variantSubmesh = -1;

        [Tooltip("ピンごとに配るマテリアル。ピンの並び順に、上の部分へ順に配る（醤油・酢・ラー油など）。")]
        [SerializeField] private Material[] variantMaterials;

        public Mesh Mesh => mesh;

        /// <summary>
        /// 並び順 index のピンに付けるマテリアルの並びを作る。
        /// 色違いがあれば、その部分だけ index 番目の色に差し替える。
        /// </summary>
        public Material[] BuildMaterials(int index)
        {
            var result = materials != null ? (Material[])materials.Clone() : new Material[0];
            if (variantSubmesh >= 0 && variantSubmesh < result.Length
                && variantMaterials != null && variantMaterials.Length > 0)
            {
                result[variantSubmesh] = variantMaterials[Mathf.Abs(index) % variantMaterials.Length];
            }
            return result;
        }
    }
}
