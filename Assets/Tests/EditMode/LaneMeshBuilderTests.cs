using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// LaneMeshBuilder の EditMode テスト。
    /// 三角形の向きは目で見ても分かりにくいので、ここで確かめる。
    /// 裏返っていると床が透けて見えなくなる。
    /// </summary>
    public class LaneMeshBuilderTests
    {
        private static LaneShapeSettings Flat()
        {
            return new LaneShapeSettings
            {
                length = 18f,
                width = 1.05f,
                thickness = 0.1f,
                flatStartZ = 15.2f,
                flatEndZ = 15.9f,
            };
        }

        /// <summary>Unity の表面の向き。頂点の並びから面の向きを出す。</summary>
        private static Vector3 FaceNormal(LaneMeshData data, int triangleIndex)
        {
            Vector3 a = data.vertices[data.triangles[triangleIndex * 3]];
            Vector3 b = data.vertices[data.triangles[triangleIndex * 3 + 1]];
            Vector3 c = data.vertices[data.triangles[triangleIndex * 3 + 2]];

            return Vector3.Cross(b - a, c - a).normalized;
        }

        // ---- 数 ----

        [Test]
        public void 上面の頂点の数は分割数から決まる()
        {
            Assert.AreEqual(9 * 181, LaneMeshBuilder.CountTopVertices(8, 180));
        }

        [Test]
        public void 三角形の数は3の倍数になる()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            Assert.AreEqual(0, data.triangles.Length % 3);
        }

        [Test]
        public void 頂点と法線とUVの数がそろっている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            Assert.AreEqual(data.vertices.Length, data.normals.Length);
            Assert.AreEqual(data.vertices.Length, data.uv.Length);
        }

        [Test]
        public void 三角形の番号が頂点の範囲に収まっている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            foreach (int index in data.triangles)
            {
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, data.vertices.Length);
            }
        }

        [Test]
        public void 分割数が0以下でも組める()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 0, 0);

            Assert.Greater(data.vertices.Length, 0);
            Assert.Greater(data.triangles.Length, 0);
        }

        // ---- 向き ----

        [Test]
        public void 上面の三角形は上を向いている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            // 上面は先頭から 4 × 10 × 2 枚
            for (int t = 0; t < 4 * 10 * 2; t++)
            {
                Assert.Greater(FaceNormal(data, t).y, 0.9f, $"{t}枚目の三角形が裏返っている");
            }
        }

        [Test]
        public void 上面の法線も上を向いている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            for (int i = 0; i < LaneMeshBuilder.CountTopVertices(4, 10); i++)
            {
                Assert.AreEqual(1f, data.normals[i].y, 0.001f);
            }
        }

        [Test]
        public void スカートは外を向いている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            int topTriangles = 4 * 10 * 2;
            int total = data.triangles.Length / 3;

            for (int t = topTriangles; t < total; t++)
            {
                Vector3 normal = FaceNormal(data, t);

                // 横か前後を向いていて、上下は向いていない
                Assert.Less(Mathf.Abs(normal.y), 0.1f, $"{t}枚目のスカートが上下を向いている");

                // 頂点の法線と同じ向きであること（裏返っていない）
                Vector3 stored = data.normals[data.triangles[t * 3]];
                Assert.Greater(Vector3.Dot(normal, stored), 0.9f, $"{t}枚目のスカートが裏返っている");
            }
        }

        // ---- 位置 ----

        [Test]
        public void 上面はレーンの幅と長さに収まっている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            for (int i = 0; i < LaneMeshBuilder.CountTopVertices(4, 10); i++)
            {
                Assert.LessOrEqual(Mathf.Abs(data.vertices[i].x), 1.05f * 0.5f + 0.0001f);
                Assert.GreaterOrEqual(data.vertices[i].z, -0.0001f);
                Assert.LessOrEqual(data.vertices[i].z, 18f + 0.0001f);
            }
        }

        [Test]
        public void 上面のUVは0から1に収まっている()
        {
            LaneMeshData data = LaneMeshBuilder.Build(Flat(), 4, 10);

            for (int i = 0; i < LaneMeshBuilder.CountTopVertices(4, 10); i++)
            {
                Assert.GreaterOrEqual(data.uv[i].x, 0f);
                Assert.LessOrEqual(data.uv[i].x, 1f);
                Assert.GreaterOrEqual(data.uv[i].y, 0f);
                Assert.LessOrEqual(data.uv[i].y, 1f);
            }
        }

        [Test]
        public void 起伏を付けると上面の高さが変わる()
        {
            LaneShapeSettings settings = Flat();
            settings.heightAlongLane = AnimationCurve.EaseInOut(0f, 0f, 1f, 0.2f);

            LaneMeshData data = LaneMeshBuilder.Build(settings, 4, 20);

            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;
            for (int i = 0; i < LaneMeshBuilder.CountTopVertices(4, 20); i++)
            {
                min = Mathf.Min(min, data.vertices[i].y);
                max = Mathf.Max(max, data.vertices[i].y);
            }

            Assert.Greater(max - min, 0.05f);
        }
    }
}
