using NUnit.Framework;
using UnityEngine;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>LOOK AHEAD の見回しの計算（段階6）。</summary>
    public class LookAroundRulesTests
    {
        private static readonly LookAroundLimits Limits = LookAroundLimits.Default;

        [Test]
        public void 右へドラッグすると右を向き上へドラッグすると上を向く()
        {
            Vector2 look = LookAroundRules.ApplyDrag(Vector2.zero, new Vector2(100f, 50f), 0.1f, Limits);
            Assert.That(look.x, Is.EqualTo(10f).Within(1e-4f));
            Assert.That(look.y, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void 左右は60度上下は20度で止まる()
        {
            Vector2 look = LookAroundRules.ApplyDrag(Vector2.zero, new Vector2(5000f, 5000f), 0.1f, Limits);
            Assert.That(look, Is.EqualTo(new Vector2(60f, 20f)));
            look = LookAroundRules.ApplyDrag(Vector2.zero, new Vector2(-5000f, -5000f), 0.1f, Limits);
            Assert.That(look, Is.EqualTo(new Vector2(-60f, -20f)));
        }

        [Test]
        public void レーンごとの幅で止まる()
        {
            var narrow = new LookAroundLimits { yaw = 30f, pitchUp = 5f, pitchDown = 10f, zoomIn = 8f, zoomOut = 2f };
            Vector2 look = LookAroundRules.Clamp(new Vector2(-45f, 12f), narrow);
            Assert.That(look, Is.EqualTo(new Vector2(-30f, 5f)));
            Assert.That(LookAroundRules.Clamp(new Vector2(0f, -40f), narrow).y, Is.EqualTo(-10f));
        }

        [Test]
        public void 寄り引きは狭い幅で止まる()
        {
            Assert.That(LookAroundRules.ApplyZoom(0f, -100f, Limits), Is.EqualTo(-15f));
            Assert.That(LookAroundRules.ApplyZoom(0f, 100f, Limits), Is.EqualTo(5f));
            Assert.That(LookAroundRules.ApplyZoom(-3f, 1f, Limits), Is.EqualTo(-2f));
        }

        [Test]
        public void 指を広げると寄りつまむと引く()
        {
            Assert.That(LookAroundRules.PinchToZoom(100f, 200f, 0.05f), Is.LessThan(0f));
            Assert.That(LookAroundRules.PinchToZoom(200f, 100f, 0.05f), Is.GreaterThan(0f));
        }

        [Test]
        public void ホイールを奥へ回すと寄る()
        {
            Assert.That(LookAroundRules.WheelToZoom(120f, 2f), Is.EqualTo(-2f).Within(1e-4f));
            Assert.That(LookAroundRules.WheelToZoom(-120f, 2f), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(LookAroundRules.WheelToZoom(12000f, 2f), Is.EqualTo(-6f).Within(1e-4f));
        }

        [Test]
        public void 見回しが0なら止まった所の向きのまま()
        {
            Quaternion baseRotation = Quaternion.Euler(12f, 5f, 0f);
            Quaternion r = LookAroundRules.Rotate(baseRotation, Vector2.zero);
            Assert.That(Quaternion.Angle(r, baseRotation), Is.LessThan(1e-3f));
        }

        [Test]
        public void 左右に向けても水平線は傾かない()
        {
            Quaternion baseRotation = Quaternion.Euler(12f, 0f, 0f);
            Quaternion r = LookAroundRules.Rotate(baseRotation, new Vector2(40f, 0f));
            Vector3 right = r * Vector3.right;
            Assert.That(Mathf.Abs(right.y), Is.LessThan(1e-4f));
            Assert.That((r * Vector3.forward).x, Is.GreaterThan(0.5f));
        }

        [Test]
        public void 上へ向けると前が上を向く()
        {
            Quaternion baseRotation = Quaternion.identity;
            Quaternion r = LookAroundRules.Rotate(baseRotation, new Vector2(0f, 20f));
            Assert.That((r * Vector3.forward).y, Is.EqualTo(Mathf.Sin(20f * Mathf.Deg2Rad)).Within(1e-4f));
        }

        [Test]
        public void 戻すときは0へ近づき越えない()
        {
            Vector2 look = LookAroundRules.Relax(new Vector2(10f, -4f), 100f, 1f);
            Assert.That(look, Is.EqualTo(Vector2.zero));
            look = LookAroundRules.Relax(new Vector2(10f, 0f), 5f, 1f);
            Assert.That(look.x, Is.EqualTo(5f).Within(1e-4f));
        }
    }
}
