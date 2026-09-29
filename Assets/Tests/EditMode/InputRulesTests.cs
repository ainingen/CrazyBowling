using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Ball;
using CrazyBowling.UI;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>操作の直し（段階6）：強さを画面の高さの割合に・TAP の案内・カーブの吸い付き。</summary>
    public class InputRulesTests
    {
        private static ThrowSettings Reference => new ThrowSettings
        {
            maxPullPixels = 300f,
            minPullPixels = 20f,
            maxAnglePixels = 250f,
            maxAngleDegrees = 2f,
            angleDeadZonePixels = 10f,
            minThrowSpeed = 3f,
            maxThrowSpeed = 10f,
        };

        [Test]
        public void 高さ1080では直す前と同じ値のまま()
        {
            ThrowSettings s = ThrowCalculator.ScaleForScreen(Reference, 1080f);
            Assert.That(s.maxPullPixels, Is.EqualTo(300f));
            Assert.That(s.minPullPixels, Is.EqualTo(20f));
            Assert.That(s.maxAnglePixels, Is.EqualTo(250f));
            Assert.That(s.angleDeadZonePixels, Is.EqualTo(10f));
            Assert.That(s.minThrowSpeed, Is.EqualTo(3f));
            Assert.That(s.maxThrowSpeed, Is.EqualTo(10f));
            Assert.That(s.maxAngleDegrees, Is.EqualTo(2f));
        }

        [Test]
        public void 高さ1080では同じ引き方で初速と角度が直す前と同じ()
        {
            ThrowSettings scaled = ThrowCalculator.ScaleForScreen(Reference, 1080f);
            Vector2 start = new Vector2(900f, 700f);
            foreach (Vector2 end in new[] { new Vector2(900f, 400f), new Vector2(960f, 550f), new Vector2(700f, 680f), new Vector2(905f, 690f), new Vector2(1300f, 100f) })
            {
                ThrowResult before = ThrowCalculator.Calculate(start, end, Reference, 0.3f);
                ThrowResult after = ThrowCalculator.Calculate(start, end, scaled, 0.3f);
                Assert.That(after.isValid, Is.EqualTo(before.isValid));
                Assert.That(after.speed, Is.EqualTo(before.speed));
                Assert.That(after.sideAngle, Is.EqualTo(before.sideAngle));
                Assert.That(after.pullRatio, Is.EqualTo(before.pullRatio));
            }
        }

        [Test]
        public void 画面の高さが違っても同じ割合を引けば同じ強さと角度()
        {
            ThrowResult at1080 = ThrowCalculator.Calculate(Vector2.zero, new Vector2(-120f, -200f), ThrowCalculator.ScaleForScreen(Reference, 1080f), 0f);
            foreach (float h in new[] { 720f, 1440f, 2160f, 600f })
            {
                float k = h / 1080f;
                ThrowResult r = ThrowCalculator.Calculate(Vector2.zero, new Vector2(-120f * k, -200f * k), ThrowCalculator.ScaleForScreen(Reference, h), 0f);
                Assert.That(r.speed, Is.EqualTo(at1080.speed).Within(1e-4f), $"高さ {h}");
                Assert.That(r.sideAngle, Is.EqualTo(at1080.sideAngle).Within(1e-4f), $"高さ {h}");
            }
        }

        [Test]
        public void 高さ720では200ピクセルで最大の強さ()
        {
            ThrowResult r = ThrowCalculator.Calculate(Vector2.zero, new Vector2(0f, -200f), ThrowCalculator.ScaleForScreen(Reference, 720f), 0f);
            Assert.That(r.speed, Is.EqualTo(10f).Within(1e-4f));
            ThrowResult tooShort = ThrowCalculator.Calculate(Vector2.zero, new Vector2(0f, -13f), ThrowCalculator.ScaleForScreen(Reference, 720f), 0f);
            Assert.That(tooShort.isValid, Is.False);
        }

        [Test]
        public void スマホとタブレットではTAP()
        {
            Assert.That(InputHints.UseTap(true, true, false), Is.True);
            Assert.That(InputHints.UseTap(true, false, false), Is.True);
            Assert.That(InputHints.UseTap(false, true, false), Is.True);
        }

        [Test]
        public void マウスのあるパソコンではCLICK()
        {
            Assert.That(InputHints.UseTap(false, false, true), Is.False);
            Assert.That(InputHints.UseTap(false, true, true), Is.False);
            Assert.That(InputHints.Choose("CLICK TO START", "TAP TO START", false), Is.EqualTo("CLICK TO START"));
            Assert.That(InputHints.Choose("CLICK TO START", "TAP TO START", true), Is.EqualTo("TAP TO START"));
        }

        [Test]
        public void 真ん中付近で離すとSTRAIGHTに吸い付く()
        {
            Assert.That(CurveSnap.Apply(0.04f, 0.05f), Is.EqualTo(0f));
            Assert.That(CurveSnap.Apply(-0.049f, 0.05f), Is.EqualTo(0f));
            Assert.That(CurveSnap.Apply(0.06f, 0.05f), Is.EqualTo(0.06f));
            Assert.That(CurveSnap.Apply(-0.5f, 0.05f), Is.EqualTo(-0.5f));
            Assert.That(CurveSnap.Apply(0.04f, 0f), Is.EqualTo(0.04f));
        }

        [Test]
        public void 立ち位置のゲージは左右いっぱいで範囲の端()
        {
            Assert.That(PositionGauge.ToOffset(-1f, 0.4f), Is.EqualTo(-0.4f).Within(1e-6f));
            Assert.That(PositionGauge.ToOffset(1f, 0.4f), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(PositionGauge.ToOffset(0f, 0.4f), Is.EqualTo(0f));
            Assert.That(PositionGauge.ToOffset(0.5f, 0.4f), Is.EqualTo(0.2f).Within(1e-6f));
        }

        [Test]
        public void 立ち位置のゲージは範囲の外を端に収める()
        {
            Assert.That(PositionGauge.ToOffset(1.7f, 0.4f), Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(PositionGauge.ToOffset(-3f, 0.4f), Is.EqualTo(-0.4f).Within(1e-6f));
            Assert.That(PositionGauge.ToValue(0.9f, 0.4f), Is.EqualTo(1f));
            Assert.That(PositionGauge.ToValue(0.1f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void 立ち位置のゲージの値と立ち位置は行き来しても同じ()
        {
            foreach (float v in new[] { -1f, -0.63f, -0.05f, 0f, 0.25f, 0.999f })
            {
                Assert.That(PositionGauge.ToValue(PositionGauge.ToOffset(v, 0.4f), 0.4f), Is.EqualTo(v).Within(1e-6f));
            }
            Assert.That(PositionGauge.ToCentimeters(-0.2049f), Is.EqualTo(20));
            Assert.That(PositionGauge.ToCentimeters(0.4f), Is.EqualTo(40));
        }

        [Test]
        public void 押しただけではCANCELを出さない()
        {
            Vector2 start = new Vector2(900f, 500f);
            bool moved = ThrowCancelRule.HasMoved(start, start + new Vector2(3f, -4f), 20f);
            Assert.That(moved, Is.False);
            Assert.That(ThrowCancelRule.ShouldShow(true, false, moved), Is.False);
        }

        [Test]
        public void 引いてから戻すとCANCELを出す()
        {
            Assert.That(ThrowCancelRule.HasMoved(new Vector2(900f, 500f), new Vector2(900f, 470f), 20f), Is.True);
            Assert.That(ThrowCancelRule.ShouldShow(true, false, true), Is.True);
        }

        [Test]
        public void 投げる範囲にあるときや引いていないときはCANCELを出さない()
        {
            Assert.That(ThrowCancelRule.ShouldShow(true, true, true), Is.False);
            Assert.That(ThrowCancelRule.ShouldShow(false, false, true), Is.False);
        }

        [Test]
        public void 上へ動かすと投げない範囲になる()
        {
            // 押した所より上（y が大きい）へ動かすと、引き幅は負になって投げない
            ThrowResult r = ThrowCalculator.Calculate(new Vector2(900f, 500f), new Vector2(900f, 600f), ThrowCalculator.ScaleForScreen(Reference, 1080f), 0f);
            Assert.That(r.isValid, Is.False);
            Assert.That(ThrowCancelRule.HasMoved(new Vector2(900f, 500f), new Vector2(900f, 600f), 20f), Is.True);
        }
    }
}
