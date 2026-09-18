using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>
    /// 起伏のある床のレーン。ゴルフのグリーンのように、場所ごとに傾きが変わる。
    /// 曲線から床のメッシュを組み立てる。
    ///
    /// 段階2-A では見た目だけで、当たり判定は平らなままにしてある。
    /// ボールは見た目の床の上を転がらない。形が作れるかを確かめるための段階。
    /// </summary>
    public class ShapedLane : LaneBehaviour
    {
        [Header("レーンの大きさ")]
        [Tooltip("レーンの長さ（m）。")]
        [SerializeField] private float laneLength = 18f;

        [Tooltip("レーンの幅（m）。")]
        [SerializeField] private float laneWidth = 1.05f;

        [Tooltip("床の厚み（m）。横から見たときの見た目のため。")]
        [SerializeField] private float laneThickness = 0.1f;

        [Header("形")]
        [Tooltip("レーンに沿った中心の高さ。横軸は0（手前）〜1（奥）、縦軸はm。")]
        [SerializeField] private AnimationCurve heightAlongLane = AnimationCurve.Constant(0f, 1f, 0f);

        [Tooltip("レーンに沿った左右の傾き。横軸は0〜1、縦軸は度。正で右が高く、ボールは左へ流れる。" +
                 "ピンは13度を超えると自分で倒れるので、そこまでは上げないこと。")]
        [SerializeField] private AnimationCurve tiltAlongLane = AnimationCurve.Constant(0f, 1f, 0f);

        [Tooltip("横断面の形。横軸は-1（左端）〜1（右端）、縦軸はm。谷型やかまぼこ型にできる。")]
        [SerializeField] private AnimationCurve crossSection = AnimationCurve.Constant(-1f, 1f, 0f);

        [Header("ピン台")]
        [Tooltip("ここから先は平らに寄せ始める位置（m）。ピンが自分で倒れないようにするため。")]
        [SerializeField] private float flatStartZ = 15.2f;

        [Tooltip("ここから先は完全に平らにする位置（m）。")]
        [SerializeField] private float flatEndZ = 15.9f;

        [Header("細かさ")]
        [Tooltip("横の分割数。")]
        [SerializeField] private int segmentsAcross = 8;

        [Tooltip("奥行きの分割数。多いほど起伏が滑らかに見えるが、細かくしすぎないこと。" +
                 "1マスがボールの半径（0.11m）より短くなると、当たり判定の継ぎ目でボールが跳ねる。" +
                 "18mのレーンなら120前後（1マス15cm）が浮きがいちばん小さかった。")]
        [SerializeField] private int segmentsAlong = 120;

        [Header("参照")]
        [Tooltip("組み上げたメッシュを入れる相手。")]
        [SerializeField] private MeshFilter floorMeshFilter;

        [Tooltip("床の当たり判定。組んだメッシュをここにも入れる。" +
                 "空なら当たり判定は変えない（見た目だけの確認に使う）。")]
        [SerializeField] private MeshCollider floorCollider;

        /// <summary>実行時に作ったメッシュ。レーンを出るときに捨てる。</summary>
        private Mesh _runtimeMesh;

        /// <summary>今の形の設定。</summary>
        public LaneShapeSettings Shape => new LaneShapeSettings
        {
            length = laneLength,
            width = laneWidth,
            thickness = laneThickness,
            heightAlongLane = heightAlongLane,
            tiltAlongLane = tiltAlongLane,
            crossSection = crossSection,
            flatStartZ = flatStartZ,
            flatEndZ = flatEndZ,
        };

        public override void OnLaneStart(LaneContext context)
        {
            base.OnLaneStart(context);
            BuildMesh();
        }

        public override void OnLaneEnd()
        {
            base.OnLaneEnd();
            ReleaseMesh();
        }

        private void OnDestroy()
        {
            ReleaseMesh();
        }

        /// <summary>
        /// 床のメッシュを組み直す。
        /// エディタでは、このコンポーネントを右クリックして実行できる。
        /// GameManager はアンカーを読む前にここを通るので、
        /// ボールとピンは形が決まったあとの床に置かれる。
        /// </summary>
        [ContextMenu("床のメッシュを作り直す")]
        public void BuildMesh()
        {
            if (floorMeshFilter == null)
            {
                Debug.LogWarning("床の MeshFilter が入っていないのでメッシュを作れません", this);
                return;
            }

            LaneMeshData data = LaneMeshBuilder.Build(Shape, segmentsAcross, segmentsAlong);

            if (_runtimeMesh == null)
            {
                _runtimeMesh = new Mesh { name = "起伏のある床" };
                _runtimeMesh.MarkDynamic();
            }

            _runtimeMesh.Clear();
            _runtimeMesh.vertices = data.vertices;
            _runtimeMesh.normals = data.normals;
            _runtimeMesh.uv = data.uv;
            _runtimeMesh.triangles = data.triangles;
            _runtimeMesh.RecalculateBounds();

            floorMeshFilter.sharedMesh = _runtimeMesh;
            ApplyCollider();
        }

        /// <summary>
        /// 当たり判定にも同じメッシュを渡す。
        /// 同じメッシュを入れ直すときは一度外さないと、PhysX 側が作り直してくれない。
        /// </summary>
        private void ApplyCollider()
        {
            if (floorCollider == null)
            {
                return;
            }

            floorCollider.sharedMesh = null;
            floorCollider.sharedMesh = _runtimeMesh;
        }

        /// <summary>作ったメッシュを捨てる。放っておくと積み上がる。</summary>
        private void ReleaseMesh()
        {
            if (_runtimeMesh == null)
            {
                return;
            }

            if (floorMeshFilter != null && floorMeshFilter.sharedMesh == _runtimeMesh)
            {
                floorMeshFilter.sharedMesh = null;
            }

            if (floorCollider != null && floorCollider.sharedMesh == _runtimeMesh)
            {
                floorCollider.sharedMesh = null;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeMesh);
            }
            else
            {
                DestroyImmediate(_runtimeMesh);
            }
            _runtimeMesh = null;
        }

        /// <summary>
        /// この位置の床の高さと向きを返す。ワールド座標でやり取りする。
        /// 段階2-C で、ボールとピンをこの床に合わせて置くのに使う。
        /// </summary>
        public bool SampleFloor(Vector3 worldPosition, out float height, out Vector3 normal)
        {
            // メッシュが乗っている Transform を基準にする。
            // レーンのルートを基準にすると、床を持ち上げているぶんだけ高さがずれる
            Transform basis = floorMeshFilter != null ? floorMeshFilter.transform : transform;

            Vector3 local = basis.InverseTransformPoint(worldPosition);
            LaneShapeSettings shape = Shape;

            bool insideLane = local.z >= 0f && local.z <= laneLength
                && Mathf.Abs(local.x) <= laneWidth * 0.5f;

            float localHeight = LaneShape.SampleHeight(local.x, local.z, shape);
            Vector3 localNormal = LaneShape.SampleNormal(local.x, local.z, shape);

            height = basis.TransformPoint(new Vector3(local.x, localHeight, local.z)).y;
            normal = basis.TransformDirection(localNormal).normalized;

            return insideLane;
        }
    }
}
