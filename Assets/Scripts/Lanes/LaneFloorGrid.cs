using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 床に貼る格子のマス目を、実寸で揃える。
    /// マテリアルは全レーンで1枚を共有し、レーンごとの大きさの違いは
    /// MaterialPropertyBlock で吸収する（共有マテリアルは書き換えない）。
    /// ゴルフのグリーンのように、床の傾きを目で読むための目盛り。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class LaneFloorGrid : MonoBehaviour
    {
        [Header("マス目")]
        [Tooltip("1マスの大きさ（m）。ピンの間隔が0.3048mなので、その前後にすると見当が付けやすい。")]
        [SerializeField] private float cellSizeMeters = 0.35f;

        [Tooltip("マスの数を整数に丸める。オンにすると格子が床の端でちょうど切れる。")]
        [SerializeField] private bool snapToWholeCells = true;

        [Header("参照")]
        [Tooltip("床の形。大きさをここから読む。空なら同じ GameObject の MeshFilter を使う。")]
        [SerializeField] private MeshFilter meshFilter;

        /// <summary>URP Lit がタイリングを読む名前。</summary>
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        private void Awake()
        {
            Apply();
        }

        /// <summary>
        /// 床の実寸からタイル数を求めて反映する。
        /// エディタでは、このコンポーネントを右クリックして実行できる。
        /// </summary>
        [ContextMenu("格子を貼り直す")]
        public void Apply()
        {
            Renderer meshRenderer = GetComponent<Renderer>();
            if (meshRenderer == null)
            {
                return;
            }

            Vector2 tiling = CalculateTiling();

            var block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(tiling.x, tiling.y, 0f, 0f));
            meshRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// 床の幅（X）と長さ（Z）を1マスの大きさで割って、タイル数を出す。
        /// 傾いたレーンでも正しく出るよう、ワールドの外接ではなく
        /// メッシュ本来の大きさに拡大率を掛けて求める。
        /// </summary>
        private Vector2 CalculateTiling()
        {
            MeshFilter filter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || cellSizeMeters <= Mathf.Epsilon)
            {
                return Vector2.one;
            }

            Vector3 size = Vector3.Scale(filter.sharedMesh.bounds.size, transform.lossyScale);

            float x = Mathf.Abs(size.x) / cellSizeMeters;
            float z = Mathf.Abs(size.z) / cellSizeMeters;

            if (snapToWholeCells)
            {
                x = Mathf.Max(1f, Mathf.Round(x));
                z = Mathf.Max(1f, Mathf.Round(z));
            }

            return new Vector2(x, z);
        }
    }
}
