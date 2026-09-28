using NUnit.Framework;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>個人の成績の記録帳（段階6）。</summary>
    public class RecordBookTests
    {
        private static GameRecord Game(int total, string at, params int[] laneScores)
        {
            var lanes = new LaneRecord[10];
            for (int i = 0; i < 10; i++)
            {
                int s = i < laneScores.Length ? laneScores[i] : 0;
                lanes[i] = s == 30 ? RecordRules.MakeLane(10, 0, 1, 10, 30)
                    : s == 20 ? RecordRules.MakeLane(6, 4, 2, 10, 20)
                    : s == 0 ? RecordRules.MakeLane(0, 0, 2, 10, 0)
                    : RecordRules.MakeLane(s, 0, 2, 10, s);
            }
            return new GameRecord { playedAt = at, total = total, rank = "C", lanes = lanes };
        }

        [Test]
        public void レーンの種類を決める()
        {
            Assert.That(RecordRules.MakeLane(10, 0, 1, 10, 30).kind, Is.EqualTo(RecordRules.KindStrike));
            Assert.That(RecordRules.MakeLane(7, 3, 2, 10, 20).kind, Is.EqualTo(RecordRules.KindSpare));
            Assert.That(RecordRules.MakeLane(0, 0, 2, 10, 0).kind, Is.EqualTo(RecordRules.KindGutter));
            Assert.That(RecordRules.MakeLane(4, 2, 2, 10, 6).kind, Is.EqualTo(RecordRules.KindOpen));
            // 0本 → 10本はスペア（ガターではない）
            Assert.That(RecordRules.MakeLane(0, 10, 2, 10, 20).kind, Is.EqualTo(RecordRules.KindSpare));
        }

        [Test]
        public void ガターは倒した本数0の投の数()
        {
            Assert.That(RecordRules.GutterThrows(RecordRules.MakeLane(0, 0, 2, 10, 0)), Is.EqualTo(2));
            Assert.That(RecordRules.GutterThrows(RecordRules.MakeLane(0, 7, 2, 10, 7)), Is.EqualTo(1));
            Assert.That(RecordRules.GutterThrows(RecordRules.MakeLane(5, 0, 2, 10, 5)), Is.EqualTo(1));
            Assert.That(RecordRules.GutterThrows(RecordRules.MakeLane(10, 0, 1, 10, 30)), Is.EqualTo(0));
        }

        [Test]
        public void 最初のゲームは記録するがNEW_RECORDは出さない()
        {
            var book = new RecordBook();
            RecordUpdate u = RecordRules.Add(book, Game(95, "a", 30, 20, 5), 20);
            Assert.That(u.newTotalBest, Is.False);
            Assert.That(u.newLaneBests[0], Is.False);
            Assert.That(book.bestTotal, Is.EqualTo(95));
            Assert.That(book.laneBests[0], Is.EqualTo(30));
            Assert.That(book.games, Is.EqualTo(1));
        }

        [Test]
        public void 上回ったときだけ自己ベストを更新する()
        {
            var book = new RecordBook();
            RecordRules.Add(book, Game(95, "a", 30, 5, 5), 20);
            RecordUpdate u = RecordRules.Add(book, Game(80, "b", 20, 9, 5), 20);
            Assert.That(u.newTotalBest, Is.False);
            Assert.That(book.bestTotal, Is.EqualTo(95));
            Assert.That(book.bestPlayedAt, Is.EqualTo("a"));
            Assert.That(u.newLaneBests[0], Is.False);
            Assert.That(u.newLaneBests[1], Is.True);
            Assert.That(u.newLaneBests[2], Is.False, "同じ点は更新ではない");

            u = RecordRules.Add(book, Game(120, "c", 30, 5, 5), 20);
            Assert.That(u.newTotalBest, Is.True);
            Assert.That(book.bestTotal, Is.EqualTo(120));
            Assert.That(book.bestPlayedAt, Is.EqualTo("c"));
        }

        [Test]
        public void 通算を数える()
        {
            var book = new RecordBook();
            RecordRules.Add(book, Game(0, "a", 30, 20, 0, 5), 20);
            RecordRules.Add(book, Game(0, "b", 30), 20);
            Assert.That(book.games, Is.EqualTo(2));
            Assert.That(book.strikes, Is.EqualTo(2));
            Assert.That(book.spares, Is.EqualTo(1));
            // 1ゲーム目：0本のレーン 7本（各2投）＋ 5本のレーン（2投目 0）＝ 15。2ゲーム目：0本のレーン 9本 ＝ 18
            Assert.That(book.gutters, Is.EqualTo(33));
        }

        [Test]
        public void 残す数を超えたら古いものから消す()
        {
            var book = new RecordBook();
            for (int i = 1; i <= 21; i++)
            {
                RecordRules.Add(book, Game(i, "g" + i), 20);
            }
            Assert.That(book.recent.Count, Is.EqualTo(20));
            Assert.That(book.recent[0].playedAt, Is.EqualTo("g21"), "新しいものが先頭");
            Assert.That(book.recent[19].playedAt, Is.EqualTo("g2"), "いちばん古い g1 が消えた");
            Assert.That(book.games, Is.EqualTo(21), "通算は消えない");
        }

        [Test]
        public void 残す数は変えられる()
        {
            var book = new RecordBook();
            for (int i = 1; i <= 8; i++)
            {
                RecordRules.Add(book, Game(i, "g" + i), 5);
            }
            Assert.That(book.recent.Count, Is.EqualTo(5));
            Assert.That(book.recent[4].playedAt, Is.EqualTo("g4"));
        }

        [Test]
        public void JSONにして読み直せる()
        {
            var book = new RecordBook();
            RecordRules.Add(book, Game(95, "2026/09/28 18:05", 30, 20, 5), 20);
            string json = RecordRules.ToJson(book);
            RecordBook back = RecordRules.FromJson(json, 10, out bool ok);
            Assert.That(ok, Is.True);
            Assert.That(back.version, Is.EqualTo(RecordRules.CurrentVersion));
            Assert.That(back.bestTotal, Is.EqualTo(95));
            Assert.That(back.recent.Count, Is.EqualTo(1));
            Assert.That(back.recent[0].lanes[0].kind, Is.EqualTo(RecordRules.KindStrike));
            Assert.That(back.recent[0].lanes[1].second, Is.EqualTo(4));
            Assert.That(back.recent[0].playedAt, Is.EqualTo("2026/09/28 18:05"));
            Assert.That(back.laneBests.Length, Is.EqualTo(10));
        }

        [Test]
        public void 壊れた記録は捨てて空から始める()
        {
            foreach (string bad in new[] { "壊れた記録", "{\"version\":", "{\"version\":99,\"bestTotal\":5}", "[1,2,3]" })
            {
                RecordBook book = RecordRules.FromJson(bad, 10, out bool ok);
                Assert.That(ok, Is.False, bad);
                Assert.That(book.bestTotal, Is.EqualTo(-1), bad);
                Assert.That(book.recent, Is.Not.Null);
                Assert.That(book.laneBests.Length, Is.EqualTo(10));
            }
        }

        [Test]
        public void 記録が無いときは空で読めたことにする()
        {
            RecordBook book = RecordRules.FromJson("", 10, out bool ok);
            Assert.That(ok, Is.True);
            Assert.That(book.games, Is.EqualTo(0));
        }

        [Test]
        public void RANKの決め方は結果画面と同じ()
        {
            float[] t = { 0.6f, 0.45f, 0.3f, 0.15f };
            Assert.That(RankRule.Decide(180, 300, t), Is.EqualTo("S"));
            Assert.That(RankRule.Decide(179, 300, t), Is.EqualTo("A"));
            Assert.That(RankRule.Decide(135, 300, t), Is.EqualTo("A"));
            Assert.That(RankRule.Decide(90, 300, t), Is.EqualTo("B"));
            Assert.That(RankRule.Decide(45, 300, t), Is.EqualTo("C"));
            Assert.That(RankRule.Decide(44, 300, t), Is.EqualTo("D"));
        }
    }
}
