using NUnit.Framework;
using CrazyBowling.UI;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>クレジットの自動送りの計算（段階6）。</summary>
    public class CreditsScrollRulesTests
    {
        [Test]
        public void 見出しを上から3分の1に来させる位置()
        {
            // 中身の上から 1000 の見出しを、高さ 600 の見える範囲の上から 0.33 に置く → 1000 − 198 = 802
            Assert.That(CreditsScrollRules.OffsetFor(1000f, 600f, 0.33f, 4000f), Is.EqualTo(802f).Within(0.01f));
        }

        [Test]
        public void いちばん上といちばん下より先へは送らない()
        {
            Assert.That(CreditsScrollRules.OffsetFor(50f, 600f, 0.33f, 4000f), Is.EqualTo(0f));
            Assert.That(CreditsScrollRules.OffsetFor(3950f, 600f, 0.33f, 4000f), Is.EqualTo(3400f));
            // 中身が見える範囲より短ければ動かない
            Assert.That(CreditsScrollRules.OffsetFor(300f, 600f, 0.33f, 500f), Is.EqualTo(0f));
        }

        [Test]
        public void 目印と目印の間は一定の速さ()
        {
            var times = new[] { 0f, 10f, 20f };
            var offsets = new[] { 0f, 500f, 700f };
            Assert.That(CreditsScrollRules.TargetOffset(-1f, times, offsets), Is.EqualTo(0f));
            Assert.That(CreditsScrollRules.TargetOffset(5f, times, offsets), Is.EqualTo(250f).Within(0.01f));
            Assert.That(CreditsScrollRules.TargetOffset(10f, times, offsets), Is.EqualTo(500f).Within(0.01f));
            Assert.That(CreditsScrollRules.TargetOffset(15f, times, offsets), Is.EqualTo(600f).Within(0.01f));
        }

        [Test]
        public void 最後の目印より後はそこで止まる()
        {
            var times = new[] { 0f, 10f };
            var offsets = new[] { 0f, 500f };
            Assert.That(CreditsScrollRules.TargetOffset(99f, times, offsets), Is.EqualTo(500f));
        }

        [Test]
        public void 目印が無ければいちばん上()
        {
            Assert.That(CreditsScrollRules.TargetOffset(5f, new float[0], new float[0]), Is.EqualTo(0f));
        }

        [Test]
        public void 速さの上限を守って近づく()
        {
            // 上限 400／秒で 0.1秒 → 40 だけ進む
            Assert.That(CreditsScrollRules.Step(0f, 1000f, 400f, 0.1f), Is.EqualTo(40f).Within(0.001f));
            // 近ければ目標で止まる（行き過ぎない）
            Assert.That(CreditsScrollRules.Step(990f, 1000f, 400f, 0.1f), Is.EqualTo(1000f));
            // 戻る向きも同じ
            Assert.That(CreditsScrollRules.Step(1000f, 0f, 400f, 0.1f), Is.EqualTo(960f).Within(0.001f));
        }
    }
}
