using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// ObstacleMotion の EditMode テスト。
    /// 動く障害物が、端で止まってから折り返すことを確かめる。
    /// </summary>
    public class ObstacleMotionTests
    {
        /// <summary>MOVING WALL の初期値と同じ設定。</summary>
        private static ObstacleMotionSettings Standard()
        {
            return new ObstacleMotionSettings
            {
                travel = 0.35f,
                period = 2.5f,
                phase = 0f,
            };
        }

        // ---- 往復 ----

        [Test]
        public void 始まりは中心にいる()
        {
            Assert.AreEqual(0f, ObstacleMotion.GetOffset(0f, Standard()), 0.0001f);
        }

        [Test]
        public void 四分の一で片端に届く()
        {
            Assert.AreEqual(0.35f, ObstacleMotion.GetOffset(2.5f * 0.25f, Standard()), 0.0001f);
        }

        [Test]
        public void 四分の三で反対の端に届く()
        {
            Assert.AreEqual(-0.35f, ObstacleMotion.GetOffset(2.5f * 0.75f, Standard()), 0.0001f);
        }

        [Test]
        public void 一往復すると元の位置に戻る()
        {
            Assert.AreEqual(
                ObstacleMotion.GetOffset(0.4f, Standard()),
                ObstacleMotion.GetOffset(0.4f + 2.5f, Standard()),
                0.0001f);
        }

        [Test]
        public void 振れ幅を超えて外へは出ない()
        {
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                float offset = ObstacleMotion.GetOffset(t, Standard());

                Assert.LessOrEqual(Mathf.Abs(offset), 0.35f + 0.0001f, $"t={t} で外へ出た");
            }
        }

        // ---- 折り返し方 ----

        [Test]
        public void 端では速度が0になる()
        {
            // 一定速度で往復させると、端で速度が一瞬で反転してボールを弾き飛ばす
            ObstacleMotionSettings settings = Standard();
            const float Step = 0.001f;
            float peak = 2.5f * 0.25f;

            float speed =
                Mathf.Abs(ObstacleMotion.GetOffset(peak + Step, settings)
                          - ObstacleMotion.GetOffset(peak - Step, settings)) / (2f * Step);

            Assert.Less(speed, 0.01f, "端で止まらずに折り返している");
        }

        [Test]
        public void 一番速いのは中心を通るとき()
        {
            ObstacleMotionSettings settings = Standard();
            const float Step = 0.001f;

            float atCenter =
                Mathf.Abs(ObstacleMotion.GetOffset(Step, settings)
                          - ObstacleMotion.GetOffset(-Step, settings)) / (2f * Step);

            Assert.AreEqual(ObstacleMotion.GetPeakSpeed(settings), atCenter, 0.01f);
        }

        [Test]
        public void 一番速いときの速さは振れ幅と周期から決まる()
        {
            // 0.35m を 2.5秒で往復 → 中心で約0.88 m/秒
            Assert.AreEqual(0.88f, ObstacleMotion.GetPeakSpeed(Standard()), 0.01f);
        }

        [Test]
        public void 周期が短いほど速くなる()
        {
            ObstacleMotionSettings fast = Standard();
            fast.period = 1.25f;

            Assert.Greater(ObstacleMotion.GetPeakSpeed(fast), ObstacleMotion.GetPeakSpeed(Standard()));
        }

        // ---- ずらし ----

        [Test]
        public void ずらし四分の一なら端から始まる()
        {
            ObstacleMotionSettings settings = Standard();
            settings.phase = 0.25f;

            Assert.AreEqual(0.35f, ObstacleMotion.GetOffset(0f, settings), 0.0001f);
        }

        [Test]
        public void ずらし半分なら向きが逆になる()
        {
            ObstacleMotionSettings settings = Standard();
            settings.phase = 0.5f;

            Assert.AreEqual(
                -ObstacleMotion.GetOffset(0.4f, Standard()),
                ObstacleMotion.GetOffset(0.4f, settings),
                0.0001f);
        }

        // ---- 止めたとき ----

        [Test]
        public void 周期が0なら動かない()
        {
            ObstacleMotionSettings settings = Standard();
            settings.period = 0f;

            Assert.AreEqual(0f, ObstacleMotion.GetOffset(3f, settings), 0.0001f);
            Assert.AreEqual(0f, ObstacleMotion.GetPeakSpeed(settings), 0.0001f);
        }

        [Test]
        public void 振れ幅が0なら動かない()
        {
            ObstacleMotionSettings settings = Standard();
            settings.travel = 0f;

            Assert.AreEqual(0f, ObstacleMotion.GetOffset(3f, settings), 0.0001f);
        }

        [Test]
        public void 負の周期でも壊れない()
        {
            ObstacleMotionSettings settings = Standard();
            settings.period = -1f;

            Assert.AreEqual(0f, ObstacleMotion.GetOffset(3f, settings), 0.0001f);
        }

        // ---- 波そのもの ----

        [Test]
        public void 波は1とマイナス1の間に収まる()
        {
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                float wave = ObstacleMotion.GetWave(t, 2.5f, 0f);

                Assert.LessOrEqual(Mathf.Abs(wave), 1f + 0.0001f, $"t={t} で1を超えた");
            }
        }

        // ---- 回転 ----

        [Test]
        public void 回転は0度から始まる()
        {
            Assert.AreEqual(0f, ObstacleMotion.GetAngle(0f, 1f, 0f), 0.0001f);
        }

        [Test]
        public void 半周で180度になる()
        {
            Assert.AreEqual(180f, ObstacleMotion.GetAngle(0.5f, 1f, 0f), 0.0001f);
        }

        [Test]
        public void 一周すると角度が一巡する()
        {
            Assert.AreEqual(
                0f,
                Mathf.DeltaAngle(
                    ObstacleMotion.GetAngle(0.3f, 1f, 0f),
                    ObstacleMotion.GetAngle(0.3f + 1f, 1f, 0f)),
                0.001f);
        }

        [Test]
        public void 角度は0から360の間に収まる()
        {
            for (float t = 0f; t < 10f; t += 0.01f)
            {
                float angle = ObstacleMotion.GetAngle(t, 1f, 0.3f);

                Assert.GreaterOrEqual(angle, 0f, $"t={t} で負になった");
                Assert.Less(angle, 360f, $"t={t} で360以上になった");
            }
        }

        [Test]
        public void 負の周期は逆回りになる()
        {
            // 往復と違い、回転には向きの区別があるので、負は「止まっている」ではなく逆回り
            Assert.AreEqual(
                -Mathf.DeltaAngle(0f, ObstacleMotion.GetAngle(0.25f, 1f, 0f)),
                Mathf.DeltaAngle(0f, ObstacleMotion.GetAngle(0.25f, -1f, 0f)),
                0.001f);
        }

        [Test]
        public void 周期が0なら回らない()
        {
            Assert.AreEqual(0f, ObstacleMotion.GetAngle(3f, 0f, 0f), 0.0001f);
            Assert.AreEqual(0f, ObstacleMotion.GetAngularSpeed(0f), 0.0001f);
        }

        [Test]
        public void 一周1秒なら回る速さは約6283ミリラジアン毎秒()
        {
            Assert.AreEqual(6.283f, ObstacleMotion.GetAngularSpeed(1f), 0.001f);
        }

        [Test]
        public void 負の周期なら回る速さも負になる()
        {
            Assert.AreEqual(
                -ObstacleMotion.GetAngularSpeed(1f),
                ObstacleMotion.GetAngularSpeed(-1f),
                0.0001f);
        }
    }
}
