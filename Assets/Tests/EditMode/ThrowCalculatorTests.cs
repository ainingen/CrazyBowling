using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Ball;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// ThrowCalculator の EditMode テスト。
    /// 投げる角度は廃止したので、左右はカーブで表す。
    /// </summary>
    public class ThrowCalculatorTests
    {
        /// <summary>Inspector の初期値と同じ設定。</summary>
        private static ThrowSettings CreateSettings()
        {
            return new ThrowSettings
            {
                maxDragPixels = 300f,
                minDragPixels = 20f,
                minThrowSpeed = 3f,
                maxThrowSpeed = 10f,
                maxCurvePixels = 200f,
                allowStraightThrow = true,
            };
        }

        /// <summary>手前にどれだけ引いたかだけを指定して投げる。</summary>
        private static ThrowResult ThrowWithPull(float pullPixels, bool curveToRight = false)
        {
            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(500f, 400f - pullPixels);
            return ThrowCalculator.Calculate(start, end, CreateSettings(), curveToRight);
        }

        /// <summary>引き幅とマウスの横移動量を指定して投げる。</summary>
        private static ThrowResult ThrowWith(float pullPixels, float sidePixels, bool curveToRight)
        {
            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(500f + sidePixels, 400f - pullPixels);
            return ThrowCalculator.Calculate(start, end, CreateSettings(), curveToRight);
        }

        // ---- 強さ（従来どおり） ----

        [Test]
        public void 引き幅が足りないと投げない()
        {
            Assert.IsFalse(ThrowWithPull(10f).isValid);
        }

        [Test]
        public void 最小の引き幅ちょうどなら投げられる()
        {
            Assert.IsTrue(ThrowWithPull(20f).isValid);
        }

        [Test]
        public void 奥に動かすと投げない()
        {
            Assert.IsFalse(ThrowWithPull(-100f).isValid);
        }

        [Test]
        public void 最大まで引くと最大初速になる()
        {
            Assert.AreEqual(10f, ThrowWithPull(300f).speed, 0.0001f);
        }

        [Test]
        public void 引きすぎても最大初速でクランプされる()
        {
            Assert.AreEqual(10f, ThrowWithPull(600f).speed, 0.0001f);
        }

        [Test]
        public void 半分引くと最小と最大の中間の初速になる()
        {
            Assert.AreEqual(6.5f, ThrowWithPull(150f).speed, 0.0001f);
        }

        [Test]
        public void 半分引くと引き幅の割合が0_5になる()
        {
            Assert.AreEqual(0.5f, ThrowWithPull(150f).pullRatio, 0.0001f);
        }

        [Test]
        public void 引きすぎても引き幅の割合は1でクランプされる()
        {
            Assert.AreEqual(1f, ThrowWithPull(600f).pullRatio, 0.0001f);
        }

        [Test]
        public void 投げない範囲でも引き幅の割合は入っている()
        {
            ThrowResult result = ThrowWithPull(10f);

            Assert.IsFalse(result.isValid);
            Assert.AreEqual(10f / 300f, result.pullRatio, 0.0001f);
        }

        [Test]
        public void 引く向きの反転を有効にすると奥に払って投げられる()
        {
            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(500f, 700f);
            ThrowResult result = ThrowCalculator.Calculate(start, end, CreateSettings(), false, invertPull: true);

            Assert.IsTrue(result.isValid);
            Assert.AreEqual(10f, result.speed, 0.0001f);
        }

        // ---- カーブ ----

        [Test]
        public void 横に動かさなければカーブ0()
        {
            Assert.AreEqual(0f, ThrowWith(300f, 0f, false).curve, 0.0001f);
        }

        [Test]
        public void 横に動かさずに離してもそのまま投げられる()
        {
            // 既定では、カーブ無しの投球はキャンセルされない
            ThrowResult result = ThrowWith(300f, 0f, false);

            Assert.IsTrue(result.isValid);
            Assert.AreEqual(10f, result.speed, 0.0001f);
        }

        [Test]
        public void カーブ必須にするとカーブ0では投げない()
        {
            ThrowSettings settings = CreateSettings();
            settings.allowStraightThrow = false;

            Vector2 start = new Vector2(500f, 400f);
            Vector2 end = new Vector2(500f, 100f);
            ThrowResult result = ThrowCalculator.Calculate(start, end, settings, false);

            Assert.IsFalse(result.isValid);
        }

        [Test]
        public void 左ボタンなら左へのカーブになる()
        {
            Assert.Less(ThrowWith(300f, 100f, false).curve, 0f);
        }

        [Test]
        public void 右ボタンなら右へのカーブになる()
        {
            Assert.Greater(ThrowWith(300f, 100f, true).curve, 0f);
        }

        [Test]
        public void 左右どちらに動かしてもカーブの強さは同じ()
        {
            // 向きはボタンで決まるので、動かす向きは強さにだけ効く
            float movedRight = ThrowWith(300f, 100f, true).curve;
            float movedLeft = ThrowWith(300f, -100f, true).curve;

            Assert.AreEqual(movedRight, movedLeft, 0.0001f);
        }

        [Test]
        public void 最大の横移動量でカーブが1になる()
        {
            Assert.AreEqual(1f, ThrowWith(300f, 200f, true).curve, 0.0001f);
            Assert.AreEqual(-1f, ThrowWith(300f, 200f, false).curve, 0.0001f);
        }

        [Test]
        public void 動かしすぎてもカーブは1でクランプされる()
        {
            Assert.AreEqual(1f, ThrowWith(300f, 500f, true).curve, 0.0001f);
            Assert.AreEqual(-1f, ThrowWith(300f, -500f, false).curve, 0.0001f);
        }

        [Test]
        public void 半分動かすとカーブが0_5になる()
        {
            Assert.AreEqual(0.5f, ThrowWith(300f, 100f, true).curve, 0.0001f);
        }

        [Test]
        public void 投げない範囲でもカーブの値は入っている()
        {
            ThrowResult result = ThrowWith(10f, 200f, true);

            Assert.IsFalse(result.isValid);
            Assert.AreEqual(1f, result.curve, 0.0001f);
        }

        [Test]
        public void カーブは引き幅に影響しない()
        {
            float withoutCurve = ThrowWith(150f, 0f, false).speed;
            float withCurve = ThrowWith(150f, 200f, true).speed;

            Assert.AreEqual(withoutCurve, withCurve, 0.0001f);
        }
    }
}
