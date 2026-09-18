using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Lanes
{
    /// <summary>組み上がったメッシュの中身。Mesh に流し込む前の素の配列。</summary>
    public struct LaneMeshData
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] triangles;
    }

    /// <summary>
    /// LaneShape の形から床のメッシュを組む。
    /// 上面の格子と、横から見たときの厚み（スカート）を作る。
    /// 底面は上から見るゲームなので作らない。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class LaneMeshBuilder
    {
        /// <summary>スカートに割り当てる UV。格子の線に当たらないマスの真ん中。</summary>
        private static readonly Vector2 SkirtUV = new Vector2(0.5f, 0.5f);

        /// <summary>
        /// 床のメッシュを組む。
        /// UV は上面が 0〜1 になるので、LaneFloorLook の格子の割り当てがそのまま効く。
        /// </summary>
        /// <param name="segmentsAcross">横の分割数。1マスの細かさ。</param>
        /// <param name="segmentsAlong">奥行きの分割数。起伏の滑らかさを決める。</param>
        public static LaneMeshData Build(LaneShapeSettings shape, int segmentsAcross, int segmentsAlong)
        {
            int across = Mathf.Max(1, segmentsAcross);
            int along = Mathf.Max(1, segmentsAlong);
            float halfWidth = shape.width * 0.5f;
            float bottomY = LaneShape.CalculateBottomY(shape);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();

            // ---- 上面 ----
            int columns = across + 1;
            for (int j = 0; j <= along; j++)
            {
                float z = shape.length * j / along;
                for (int i = 0; i <= across; i++)
                {
                    float x = -halfWidth + shape.width * i / across;

                    vertices.Add(new Vector3(x, LaneShape.SampleHeight(x, z, shape), z));
                    normals.Add(LaneShape.SampleNormal(x, z, shape));
                    uv.Add(new Vector2((float)i / across, (float)j / along));
                }
            }

            for (int j = 0; j < along; j++)
            {
                for (int i = 0; i < across; i++)
                {
                    int a = j * columns + i;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    // この並びで法線が上を向く（Unity は時計回りが表）
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            // ---- スカート（厚みの見た目） ----
            void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
                for (int k = 0; k < 4; k++)
                {
                    normals.Add(normal);
                    uv.Add(SkirtUV);
                }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start + 1); triangles.Add(start + 3); triangles.Add(start + 2);
            }

            // 左右の長い辺
            for (int j = 0; j < along; j++)
            {
                float z0 = shape.length * j / along;
                float z1 = shape.length * (j + 1) / along;

                // 左（-X）。外向きは -X
                var l0 = new Vector3(-halfWidth, LaneShape.SampleHeight(-halfWidth, z0, shape), z0);
                var l1 = new Vector3(-halfWidth, LaneShape.SampleHeight(-halfWidth, z1, shape), z1);
                AddQuad(l0, new Vector3(-halfWidth, bottomY, z0), l1, new Vector3(-halfWidth, bottomY, z1), Vector3.left);

                // 右（+X）。外向きは +X なので順序を逆にする
                var r0 = new Vector3(halfWidth, LaneShape.SampleHeight(halfWidth, z0, shape), z0);
                var r1 = new Vector3(halfWidth, LaneShape.SampleHeight(halfWidth, z1, shape), z1);
                AddQuad(r0, r1, new Vector3(halfWidth, bottomY, z0), new Vector3(halfWidth, bottomY, z1), Vector3.right);
            }

            // 手前と奥の短い辺
            for (int i = 0; i < across; i++)
            {
                float x0 = -halfWidth + shape.width * i / across;
                float x1 = -halfWidth + shape.width * (i + 1) / across;

                // 手前（z=0）。外向きは -Z
                var n0 = new Vector3(x0, LaneShape.SampleHeight(x0, 0f, shape), 0f);
                var n1 = new Vector3(x1, LaneShape.SampleHeight(x1, 0f, shape), 0f);
                AddQuad(n0, n1, new Vector3(x0, bottomY, 0f), new Vector3(x1, bottomY, 0f), Vector3.back);

                // 奥（z=length）。外向きは +Z
                float far = shape.length;
                var f0 = new Vector3(x0, LaneShape.SampleHeight(x0, far, shape), far);
                var f1 = new Vector3(x1, LaneShape.SampleHeight(x1, far, shape), far);
                AddQuad(f0, new Vector3(x0, bottomY, far), f1, new Vector3(x1, bottomY, far), Vector3.forward);
            }

            return new LaneMeshData
            {
                vertices = vertices.ToArray(),
                normals = normals.ToArray(),
                uv = uv.ToArray(),
                triangles = triangles.ToArray(),
            };
        }

        /// <summary>上面だけの頂点の数。テストと見積もりに使う。</summary>
        public static int CountTopVertices(int segmentsAcross, int segmentsAlong)
        {
            return (Mathf.Max(1, segmentsAcross) + 1) * (Mathf.Max(1, segmentsAlong) + 1);
        }
    }
}
