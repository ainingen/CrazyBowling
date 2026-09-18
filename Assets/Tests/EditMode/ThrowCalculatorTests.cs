using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// ThrowCalculator の EditMode テスト。
    /// 引きベクトルの縦成分が強さ、横成分が角度になる。カーブは別に渡される。
    /// </summary>
    public class ThrowCalculatorTests
    {
        /// <summary>Inspector の初期値と同じ設定。</summary>
        private static ThrowSettings CreateSettings()
        {
            return new ThrowSettings
            {
                maxPullPixels = 300f,
                minPullPixels = 20f,
                maxAnglePixels = 250f,
                maxAngleDegrees = 4f,
                angleDeadZonePixels = 10f,
                minThrowSpeed = 3f,
                maxThrowSpeed = 10f,
            };
        }

        /// <summary>引き幅と横ずれを指定して投げる。</summary>
        private static ThrowResult ThrowWith(float pullPixels, float sidePixels, float curve = 0f)
        {
            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(500f + sidePixels, 400f - pullPixels);
            return ThrowCalculator.Calculate(start, end, CreateSettings(), curve);
        }

        // ---- 強さ ----

        [Test]
        public void 引き幅が足りないと投げない()
        {
            Assert.IsFalse(ThrowWith(10f, 0f).isValid);
        }

        [Test]
        public void 最小の引き幅ちょうどなら投げられる()
        {
            Assert.IsTrue(ThrowWith(20f, 0f).isValid);
        }

        [Test]
        public void 奥に動かすと投げない()
        {
            Assert.IsFalse(ThrowWith(-100f, 0f).isValid);
        }

        [Test]
        public void 最大まで引くと最大初速になる()
        {
            Assert.AreEqual(10f, ThrowWith(300f, 0f).speed, 0.0001f);
        }

        [Test]
        public void 引きすぎても最大初速でクランプされる()
        {
            Assert.AreEqual(10f, ThrowWith(600f, 0f).speed, 0.0001f);
        }

        [Test]
        public void 半分引くと最小と最大の中間の初速になる()
        {
            Assert.AreEqual(6.5f, ThrowWith(150f, 0f).speed, 0.0001f);
        }

        [Test]
        public void 半分引くと引き幅の割合が0_5になる()
        {
            Assert.AreEqual(0.5f, ThrowWith(150f, 0f).pullRatio, 0.0001f);
        }

        [Test]
        public void 投げない範囲でも引き幅の割合は入っている()
        {
            ThrowResult result = ThrowWith(10f, 0f);

            Assert.IsFalse(result.isValid);
            Assert.AreEqual(10f / 300f, result.pullRatio, 0.0001f);
        }

        [Test]
        public void 引く向きの反転を有効にすると奥に払って投げられる()
        {
            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(500f, 700f);
            ThrowResult result = ThrowCalculator.Calculate(start, end, CreateSettings(), 0f, invertPull: true);

            Assert.IsTrue(result.isValid);
            Assert.AreEqual(10f, result.speed, 0.0001f);
        }

        // ---- 角度 ----

        [Test]
        public void 真下に引くと角度は0になる()
        {
            Assert.AreEqual(0f, ThrowWith(300f, 0f).sideAngle, 0.0001f);
        }

        [Test]
        public void 右に引くと左向きの角度になる()
        {
            // 引いた向きの逆へ飛ぶ
            Assert.Less(ThrowWith(300f, 100f).sideAngle, 0f);
        }

        [Test]
        public void 左に引くと右向きの角度になる()
        {
            Assert.Greater(ThrowWith(300f, -100f).sideAngle, 0f);
        }

        [Test]
        public void デッドゾーンの中では角度が付かない()
        {
            Assert.AreEqual(0f, ThrowWith(300f, 10f).sideAngle, 0.0001f);
            Assert.AreEqual(0f, ThrowWith(300f, -10f).sideAngle, 0.0001f);
            Assert.AreEqual(0f, ThrowWith(300f, 5f).sideAngle, 0.0001f);
        }

        [Test]
        public void デッドゾーンを超えると角度が付き始める()
        {
            float angle = ThrowWith(300f, 11f).sideAngle;

            Assert.Less(angle, 0f);
            Assert.Greater(Mathf.Abs(angle), 0f);
            Assert.Less(Mathf.Abs(angle), 0.1f, "境界では角度はごく小さい");
        }

        [Test]
        public void 最大の横ずれでちょうど最大角度になる()
        {
            // デッドゾーンを差し引いて正規化しているので、250pxで4度に届く
            Assert.AreEqual(-4f, ThrowWith(300f, 250f).sideAngle, 0.0001f);
            Assert.AreEqual(4f, ThrowWith(300f, -250f).sideAngle, 0.0001f);
        }

        [Test]
        public void 横にずらしすぎても最大角度でクランプされる()
        {
            Assert.AreEqual(-4f, ThrowWith(300f, 600f).sideAngle, 0.0001f);
        }

        [Test]
        public void 角度は左右で対称になる()
        {
            Assert.AreEqual(
                -ThrowWith(300f, 120f).sideAngle,
                ThrowWith(300f, -120f).sideAngle,
                0.0001f);
        }

        [Test]
        public void 角度は引き幅に影響しない()
        {
            Assert.AreEqual(
                ThrowWith(150f, 0f).speed,
                ThrowWith(150f, 250f).speed,
                0.0001f);
        }

        [Test]
        public void 引き幅は角度に影響しない()
        {
            Assert.AreEqual(
                ThrowWith(50f, 120f).sideAngle,
                ThrowWith(300f, 120f).sideAngle,
                0.0001f);
        }

        [Test]
        public void 投げない範囲でも角度の値は入っている()
        {
            ThrowResult result = ThrowWith(10f, 250f);

            Assert.IsFalse(result.isValid);
            Assert.AreEqual(-4f, result.sideAngle, 0.0001f);
        }

        [Test]
        public void レーンごとに最大角度を広げられる()
        {
            ThrowSettings settings = CreateSettings();
            settings.maxAngleDegrees = 12f;

            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(750f, 100f);
            ThrowResult result = ThrowCalculator.Calculate(start, end, settings, 0f);

            Assert.AreEqual(-12f, result.sideAngle, 0.0001f);
        }

        // ---- カーブ ----

        [Test]
        public void カーブは渡した値がそのまま入る()
        {
            Assert.AreEqual(0.6f, ThrowWith(300f, 0f, 0.6f).curve, 0.0001f);
            Assert.AreEqual(-1f, ThrowWith(300f, 0f, -1f).curve, 0.0001f);
        }

        [Test]
        public void カーブは角度にも強さにも影響しない()
        {
            ThrowResult withoutCurve = ThrowWith(150f, 120f, 0f);
            ThrowResult withCurve = ThrowWith(150f, 120f, 1f);

            Assert.AreEqual(withoutCurve.speed, withCurve.speed, 0.0001f);
            Assert.AreEqual(withoutCurve.sideAngle, withCurve.sideAngle, 0.0001f);
        }

        [Test]
        public void 投げない範囲でもカーブの値は入っている()
        {
            ThrowResult result = ThrowWith(10f, 0f, 0.8f);

            Assert.IsFalse(result.isValid);
            Assert.AreEqual(0.8f, result.curve, 0.0001f);
        }
    }
}
