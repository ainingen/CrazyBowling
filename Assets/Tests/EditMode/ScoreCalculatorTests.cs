using NUnit.Framework;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>
    /// ScoreCalculator の EditMode テスト。
    /// レーン完結型なので、1レーンの得点はその場で確定し、あとのレーンに影響しない。
    /// </summary>
    public class ScoreCalculatorTests
    {
        /// <summary>投球の結果を組み立てる。</summary>
        private static LaneThrowResult Throws(int first, int second, int throwCount, int pinCount = 10)
        {
            return new LaneThrowResult
            {
                firstThrowFallen = first,
                secondThrowFallen = second,
                throwCount = throwCount,
                pinCount = pinCount,
            };
        }

        // ---- ストライク ----

        [Test]
        public void 一投目で全部倒すとストライクで30点()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(10, 0, 1), 1f);

            Assert.IsTrue(score.isStrike);
            Assert.IsFalse(score.isSpare);
            Assert.AreEqual(30, score.score);
        }

        [Test]
        public void ストライクでも倒した本数は10本のまま()
        {
            Assert.AreEqual(10, ScoreCalculator.Calculate(Throws(10, 0, 1), 1f).fallen);
        }

        // ---- スペア ----

        [Test]
        public void 二投合計で全部倒すとスペアで20点()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(7, 3, 2), 1f);

            Assert.IsFalse(score.isStrike);
            Assert.IsTrue(score.isSpare);
            Assert.AreEqual(20, score.score);
        }

        [Test]
        public void 一投目0本でも二投目で全部倒せばスペア()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(0, 10, 2), 1f);

            Assert.IsTrue(score.isSpare);
            Assert.AreEqual(20, score.score);
        }

        [Test]
        public void ストライクとスペアが同時に立つことはない()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(10, 0, 2), 1f);

            Assert.IsFalse(score.isStrike && score.isSpare);
        }

        // ---- それ以外 ----

        [Test]
        public void 倒しきれなければ本数がそのまま点になる()
        {
            Assert.AreEqual(7, ScoreCalculator.Calculate(Throws(5, 2, 2), 1f).score);
        }

        [Test]
        public void ガーターは0点()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(0, 0, 2), 1f);

            Assert.AreEqual(0, score.score);
            Assert.IsFalse(score.isStrike);
            Assert.IsFalse(score.isSpare);
        }

        [Test]
        public void 九本ならスペアにならず9点()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(8, 1, 2), 1f);

            Assert.IsFalse(score.isSpare);
            Assert.AreEqual(9, score.score);
        }

        [Test]
        public void 一投で終わって倒しきれなければ本数がそのまま点になる()
        {
            // ピットに落ちるなどで2投目が無かった場合
            Assert.AreEqual(6, ScoreCalculator.Calculate(Throws(6, 0, 1), 1f).score);
        }

        // ---- 倍率 ----

        [Test]
        public void 倍率1では素の点のまま()
        {
            Assert.AreEqual(30, ScoreCalculator.Calculate(Throws(10, 0, 1), 1f).score);
        }

        [Test]
        public void 倍率2でストライクは60点()
        {
            Assert.AreEqual(60, ScoreCalculator.Calculate(Throws(10, 0, 1), 2f).score);
        }

        [Test]
        public void 倍率の端数は切り捨てる()
        {
            // 7本 × 1.5 = 10.5 → 10
            Assert.AreEqual(10, ScoreCalculator.Calculate(Throws(5, 2, 2), 1.5f).score);
        }

        [Test]
        public void 倍率をかけても素の点は残る()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(10, 0, 1), 2f);

            Assert.AreEqual(30, score.baseScore);
            Assert.AreEqual(60, score.score);
        }

        [Test]
        public void 倍率0なら0点()
        {
            Assert.AreEqual(0, ScoreCalculator.Calculate(Throws(10, 0, 1), 0f).score);
        }

        [Test]
        public void 負の倍率でもマイナスにはならない()
        {
            Assert.AreEqual(0, ScoreCalculator.Calculate(Throws(10, 0, 1), -2f).score);
        }

        [Test]
        public void 倍率で1点未満になったら0点()
        {
            // 1本 × 0.5 = 0.5 → 0
            Assert.AreEqual(0, ScoreCalculator.Calculate(Throws(1, 0, 2), 0.5f).score);
        }

        // ---- 変わったピン数のレーン ----

        [Test]
        public void ピンが5本のレーンでも全部倒せばストライク()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(5, 0, 1, pinCount: 5), 1f);

            Assert.IsTrue(score.isStrike);
            Assert.AreEqual(30, score.score);
        }

        [Test]
        public void ピンが0本のレーンではストライクにならない()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(0, 0, 1, pinCount: 0), 1f);

            Assert.IsFalse(score.isStrike);
            Assert.AreEqual(0, score.score);
        }

        // ---- おかしな値が来ても壊れない ----

        [Test]
        public void 本数がピン数を超えても丸められる()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(12, 5, 2), 1f);

            Assert.AreEqual(10, score.fallen);
            Assert.IsTrue(score.isSpare, "2投かかっているのでスペア");
            Assert.AreEqual(20, score.score);
        }

        [Test]
        public void 一投目だけでピン数を超えてもストライク()
        {
            LaneScore score = ScoreCalculator.Calculate(Throws(12, 0, 1), 1f);

            Assert.AreEqual(10, score.fallen);
            Assert.IsTrue(score.isStrike);
            Assert.AreEqual(30, score.score);
        }

        [Test]
        public void 負の本数は0として扱う()
        {
            Assert.AreEqual(0, ScoreCalculator.Calculate(Throws(-3, -1, 2), 1f).fallen);
        }

        // ---- 合計 ----

        [Test]
        public void 全レーンストライクなら300点()
        {
            var lanes = new LaneScore[10];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = ScoreCalculator.Calculate(Throws(10, 0, 1), 1f);
            }

            Assert.AreEqual(300, ScoreCalculator.CalculateTotal(lanes));
        }

        [Test]
        public void 全レーンスペアなら200点()
        {
            var lanes = new LaneScore[10];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = ScoreCalculator.Calculate(Throws(7, 3, 2), 1f);
            }

            Assert.AreEqual(200, ScoreCalculator.CalculateTotal(lanes));
        }

        [Test]
        public void 全レーンガーターなら0点()
        {
            var lanes = new LaneScore[10];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = ScoreCalculator.Calculate(Throws(0, 0, 2), 1f);
            }

            Assert.AreEqual(0, ScoreCalculator.CalculateTotal(lanes));
        }

        [Test]
        public void 合計は空でも0点()
        {
            Assert.AreEqual(0, ScoreCalculator.CalculateTotal(new LaneScore[0]));
            Assert.AreEqual(0, ScoreCalculator.CalculateTotal(null));
        }

        [Test]
        public void 混ざっていても足し合わせる()
        {
            var lanes = new[]
            {
                ScoreCalculator.Calculate(Throws(10, 0, 1), 1f),  // 30
                ScoreCalculator.Calculate(Throws(7, 3, 2), 1f),   // 20
                ScoreCalculator.Calculate(Throws(5, 2, 2), 1f),   // 7
                ScoreCalculator.Calculate(Throws(0, 0, 2), 1f),   // 0
            };

            Assert.AreEqual(57, ScoreCalculator.CalculateTotal(lanes));
        }

        // ---- 満点 ----

        [Test]
        public void 十レーンの満点は300点()
        {
            Assert.AreEqual(300, ScoreCalculator.CalculatePerfectScore(10));
        }

        [Test]
        public void レーン数が0なら満点も0点()
        {
            Assert.AreEqual(0, ScoreCalculator.CalculatePerfectScore(0));
            Assert.AreEqual(0, ScoreCalculator.CalculatePerfectScore(-5));
        }
    }
}
