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

        [TestCase(300, "SSS")]
        [TestCase(299, "SS")]
        [TestCase(280, "SS")]
        [TestCase(279, "S")]
        [TestCase(250, "S")]
        [TestCase(249, "A")]
        [TestCase(200, "A")]
        [TestCase(199, "B")]
        [TestCase(150, "B")]
        [TestCase(149, "C")]
        [TestCase(100, "C")]
        [TestCase(99, "D")]
        [TestCase(0, "D")]
        public void RANKは境目の点数で決まる(int total, string expected)
        {
            Assert.That(RankRule.Decide(total), Is.EqualTo(expected));
            // 設定ファイルと同じ形（名前と境目の並び）で渡しても同じ
            Assert.That(RankRule.Decide(total, new[] { 300, 280, 250, 200, 150, 100 }, new[] { "SSS", "SS", "S", "A", "B", "C" }, "D"), Is.EqualTo(expected));
        }

        [Test]
        public void RANKの境目は変えられる()
        {
            // 例：SSS を 290 以上にしたら 295 も SSS
            var min = new[] { 290, 280, 250, 200, 150, 100 };
            var names = new[] { "SSS", "SS", "S", "A", "B", "C" };
            Assert.That(RankRule.Decide(295, min, names, "D"), Is.EqualTo("SSS"));
            Assert.That(RankRule.Decide(289, min, names, "D"), Is.EqualTo("SS"));
        }

        [Test]
        public void いちばん上のRANKだけが特別()
        {
            Assert.That(RankRule.IsTop("SSS", RankRule.DefaultNames), Is.True);
            Assert.That(RankRule.IsTop("SS", RankRule.DefaultNames), Is.False);
        }

        [Test]
        public void 古い決まりで付いた記録も合計点から新しい決まりで付け直せる()
        {
            // 古い決まり（合計÷満点で S〜D）で記録した記録帳：180点は「S」、135点は「A」、45点は「C」と残っている
            var old = new RecordBook();
            RecordRules.Add(old, new GameRecord { playedAt = "2026/09/28 10:00", total = 45, rank = "C", lanes = new LaneRecord[0] }, 20);
            RecordRules.Add(old, new GameRecord { playedAt = "2026/09/28 11:00", total = 135, rank = "A", lanes = new LaneRecord[0] }, 20);
            RecordRules.Add(old, new GameRecord { playedAt = "2026/09/28 12:00", total = 180, rank = "S", lanes = new LaneRecord[0] }, 20);
            string json = RecordRules.ToJson(old);

            // 読み直しても形は同じ（版の番号 1 のまま読める）
            RecordBook book = RecordRules.FromJson(json, 10, out bool ok);
            Assert.That(ok, Is.True);
            Assert.That(book.bestRank, Is.EqualTo("S"));

            // 表示は合計点から今の決まりで付け直す（記録の画面と同じ呼び方）
            Assert.That(RankRule.Decide(book.bestTotal), Is.EqualTo("B"));
            Assert.That(RankRule.Decide(book.recent[0].total), Is.EqualTo("B"));
            Assert.That(RankRule.Decide(book.recent[1].total), Is.EqualTo("C"));
            Assert.That(RankRule.Decide(book.recent[2].total), Is.EqualTo("D"));
        }
    }
}
