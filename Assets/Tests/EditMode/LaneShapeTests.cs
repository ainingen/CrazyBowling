using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// LaneShape の EditMode テスト。
    /// ピン台が平らに保たれることと、傾きの向きが仕様どおりかを確かめる。
    /// </summary>
    public class LaneShapeTests
    {
        /// <summary>平らなレーン。</summary>
        private static LaneShapeSettings Flat()
        {
            return new LaneShapeSettings
            {
                length = 18f,
                width = 1.05f,
                thickness = 0.1f,
                heightAlongLane = null,
                tiltAlongLane = null,
                crossSection = null,
                flatStartZ = 15.2f,
                flatEndZ = 15.9f,
            };
        }

        /// <summary>全域で一定の傾きを持つレーン。</summary>
        private static LaneShapeSettings Tilted(float degrees)
        {
            LaneShapeSettings settings = Flat();
            settings.tiltAlongLane = AnimationCurve.Constant(0f, 1f, degrees);
            return settings;
        }

        // ---- 平らなレーン ----

        [Test]
        public void 曲線が空なら高さは0になる()
        {
            Assert.AreEqual(0f, LaneShape.SampleHeight(0f, 9f, Flat()), 0.0001f);
            Assert.AreEqual(0f, LaneShape.SampleHeight(-0.5f, 3f, Flat()), 0.0001f);
        }

        [Test]
        public void 平らなレーンの法線は真上を向く()
        {
            Vector3 normal = LaneShape.SampleNormal(0.2f, 5f, Flat());

            Assert.AreEqual(1f, normal.y, 0.0001f);
            Assert.AreEqual(0f, normal.x, 0.0001f);
            Assert.AreEqual(0f, normal.z, 0.0001f);
        }

        // ---- 傾き ----

        [Test]
        public void 正の傾きでは右が高くなる()
        {
            LaneShapeSettings settings = Tilted(3f);

            float right = LaneShape.SampleHeight(0.5f, 5f, settings);
            float left = LaneShape.SampleHeight(-0.5f, 5f, settings);

            Assert.Greater(right, left);
        }

        [Test]
        public void 傾きの高さは三角関数どおりになる()
        {
            LaneShapeSettings settings = Tilted(3f);

            float expected = 0.5f * Mathf.Tan(3f * Mathf.Deg2Rad);

            Assert.AreEqual(expected, LaneShape.SampleHeight(0.5f, 5f, settings), 0.0001f);
        }

        [Test]
        public void 中心は傾いても高さが変わらない()
        {
            Assert.AreEqual(0f, LaneShape.SampleHeight(0f, 5f, Tilted(5f)), 0.0001f);
        }

        [Test]
        public void 傾いた床の法線は横に倒れる()
        {
            Vector3 normal = LaneShape.SampleNormal(0f, 5f, Tilted(3f));

            // 右が高いので、法線は左（-X）に倒れる
            Assert.Less(normal.x, 0f);
            Assert.Greater(normal.y, 0.9f);
        }

        // ---- ピン台を平らに保つ ----

        [Test]
        public void ピン台の手前までは傾きがそのまま効く()
        {
            Assert.AreEqual(1f, LaneShape.FlatFade(10f, Flat()), 0.0001f);
            Assert.AreEqual(1f, LaneShape.FlatFade(15.2f, Flat()), 0.0001f);
        }

        [Test]
        public void ピン台では傾きが完全に消える()
        {
            Assert.AreEqual(0f, LaneShape.FlatFade(15.9f, Flat()), 0.0001f);
            Assert.AreEqual(0f, LaneShape.FlatFade(16.2f, Flat()), 0.0001f);
        }

        [Test]
        public void ピン台の手前では滑らかにつながる()
        {
            float mid = LaneShape.FlatFade(15.55f, Flat());

            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
        }

        [Test]
        public void ピン台は傾いていても水平になる()
        {
            LaneShapeSettings settings = Tilted(8f);

            float right = LaneShape.SampleHeight(0.5f, 16.2f, settings);
            float left = LaneShape.SampleHeight(-0.5f, 16.2f, settings);

            Assert.AreEqual(left, right, 0.0001f, "ピンの位置では左右の高さが同じであること");
        }

        [Test]
        public void ピン台の法線は真上を向く()
        {
            Vector3 normal = LaneShape.SampleNormal(0.3f, 16.2f, Tilted(8f));

            Assert.AreEqual(1f, normal.y, 0.001f);
        }

        [Test]
        public void ピン台の高さは平らにし始めた所の高さを保つ()
        {
            LaneShapeSettings settings = Flat();
            settings.heightAlongLane = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            float atFlatStart = LaneShape.SampleHeight(0f, 15.2f, settings);
            float atPins = LaneShape.SampleHeight(0f, 16.2f, settings);
            float atEnd = LaneShape.SampleHeight(0f, 18f, settings);

            Assert.AreEqual(atFlatStart, atPins, 0.0001f);
            Assert.AreEqual(atFlatStart, atEnd, 0.0001f);
        }

        // ---- 横断面 ----

        [Test]
        public void 横断面は左右の端で効く()
        {
            LaneShapeSettings settings = Flat();
            settings.crossSection = AnimationCurve.Constant(-1f, 1f, 0.05f);

            Assert.AreEqual(0.05f, LaneShape.SampleHeight(0.5f, 5f, settings), 0.0001f);
        }

        [Test]
        public void 横断面もピン台では消える()
        {
            LaneShapeSettings settings = Flat();
            settings.crossSection = AnimationCurve.Constant(-1f, 1f, 0.05f);

            Assert.AreEqual(0f, LaneShape.SampleHeight(0.5f, 16.2f, settings), 0.0001f);
        }

        // ---- 底 ----

        [Test]
        public void 底は厚みのぶん下がる()
        {
            Assert.AreEqual(-0.1f, LaneShape.CalculateBottomY(Flat()), 0.0001f);
        }

        [Test]
        public void 底はいちばん低い所より下になる()
        {
            LaneShapeSettings settings = Flat();
            settings.heightAlongLane = AnimationCurve.Linear(0f, 0f, 1f, -0.3f);

            float bottom = LaneShape.CalculateBottomY(settings);

            Assert.Less(bottom, LaneShape.SampleHeight(0f, 15.2f, settings));
        }
    }
}
