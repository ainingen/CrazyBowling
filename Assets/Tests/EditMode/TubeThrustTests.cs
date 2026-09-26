using NUnit.Framework;
using CrazyBowling.Lanes;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// TubeThrust の EditMode テスト。
    ///
    /// 確かめたいのは「止まりかけの遅い球に強く効き、速い球には効かない」ことと、
    /// 「筒の後半の区間の外では効かない」こと（壁を駆け上がる見せ場を残すため）。
    /// </summary>
    public class TubeThrustTests
    {
        /// <summary>7本目の設定に近い値。</summary>
        private static TubeThrustSettings Standard()
        {
            return new TubeThrustSettings
            {
                startZ = 6.9f,
                rampLength = 0.2f,
                endZ = 9f,
                acceleration = 3f,
                fadeSpeed = 2.5f,
            };
        }

        [Test]
        public void 止まっている球には全力で効く()
        {
            Assert.That(TubeThrust.CalculateAcceleration(8f, 0f, Standard()), Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void 逆走している球にも全力で効く()
        {
            Assert.That(TubeThrust.CalculateAcceleration(8f, -1.5f, Standard()), Is.EqualTo(3f).Within(1e-5f));
        }

        [Test]
        public void 前向きに速いほど弱くなる()
        {
            float slow = TubeThrust.CalculateAcceleration(8f, 0.5f, Standard());
            float faster = TubeThrust.CalculateAcceleration(8f, 1.5f, Standard());
            Assert.That(faster, Is.LessThan(slow));
            Assert.That(TubeThrust.CalculateAcceleration(8f, 1.25f, Standard()), Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void 効かなくなる速さ以上の球には効かない()
        {
            Assert.That(TubeThrust.CalculateAcceleration(8f, 2.5f, Standard()), Is.EqualTo(0f));
            Assert.That(TubeThrust.CalculateAcceleration(8f, 7f, Standard()), Is.EqualTo(0f));
        }

        [Test]
        public void 区間の外では効かない()
        {
            Assert.That(TubeThrust.CalculateAcceleration(6.5f, 0f, Standard()), Is.EqualTo(0f));
            Assert.That(TubeThrust.CalculateAcceleration(9.5f, 0f, Standard()), Is.EqualTo(0f));
        }

        [Test]
        public void 入口では少しずつ効き始める()
        {
            Assert.That(TubeThrust.CalculateAcceleration(6.9f, 0f, Standard()), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(TubeThrust.CalculateAcceleration(7.0f, 0f, Standard()), Is.EqualTo(1.5f).Within(1e-4f));
            Assert.That(TubeThrust.CalculateAcceleration(7.1f, 0f, Standard()), Is.EqualTo(3f).Within(1e-4f));
        }

        [Test]
        public void 加速度が0なら効かない()
        {
            TubeThrustSettings settings = Standard();
            settings.acceleration = 0f;
            Assert.That(TubeThrust.CalculateAcceleration(8f, 0f, settings), Is.EqualTo(0f));
        }
    }
}
