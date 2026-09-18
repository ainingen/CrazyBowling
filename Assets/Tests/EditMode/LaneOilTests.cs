using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// LaneOil の EditMode テスト。
    /// 「手前は曲がらず、奥で曲がる」を作るための摩擦の分布を確かめる。
    /// </summary>
    public class LaneOilTests
    {
        /// <summary>Inspector の初期値と同じ設定。</summary>
        private static LaneOilSettings Standard()
        {
            return new LaneOilSettings
            {
                enabled = true,
                oilEndZ = 12f,
                transitionLength = 1.5f,
                oilFriction = 0.2f,
                dryFriction = 1f,
            };
        }

        // ---- オイル区画 ----

        [Test]
        public void 手前はオイルの係数になる()
        {
            Assert.AreEqual(0.2f, LaneOil.GetFrictionScale(0f, Standard()), 0.0001f);
            Assert.AreEqual(0.2f, LaneOil.GetFrictionScale(6f, Standard()), 0.0001f);
        }

        [Test]
        public void オイルの終わりちょうどはまだオイル()
        {
            Assert.AreEqual(0.2f, LaneOil.GetFrictionScale(12f, Standard()), 0.0001f);
        }

        // ---- 乾いた区画 ----

        [Test]
        public void 移り変わりを過ぎると乾いた係数になる()
        {
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(13.5f, Standard()), 0.0001f);
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(16.2f, Standard()), 0.0001f);
        }

        [Test]
        public void ピンの位置は完全に乾いている()
        {
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(16.2f, Standard()), 0.0001f);
        }

        // ---- 境目のつなぎ方 ----

        [Test]
        public void 移り変わりの途中は両端の間に入る()
        {
            float mid = LaneOil.GetFrictionScale(12.75f, Standard());

            Assert.Greater(mid, 0.2f);
            Assert.Less(mid, 1f);
        }

        [Test]
        public void 移り変わりの真ん中はちょうど中間になる()
        {
            Assert.AreEqual(0.6f, LaneOil.GetFrictionScale(12.75f, Standard()), 0.0001f);
        }

        [Test]
        public void 移り変わりは折れ線にならない()
        {
            // 両端で傾きが0になっていること。段差があると曲がりが急に折れて見える
            LaneOilSettings settings = Standard();
            const float Step = 0.01f;

            float slopeAtStart =
                (LaneOil.GetFrictionScale(12f + Step, settings)
                 - LaneOil.GetFrictionScale(12f, settings)) / Step;
            float slopeAtEnd =
                (LaneOil.GetFrictionScale(13.5f, settings)
                 - LaneOil.GetFrictionScale(13.5f - Step, settings)) / Step;

            Assert.Less(slopeAtStart, 0.05f, "オイル側の入り口で急に立ち上がっている");
            Assert.Less(slopeAtEnd, 0.05f, "乾いた側の出口で急に止まっている");
        }

        [Test]
        public void 奥へ行くほど摩擦は増えるだけで減らない()
        {
            LaneOilSettings settings = Standard();
            float previous = LaneOil.GetFrictionScale(0f, settings);

            for (float z = 0f; z <= 18f; z += 0.1f)
            {
                float current = LaneOil.GetFrictionScale(z, settings);

                Assert.GreaterOrEqual(current, previous - 0.0001f, $"z={z} で摩擦が減った");
                previous = current;
            }
        }

        // ---- 切ったとき ----

        [Test]
        public void オイルを切ると全域が乾いた扱いになる()
        {
            LaneOilSettings settings = Standard();
            settings.enabled = false;

            Assert.AreEqual(1f, LaneOil.GetFrictionScale(0f, settings), 0.0001f);
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(16.2f, settings), 0.0001f);
        }

        [Test]
        public void 既定の設定は全域が乾いている()
        {
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(0f, LaneOil.None), 0.0001f);
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(18f, LaneOil.None), 0.0001f);
        }

        // ---- おかしな値でも壊れない ----

        [Test]
        public void 移り変わりの長さが0でも段差だけで済む()
        {
            LaneOilSettings settings = Standard();
            settings.transitionLength = 0f;

            Assert.AreEqual(0.2f, LaneOil.GetFrictionScale(12f, settings), 0.0001f);
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(12.01f, settings), 0.0001f);
        }

        [Test]
        public void 負の係数は0として扱う()
        {
            LaneOilSettings settings = Standard();
            settings.oilFriction = -1f;

            Assert.AreEqual(0f, LaneOil.GetFrictionScale(0f, settings), 0.0001f);
        }

        [Test]
        public void レーンの外でも値を返す()
        {
            Assert.AreEqual(0.2f, LaneOil.GetFrictionScale(-5f, Standard()), 0.0001f);
            Assert.AreEqual(1f, LaneOil.GetFrictionScale(100f, Standard()), 0.0001f);
        }

        // ---- 乾き始めの位置 ----

        [Test]
        public void 乾き始めはオイルの終わりと移り変わりの和()
        {
            Assert.AreEqual(13.5f, LaneOil.GetDryStartZ(Standard()), 0.0001f);
        }

        [Test]
        public void オイルを切ると乾き始めは0になる()
        {
            LaneOilSettings settings = Standard();
            settings.enabled = false;

            Assert.AreEqual(0f, LaneOil.GetDryStartZ(settings), 0.0001f);
        }
    }
}
