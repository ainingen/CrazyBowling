using NUnit.Framework;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// ThrowProgress の EditMode テスト。
    /// レーン完結型なので、全部倒したらその時点でレーンは終わる。
    /// </summary>
    public class ThrowProgressTests
    {
        /// <summary>普通のレーン（2投・10本）。</summary>
        private static ThrowProgressSettings Normal()
        {
            return new ThrowProgressSettings { maxThrows = 2, pinCount = 10 };
        }

        // ---- まだ投げるか ----

        [Test]
        public void 一投目で倒しきれなければ二投目がある()
        {
            Assert.IsTrue(ThrowProgress.HasNextThrow(1, 7, Normal()));
        }

        [Test]
        public void ストライクなら二投目は無い()
        {
            Assert.IsFalse(ThrowProgress.HasNextThrow(1, 10, Normal()));
        }

        [Test]
        public void スペアならそこで終わる()
        {
            Assert.IsFalse(ThrowProgress.HasNextThrow(2, 10, Normal()));
        }

        [Test]
        public void 二投目で倒しきれなくても終わる()
        {
            Assert.IsFalse(ThrowProgress.HasNextThrow(2, 8, Normal()));
        }

        [Test]
        public void ガーターでも二投目はある()
        {
            Assert.IsTrue(ThrowProgress.HasNextThrow(1, 0, Normal()));
        }

        [Test]
        public void 一投だけのレーンでは二投目が無い()
        {
            var settings = new ThrowProgressSettings { maxThrows = 1, pinCount = 10 };

            Assert.IsFalse(ThrowProgress.HasNextThrow(1, 3, settings));
        }

        [Test]
        public void 三投のレーンなら二投目のあとも投げられる()
        {
            var settings = new ThrowProgressSettings { maxThrows = 3, pinCount = 10 };

            Assert.IsTrue(ThrowProgress.HasNextThrow(2, 8, settings));
            Assert.IsFalse(ThrowProgress.HasNextThrow(3, 8, settings));
        }

        [Test]
        public void ピンの本数が少ないレーンでも倒しきれば終わる()
        {
            var settings = new ThrowProgressSettings { maxThrows = 2, pinCount = 5 };

            Assert.IsFalse(ThrowProgress.HasNextThrow(1, 5, settings));
        }

        // ---- ストライクとスペア ----

        [Test]
        public void 一投目で全部倒すとストライク()
        {
            Assert.IsTrue(ThrowProgress.IsStrike(1, 10, Normal()));
        }

        [Test]
        public void 二投目で全部倒してもストライクではない()
        {
            Assert.IsFalse(ThrowProgress.IsStrike(2, 10, Normal()));
        }

        [Test]
        public void 一投目で九本ならストライクではない()
        {
            Assert.IsFalse(ThrowProgress.IsStrike(1, 9, Normal()));
        }

        [Test]
        public void 二投目で全部倒すとスペア()
        {
            Assert.IsTrue(ThrowProgress.IsSpare(2, 10, Normal()));
        }

        [Test]
        public void 一投目で全部倒してもスペアではない()
        {
            Assert.IsFalse(ThrowProgress.IsSpare(1, 10, Normal()));
        }

        [Test]
        public void 二投目で九本ならスペアではない()
        {
            Assert.IsFalse(ThrowProgress.IsSpare(2, 9, Normal()));
        }

        [Test]
        public void ストライクとスペアが同時に成り立つことはない()
        {
            for (int throwNumber = 1; throwNumber <= 2; throwNumber++)
            {
                for (int fallen = 0; fallen <= 10; fallen++)
                {
                    bool strike = ThrowProgress.IsStrike(throwNumber, fallen, Normal());
                    bool spare = ThrowProgress.IsSpare(throwNumber, fallen, Normal());

                    Assert.IsFalse(strike && spare, $"{throwNumber}投目・{fallen}本で両方成立した");
                }
            }
        }
    }
}
