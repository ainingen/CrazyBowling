using NUnit.Framework;
using CrazyBowling.Core;
using CrazyBowling.UI;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>1本目の最初の1投のヒントと、遊び方の画面のページ分け（段階6）。</summary>
    public class FirstThrowHintTests
    {
        [Test]
        public void 初めての1本目の1投目の構え中だけ出す()
        {
            Assert.That(FirstThrowHintRules.ShouldShow(false, 1, 1, true, false, false), Is.True);
        }

        [Test]
        public void 一度投げたら出さない()
        {
            Assert.That(FirstThrowHintRules.ShouldShow(true, 1, 1, true, false, false), Is.False);
        }

        [Test]
        public void 一本目の一投目のほかでは出さない()
        {
            Assert.That(FirstThrowHintRules.ShouldShow(false, 2, 1, true, false, false), Is.False);
            Assert.That(FirstThrowHintRules.ShouldShow(false, 1, 2, true, false, false), Is.False);
        }

        [Test]
        public void 押し始めたら消える()
        {
            // 押し始めると構え中ではなくなる
            Assert.That(FirstThrowHintRules.ShouldShow(false, 1, 1, false, false, false), Is.False);
        }

        [Test]
        public void 上に画面が重なっているときと下見の間は出さない()
        {
            Assert.That(FirstThrowHintRules.ShouldShow(false, 1, 1, true, true, false), Is.False);
            Assert.That(FirstThrowHintRules.ShouldShow(false, 1, 1, true, false, true), Is.False);
        }

        [Test]
        public void 指の丸は押す所で現れて手前へ滑り離す所で消える()
        {
            const float fade = 0.5f, stroke = 1.5f, rest = 0.5f;
            FirstThrowHintRules.Gesture(0f, fade, stroke, rest, out float p0, out float a0);
            Assert.That(p0, Is.EqualTo(0f));
            Assert.That(a0, Is.EqualTo(0f).Within(1e-5f));

            FirstThrowHintRules.Gesture(fade, fade, stroke, rest, out float p1, out float a1);
            Assert.That(p1, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(a1, Is.EqualTo(1f));

            FirstThrowHintRules.Gesture(fade + stroke * 0.5f, fade, stroke, rest, out float pm, out float am);
            Assert.That(pm, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(am, Is.EqualTo(1f));

            FirstThrowHintRules.Gesture(fade + stroke + fade + 0.1f, fade, stroke, rest, out float p2, out float a2);
            Assert.That(p2, Is.EqualTo(1f));
            Assert.That(a2, Is.EqualTo(0f));
        }

        [Test]
        public void 指の丸の動きは繰り返し急に明るさが変わらない()
        {
            const float fade = 0.45f, stroke = 1.5f, rest = 0.5f;
            float period = fade + stroke + fade + rest;
            FirstThrowHintRules.Gesture(0.3f, fade, stroke, rest, out float pa, out float aa);
            FirstThrowHintRules.Gesture(0.3f + period, fade, stroke, rest, out float pb, out float ab);
            Assert.That(pb, Is.EqualTo(pa).Within(1e-4f));
            Assert.That(ab, Is.EqualTo(aa).Within(1e-4f));

            // 1/60秒ずつ見て、明るさの変わり方がゆっくり（点滅しない）
            float prev = 0f, maxStep = 0f;
            for (int i = 0; i <= 60 * 6; i++)
            {
                FirstThrowHintRules.Gesture(i / 60f, fade, stroke, rest, out _, out float a);
                if (i > 0) maxStep = System.Math.Max(maxStep, System.Math.Abs(a - prev));
                prev = a;
            }
            Assert.That(maxStep, Is.LessThan(0.1f));
        }

        [Test]
        public void 遊び方の画面のページ分け()
        {
            Assert.That(HowToPlayRules.PageCount(8, 2), Is.EqualTo(4));
            Assert.That(HowToPlayRules.PageCount(7, 2), Is.EqualTo(4));
            Assert.That(HowToPlayRules.PageCount(0, 2), Is.EqualTo(1));
            Assert.That(HowToPlayRules.PageCount(8, 0), Is.EqualTo(8));

            Assert.That(HowToPlayRules.ClampPage(-1, 8, 2), Is.EqualTo(0));
            Assert.That(HowToPlayRules.ClampPage(9, 8, 2), Is.EqualTo(3));

            HowToPlayRules.SectionRange(3, 7, 2, out int first, out int count);
            Assert.That(first, Is.EqualTo(6));
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void 遊び方の文面は見出しと本文が同じ数で全部のページに載る()
        {
            Assert.That(UIText.HowToPlayHeadings.Length, Is.EqualTo(UIText.HowToPlayBodies.Length));
            int shown = 0;
            int pages = HowToPlayRules.PageCount(UIText.HowToPlayHeadings.Length, 2);
            for (int p = 0; p < pages; p++)
            {
                HowToPlayRules.SectionRange(p, UIText.HowToPlayHeadings.Length, 2, out _, out int count);
                shown += count;
            }
            Assert.That(shown, Is.EqualTo(UIText.HowToPlayHeadings.Length));
        }
    }
}
