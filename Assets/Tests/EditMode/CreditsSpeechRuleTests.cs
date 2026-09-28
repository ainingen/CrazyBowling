using NUnit.Framework;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>クレジットのあいさつの声を流すかの決まり（段階6）。</summary>
    public class CreditsSpeechRuleTests
    {
        [Test]
        public void 声があって音もDJも出しているときだけ流す()
        {
            Assert.That(CreditsSpeechRule.CanStart(true, true, false, false), Is.True);
        }

        [Test]
        public void 声のファイルが無ければ流さない()
        {
            Assert.That(CreditsSpeechRule.CanStart(false, true, false, false), Is.False);
        }

        [Test]
        public void 音を消しているかDJを消しているときは流さない()
        {
            Assert.That(CreditsSpeechRule.CanStart(true, true, true, false), Is.False);
            Assert.That(CreditsSpeechRule.CanStart(true, true, false, true), Is.False);
        }

        [Test]
        public void ブラウザで一度もクリックしていなければ流さない()
        {
            Assert.That(CreditsSpeechRule.CanStart(true, false, false, false), Is.False);
        }

        [Test]
        public void 流している途中で音かDJを消したら止める()
        {
            Assert.That(CreditsSpeechRule.ShouldStop(false, false), Is.False);
            Assert.That(CreditsSpeechRule.ShouldStop(true, false), Is.True);
            Assert.That(CreditsSpeechRule.ShouldStop(false, true), Is.True);
        }
    }
}
