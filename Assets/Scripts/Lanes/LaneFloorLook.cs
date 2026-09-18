using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 床の見た目（格子と高さの色分け）を、レーンの実寸に合わせる。
    /// マテリアルは全レーンで1枚を共有し、レーンごとの違いは
    /// MaterialPropertyBlock で吸収する（共有マテリアルは書き換えない）。
    /// ゴルフのグリーンのように、床の傾きと起伏を目で読むための目盛り。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class LaneFloorLook : MonoBehaviour
    {
        [Header("格子")]
        [Tooltip("1マスの大きさ（m）。ピンの間隔が0.3048mなので、その前後にすると見当が付けやすい。")]
        [SerializeField] private float cellSizeMeters = 0.35f;

        [Tooltip("マスの数を整数に丸める。オンにすると格子が床の端でちょうど切れる。")]
        [SerializeField] private bool snapToWholeCells = true;

        [Header("高さの色分け")]
        [Tooltip("色分けの濃さ。0で床の色のまま、1で色だけになる。強すぎるとボウリング場に見えなくなる。")]
        [Range(0f, 1f)]
        [SerializeField] private float heightStrength = 0.15f;

        [Tooltip("低い側の色。")]
        [SerializeField] private Color lowColor = new Color(0.35f, 0.55f, 1.00f);

        [Tooltip("高い側の色。")]
        [SerializeField] private Color highColor = new Color(1.00f, 0.55f, 0.30f);

        [Tooltip("色が変わる高さの幅を、床の起伏から自動で決める。" +
                 "1.5度の傾きだと端から端で3cmしかないので、ふつうはオンのままにする。")]
        [SerializeField] private bool autoHeightRange = true;

        [Tooltip("自動で決めた幅にかける余裕。1.0でちょうど端が色の端になる。" +
                 "大きくすると色が穏やかになる。")]
        [SerializeField] private float heightRangePadding = 1.1f;

        [Tooltip("自動で決めないときの高さの幅（m）。")]
        [SerializeField] private float manualHeightSpan = 0.05f;

        [Tooltip("床の高低差がこれ未満なら、平らとみなして色を付けない（m）。" +
                 "平らな床に色を付けると、低くも高くもない中間の色で全面が塗られてしまう。")]
        [SerializeField] private float flatFloorThreshold = 0.005f;

        [Header("参照")]
        [Tooltip("床の形。大きさと高さをここから読む。空なら同じ GameObject の MeshFilter を使う。")]
        [SerializeField] private MeshFilter meshFilter;

        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int HeightStrengthId = Shader.PropertyToID("_HeightStrength");
        private static readonly int HeightCenterId = Shader.PropertyToID("_HeightCenter");
        private static readonly int HeightSpanId = Shader.PropertyToID("_HeightSpan");
        private static readonly int LowColorId = Shader.PropertyToID("_LowColor");
        private static readonly int HighColorId = Shader.PropertyToID("_HighColor");

        private void Awake()
        {
            Apply();
        }

        /// <summary>
        /// 床の実寸から格子と色分けを決めて反映する。
        /// エディタでは、このコンポーネントを右クリックして実行できる。
        /// </summary>
        [ContextMenu("床の見た目を貼り直す")]
        public void Apply()
        {
            Renderer meshRenderer = GetComponent<Renderer>();
            if (meshRenderer == null)
            {
                return;
            }

            Vector2 tiling = CalculateTiling();
            MeasureHeightRange(out float center, out float span);

            // 平らな床では色分けを切る。高低差が無いと全面が中間の色になり、
            // 何も伝えないままレーンの色だけが濁る
            float strength = span < flatFloorThreshold ? 0f : heightStrength;

            var block = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(block);

            block.SetVector(BaseMapST, new Vector4(tiling.x, tiling.y, 0f, 0f));
            block.SetFloat(HeightStrengthId, strength);
            block.SetFloat(HeightCenterId, center);
            block.SetFloat(HeightSpanId, span);
            block.SetColor(LowColorId, ToShaderColor(lowColor));
            block.SetColor(HighColorId, ToShaderColor(highColor));

            meshRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Inspector の色をシェーダーに渡せる形に直す。
        /// MaterialPropertyBlock は sRGB からリニアへの変換をしてくれないので、ここで揃える。
        /// これを忘れると、マテリアル側で変換済みの床の色と混ざらず、色が何倍も強く出る。
        /// </summary>
        private static Color ToShaderColor(Color color)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
        }

        /// <summary>今の設定で決まる、色が変わる高さの幅（m）。確認用。</summary>
        public float CurrentHeightSpan
        {
            get
            {
                MeasureHeightRange(out _, out float span);
                return span;
            }
        }

        /// <summary>床の形。Inspector で指定が無ければ同じ GameObject から探す。</summary>
        private MeshFilter ResolveMeshFilter()
        {
            return meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
        }

        /// <summary>
        /// 床の幅（X）と長さ（Z）を1マスの大きさで割って、タイル数を出す。
        /// 傾いたレーンでも正しく出るよう、ワールドの外接ではなく
        /// メッシュ本来の大きさに拡大率を掛けて求める。
        /// </summary>
        private Vector2 CalculateTiling()
        {
            MeshFilter filter = ResolveMeshFilter();
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

        /// <summary>
        /// 床の上を向いた面だけを見て、いちばん低い所といちばん高い所を測る。
        /// 裏面や側面を混ぜると幅が広くなりすぎて、色がほとんど付かなくなる。
        /// </summary>
        private void MeasureHeightRange(out float center, out float span)
        {
            center = transform.position.y;
            span = Mathf.Max(0.0001f, manualHeightSpan);

            if (!autoHeightRange)
            {
                return;
            }

            MeshFilter filter = ResolveMeshFilter();
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            if (mesh == null || !mesh.isReadable)
            {
                // 読めないメッシュのときは、外接から大まかに求める
                Renderer meshRenderer = GetComponent<Renderer>();
                if (meshRenderer != null)
                {
                    Bounds bounds = meshRenderer.bounds;
                    center = bounds.center.y;
                    span = Mathf.Max(0.0001f, bounds.size.y * heightRangePadding);
                }
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            for (int i = 0; i < vertices.Length; i++)
            {
                // 上を向いている面だけを数える。床の裏や側面は見えないので混ぜない
                if (normals != null && normals.Length == vertices.Length)
                {
                    Vector3 worldNormal = transform.TransformDirection(normals[i]);
                    if (worldNormal.y < 0.5f)
                    {
                        continue;
                    }
                }

                float y = transform.TransformPoint(vertices[i]).y;
                if (y < min) min = y;
                if (y > max) max = y;
            }

            if (float.IsInfinity(min) || float.IsInfinity(max))
            {
                return;
            }

            center = (min + max) * 0.5f;
            span = Mathf.Max(0.0001f, (max - min) * Mathf.Max(0.0001f, heightRangePadding));
        }
    }
}
