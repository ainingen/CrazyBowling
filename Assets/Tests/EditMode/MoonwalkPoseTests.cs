using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// MoonwalkPose の EditMode テスト。
    /// 「進んだ距離で歩く」「進行方向の逆を向く」の2つを確かめる。
    /// </summary>
    public class MoonwalkPoseTests
    {
        private const float Stride = 0.16f;

        // ---- 歩きの周期 ----

        [Test]
        public void 一歩ぶん進むと半周する()
        {
            Assert.AreEqual(0.5f, MoonwalkPose.GetPhase(Stride, Stride), 0.0001f);
        }

        [Test]
        public void 二歩ぶん進むと一周して元に戻る()
        {
            Assert.AreEqual(0f, MoonwalkPose.GetPhase(Stride * 2f, Stride), 0.0001f);
        }

        [Test]
        public void 止まっていれば周期も進まない()
        {
            float phase = MoonwalkPose.GetPhase(1.2f, Stride);

            Assert.AreEqual(phase, MoonwalkPose.GetPhase(1.2f, Stride), 0.0001f);
        }

        [Test]
        public void 一歩の長さが0でも壊れない()
        {
            Assert.AreEqual(0f, MoonwalkPose.GetPhase(5f, 0f), 0.0001f);
        }

        [Test]
        public void 周期は0から1に収まる()
        {
            for (float d = 0f; d < 10f; d += 0.013f)
            {
                float phase = MoonwalkPose.GetPhase(d, Stride);

                Assert.GreaterOrEqual(phase, 0f);
                Assert.Less(phase, 1f);
            }
        }

        // ---- 足 ----

        [Test]
        public void 左右の足は半周ずれている()
        {
            Assert.AreEqual(
                MoonwalkPose.GetThighAngle(0.25f, 30f, false),
                MoonwalkPose.GetThighAngle(0.75f, 30f, true),
                0.0001f);
        }

        [Test]
        public void 左右の足が同時に同じ側へ出ることはない()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                float left = MoonwalkPose.GetThighAngle(phase, 30f, false);
                float right = MoonwalkPose.GetThighAngle(phase, 30f, true);

                Assert.LessOrEqual(left * right, 0.0001f, $"phase={phase} で両足が同じ側にある");
            }
        }

        [Test]
        public void 太ももは振り幅を超えない()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                Assert.LessOrEqual(Mathf.Abs(MoonwalkPose.GetThighAngle(phase, 30f, false)), 30.0001f);
            }
        }

        // ---- 膝 ----

        [Test]
        public void 膝は逆には曲がらない()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                Assert.GreaterOrEqual(MoonwalkPose.GetKneeAngle(phase, 40f, false), 0f, $"phase={phase} で膝が逆に曲がった");
                Assert.GreaterOrEqual(MoonwalkPose.GetKneeAngle(phase, 40f, true), 0f, $"phase={phase} で膝が逆に曲がった");
            }
        }

        [Test]
        public void 膝は曲げ幅を超えない()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                Assert.LessOrEqual(MoonwalkPose.GetKneeAngle(phase, 40f, false), 40.0001f);
            }
        }

        [Test]
        public void 足が一番後ろにあるとき膝は伸びている()
        {
            // 太ももが一番後ろに来る位置
            float phase = 0.75f;

            Assert.AreEqual(-34f, MoonwalkPose.GetThighAngle(phase, 34f, false), 0.01f);
            Assert.AreEqual(0f, MoonwalkPose.GetKneeAngle(phase, 40f, false), 0.01f);
        }

        // ---- 腕 ----

        [Test]
        public void 右腕は左足と一緒に前へ出る()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                float leftThigh = MoonwalkPose.GetThighAngle(phase, 30f, false);
                float rightArm = MoonwalkPose.GetArmAngle(phase, 30f, true);

                Assert.GreaterOrEqual(leftThigh * rightArm, -0.0001f, $"phase={phase} で腕と足が逆になった");
            }
        }

        [Test]
        public void 腕は同じ側の足と逆に振れる()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                float leftThigh = MoonwalkPose.GetThighAngle(phase, 30f, false);
                float leftArm = MoonwalkPose.GetArmAngle(phase, 30f, false);

                Assert.LessOrEqual(leftThigh * leftArm, 0.0001f, $"phase={phase} で腕と足が同じ側にある");
            }
        }

        // ---- 腰の上下 ----

        [Test]
        public void 腰は沈むだけで浮き上がらない()
        {
            for (float phase = 0f; phase < 1f; phase += 0.01f)
            {
                Assert.LessOrEqual(MoonwalkPose.GetBob(phase, 0.02f), 0.0001f, $"phase={phase} で浮いた");
            }
        }

        [Test]
        public void 腰は一周に二回沈む()
        {
            // 足が地面を蹴るのは一周に二回なので、沈むのも二回
            Assert.AreEqual(0f, MoonwalkPose.GetBob(0f, 0.02f), 0.0001f);
            Assert.AreEqual(-0.02f, MoonwalkPose.GetBob(0.25f, 0.02f), 0.0001f);
            Assert.AreEqual(0f, MoonwalkPose.GetBob(0.5f, 0.02f), 0.0001f);
            Assert.AreEqual(-0.02f, MoonwalkPose.GetBob(0.75f, 0.02f), 0.0001f);
        }

        // ---- 向き ----

        [Test]
        public void 右へ動くときは左を向く()
        {
            Assert.AreEqual(-1f, MoonwalkPose.GetFacingSign(0.8f, 1f, 0.1f), 0.0001f);
        }

        [Test]
        public void 左へ動くときは右を向く()
        {
            Assert.AreEqual(1f, MoonwalkPose.GetFacingSign(-0.8f, -1f, 0.1f), 0.0001f);
        }

        [Test]
        public void 端で止まりかけているときは向きを変えない()
        {
            // 折り返しで速度が0を通るので、そこで向きが暴れないこと
            Assert.AreEqual(1f, MoonwalkPose.GetFacingSign(0.02f, 1f, 0.1f), 0.0001f);
            Assert.AreEqual(-1f, MoonwalkPose.GetFacingSign(-0.02f, -1f, 0.1f), 0.0001f);
            Assert.AreEqual(-1f, MoonwalkPose.GetFacingSign(0f, -1f, 0.1f), 0.0001f);
        }

        [Test]
        public void 向きは必ず1かマイナス1になる()
        {
            foreach (float v in new[] { -2f, -0.05f, 0f, 0.05f, 2f })
            {
                float sign = MoonwalkPose.GetFacingSign(v, 1f, 0.1f);

                Assert.AreEqual(1f, Mathf.Abs(sign), 0.0001f, $"速さ{v}で1でも−1でもない値になった");
            }
        }

        [Test]
        public void 折り返すと向きも入れ替わる()
        {
            float sign = 1f;
            sign = MoonwalkPose.GetFacingSign(0.8f, sign, 0.1f);    // 右へ動く → 左を向く
            sign = MoonwalkPose.GetFacingSign(0.0f, sign, 0.1f);    // 端で止まる → そのまま
            Assert.AreEqual(-1f, sign, 0.0001f);

            sign = MoonwalkPose.GetFacingSign(-0.8f, sign, 0.1f);   // 左へ動く → 右を向く
            Assert.AreEqual(1f, sign, 0.0001f);
        }
    }
}
