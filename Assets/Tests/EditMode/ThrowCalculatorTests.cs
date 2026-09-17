using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// ThrowCalculator の EditMode テスト。
    /// スクリーン座標は画面下が y=0、上に行くほど y が大きい。
    /// </summary>
    public class ThrowCalculatorTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>Inspector の仮の初期値と同じ設定。</summary>
        private static ThrowSettings CreateSettings()
        {
            return new ThrowSettings
            {
                maxDragPixels = 300f,
                minDragPixels = 20f,
                minThrowSpeed = 3f,
                maxThrowSpeed = 10f,
                maxSideAngle = 20f,
            };
        }

        [Test]
        public void 引き幅が足りないと投げない()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 390f), CreateSettings());

            Assert.IsFalse(result.isValid);
            Assert.That(result.speed, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void 奥に動かすと投げない()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 700f), CreateSettings());

            Assert.IsFalse(result.isValid);
        }

        [Test]
        public void 最大まで引くと最大初速になる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 100f), CreateSettings());

            Assert.IsTrue(result.isValid);
            Assert.That(result.speed, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void 半分引くと最小と最大の中間の初速になる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 250f), CreateSettings());

            // 3 + (10 - 3) * 0.5 = 6.5
            Assert.That(result.speed, Is.EqualTo(6.5f).Within(Tolerance));
        }

        [Test]
        public void 引きすぎても最大初速でクランプされる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, -200f), CreateSettings());

            Assert.That(result.speed, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void 真下に引くと角度は0になる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 100f), CreateSettings());

            Assert.That(result.sideAngle, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void 右に引くと左向きの角度になる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(650f, 100f), CreateSettings());

            // 右へ150px（最大の半分）ずらしたので、逆向きに 20 度の半分
            Assert.That(result.sideAngle, Is.EqualTo(-10f).Within(Tolerance));
        }

        [Test]
        public void 左に引くと右向きの角度になる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(350f, 100f), CreateSettings());

            Assert.That(result.sideAngle, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void 左右にずらしすぎても最大角度でクランプされる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(1500f, 100f), CreateSettings());

            Assert.That(result.sideAngle, Is.EqualTo(-20f).Within(Tolerance));
        }

        [Test]
        public void 左右反転を有効にすると角度の符号が入れ替わる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(650f, 100f), CreateSettings(),
                invertPull: false, invertSide: true);

            Assert.That(result.sideAngle, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void 引く向きの反転を有効にすると奥に払って投げられる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 700f), CreateSettings(),
                invertPull: true, invertSide: false);

            Assert.IsTrue(result.isValid);
            Assert.That(result.speed, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void 最小の引き幅ちょうどなら投げられる()
        {
            ThrowResult result = ThrowCalculator.Calculate(
                new Vector2(500f, 400f), new Vector2(500f, 380f), CreateSettings());

            Assert.IsTrue(result.isValid);
        }
    }
}
