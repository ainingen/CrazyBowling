using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// SpinningDiscField の EditMode テスト。
    ///
    /// 確かめたいのは「流れる量が、円盤のどこを通したかだけで決まる」こと。
    /// ボールの速さで変わってしまうと、強く投げるほど曲がらなくなって
    /// 狙って通す楽しみが無くなるため。
    /// </summary>
    public class SpinningDiscFieldTests
    {
        /// <summary>SPINNING DISC の初期値と同じ設定。</summary>
        private static SpinningDiscSettings Standard()
        {
            return new SpinningDiscSettings
            {
                radius = 0.5f,
                period = 1f,
                driftGain = 0.075f,
                edgeSoftness = 0.05f,
            };
        }

        /// <summary>外周の弱まりを外した設定。見積もりとの突き合わせに使う。</summary>
        private static SpinningDiscSettings Sharp()
        {
            SpinningDiscSettings settings = Standard();
            settings.edgeSoftness = 0f;
            return settings;
        }

        /// <summary>
        /// 円盤をまっすぐ通り抜けさせて、横に付いた速度を実測する。
        /// 細かく刻んで加速度を積み上げる。
        /// </summary>
        /// <param name="offsetX">通り道と円盤の中心との横のずれ（m）。</param>
        /// <param name="speed">進む速さ（m/秒）。</param>
        private static float SimulateDrift(float offsetX, float speed, SpinningDiscSettings settings)
        {
            const float Dt = 0.0002f;

            Vector3 center = Vector3.zero;
            float goal = settings.radius * 2f;

            Vector3 position = new Vector3(offsetX, 0f, -goal);
            Vector3 velocity = new Vector3(0f, 0f, speed);

            while (position.z < goal)
            {
                if (SpinningDiscField.TryCalculateDrift(
                        center, position, velocity, settings, out Vector3 acceleration))
                {
                    velocity += acceleration * Dt;
                }

                position += velocity * Dt;
            }

            return velocity.x;
        }

        // ---- どこまで効くか ----

        [Test]
        public void 円盤の外では効かない()
        {
            bool hit = SpinningDiscField.TryCalculateDrift(
                Vector3.zero,
                new Vector3(0.8f, 0f, 0f),
                new Vector3(0f, 0f, 8f),
                Standard(),
                out Vector3 acceleration);

            Assert.IsFalse(hit);
            Assert.AreEqual(Vector3.zero, acceleration);
        }

        [Test]
        public void 円盤の中では効く()
        {
            bool hit = SpinningDiscField.TryCalculateDrift(
                Vector3.zero,
                Vector3.zero,
                new Vector3(0f, 0f, 8f),
                Standard(),
                out Vector3 acceleration);

            Assert.IsTrue(hit);
            Assert.Greater(acceleration.magnitude, 0f);
        }

        [Test]
        public void 高さが違っても効き方は変わらない()
        {
            // 円盤は床と面一なので、ボールが浮いていても沈んでいても同じに扱う
            SpinningDiscField.TryCalculateDrift(
                Vector3.zero, new Vector3(0.1f, 0f, 0f),
                new Vector3(0f, 0f, 8f), Standard(), out Vector3 onFloor);

            SpinningDiscField.TryCalculateDrift(
                Vector3.zero, new Vector3(0.1f, 0.5f, 0f),
                new Vector3(0f, 0f, 8f), Standard(), out Vector3 lifted);

            Assert.AreEqual(onFloor.x, lifted.x, 0.0001f);
            Assert.AreEqual(onFloor.z, lifted.z, 0.0001f);
        }

        [Test]
        public void 外周では効きが弱まる()
        {
            SpinningDiscSettings settings = Standard();

            Assert.AreEqual(1f, SpinningDiscField.GetInfluence(0f, settings), 0.0001f);
            Assert.AreEqual(1f, SpinningDiscField.GetInfluence(0.44f, settings), 0.0001f);
            Assert.AreEqual(0.5f, SpinningDiscField.GetInfluence(0.475f, settings), 0.0001f);
            Assert.AreEqual(0f, SpinningDiscField.GetInfluence(0.5f, settings), 0.0001f);
            Assert.AreEqual(0f, SpinningDiscField.GetInfluence(0.8f, settings), 0.0001f);
        }

        [Test]
        public void 効きは境界へ向けて滑らかに落ちる()
        {
            // 急に効き始めると、ボールが折れ線に曲がって見える
            SpinningDiscSettings settings = Standard();
            const float Step = 0.001f;
            float previous = SpinningDiscField.GetInfluence(0.44f, settings);

            for (float d = 0.44f; d <= 0.5f; d += Step)
            {
                float influence = SpinningDiscField.GetInfluence(d, settings);

                Assert.LessOrEqual(influence, previous + 0.0001f, $"d={d} で効きが増えた");
                Assert.Less(Mathf.Abs(influence - previous), 0.05f, $"d={d} で段差ができた");
                previous = influence;
            }

            Assert.AreEqual(0f, previous, 0.05f, "境界で0に落ち切っていない");
        }

        [Test]
        public void なめらかにする幅が0なら中は一律で効く()
        {
            SpinningDiscSettings settings = Sharp();

            Assert.AreEqual(1f, SpinningDiscField.GetInfluence(0f, settings), 0.0001f);
            Assert.AreEqual(1f, SpinningDiscField.GetInfluence(0.499f, settings), 0.0001f);
            Assert.AreEqual(0f, SpinningDiscField.GetInfluence(0.5f, settings), 0.0001f);
        }

        // ---- 力の向き ----

        [Test]
        public void 力は進行方向と直交する()
        {
            // 斜めに入っても進行方向に対して横向きでなければ、速さそのものが変わってしまい、
            // 流れる量がボールの速さに左右されるようになる
            Vector3 velocity = new Vector3(3f, 0f, 7f);

            SpinningDiscField.TryCalculateDrift(
                Vector3.zero, new Vector3(0.1f, 0f, 0.1f),
                velocity, Standard(), out Vector3 acceleration);

            Assert.AreEqual(0f, Vector3.Dot(acceleration, velocity), 0.0001f);
        }

        [Test]
        public void 逆回りなら逆へ流れる()
        {
            SpinningDiscSettings reversed = Standard();
            reversed.period = -1f;

            SpinningDiscField.TryCalculateDrift(
                Vector3.zero, Vector3.zero,
                new Vector3(0f, 0f, 8f), Standard(), out Vector3 forward);

            SpinningDiscField.TryCalculateDrift(
                Vector3.zero, Vector3.zero,
                new Vector3(0f, 0f, 8f), reversed, out Vector3 backward);

            Assert.AreEqual(-forward.x, backward.x, 0.0001f);
            Assert.Greater(Mathf.Abs(forward.x), 0f);
        }

        // ---- 効かない場合 ----

        [Test]
        public void 効き具合が0なら流れない()
        {
            SpinningDiscSettings settings = Standard();
            settings.driftGain = 0f;

            Assert.IsFalse(SpinningDiscField.TryCalculateDrift(
                Vector3.zero, Vector3.zero,
                new Vector3(0f, 0f, 8f), settings, out _));
        }

        [Test]
        public void 回っていなければ流れない()
        {
            SpinningDiscSettings settings = Standard();
            settings.period = 0f;

            Assert.IsFalse(SpinningDiscField.TryCalculateDrift(
                Vector3.zero, Vector3.zero,
                new Vector3(0f, 0f, 8f), settings, out _));
        }

        [Test]
        public void 止まっているボールには効かない()
        {
            Assert.IsFalse(SpinningDiscField.TryCalculateDrift(
                Vector3.zero, Vector3.zero,
                new Vector3(0f, 0f, 0.001f), Standard(), out _));
        }

        // ---- 横切る長さ ----

        [Test]
        public void 弦の長さは中心で直径になる()
        {
            Assert.AreEqual(1f, SpinningDiscField.GetChordLength(0f, Standard()), 0.0001f);
        }

        [Test]
        public void 弦の長さは端でほぼ0になる()
        {
            // 端をかすめただけなら、ほとんど円盤の上を通っていないので流れない
            float nearEdge = SpinningDiscField.GetChordLength(0.4999f, Standard());

            Assert.Greater(nearEdge, 0f, "まだ円盤の内側なのに0になった");
            Assert.Less(nearEdge, 0.05f, "端なのに長さが残りすぎている");
        }

        [Test]
        public void 弦の長さは円盤の外では0になる()
        {
            Assert.AreEqual(0f, SpinningDiscField.GetChordLength(0.5f, Standard()), 0.0001f);
            Assert.AreEqual(0f, SpinningDiscField.GetChordLength(0.8f, Standard()), 0.0001f);
        }

        // ---- 流れる量 ----

        [Test]
        public void 初期値で真ん中を通すと横に約047流れる()
        {
            float chord = SpinningDiscField.GetChordLength(0f, Standard());

            Assert.AreEqual(0.47f, SpinningDiscField.EstimateDriftSpeed(chord, Standard()), 0.01f);
        }

        [Test]
        public void 積み上げた流れる速さが見積もりと一致する()
        {
            SpinningDiscSettings settings = Sharp();
            float expected = SpinningDiscField.EstimateDriftSpeed(
                SpinningDiscField.GetChordLength(0f, settings), settings);

            Assert.AreEqual(expected, SimulateDrift(0f, 8f, settings), 0.01f);
        }

        [Test]
        public void 流れる量はボールの速さに左右されない()
        {
            // 速い球は円盤の上にいる時間が短く、遅い球は長いので釣り合う
            SpinningDiscSettings settings = Sharp();

            Assert.AreEqual(
                SimulateDrift(0f, 10f, settings),
                SimulateDrift(0f, 5f, settings),
                0.01f);
        }

        [Test]
        public void 中心を通すほど大きく流れる()
        {
            SpinningDiscSettings settings = Sharp();

            float center = SimulateDrift(0f, 8f, settings);
            float middle = SimulateDrift(0.25f, 8f, settings);
            float edge = SimulateDrift(0.45f, 8f, settings);

            Assert.Greater(center, middle);
            Assert.Greater(middle, edge);
            Assert.Greater(edge, 0f);
        }
    }
}
