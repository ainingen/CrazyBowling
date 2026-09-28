using System.Collections.Generic;
using NUnit.Framework;
using CrazyBowling.Core;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>DJ のラジオ番組の並び（段階6）。</summary>
    public class DjProgramTests
    {
        [Test]
        public void コーナーとIDが交互になる()
        {
            var p = new DjProgram(10, 3, 1, false);
            for (int i = 0; i < 40; i++)
            {
                Assert.That(p.Next().kind, Is.EqualTo(i % 2 == 0 ? DjItemKind.Corner : DjItemKind.Id));
            }
        }

        [Test]
        public void 一周のうちに同じコーナーは流れず全部流れる()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var p = new DjProgram(10, 3, seed, false);
                var seen = new Dictionary<int, HashSet<int>>();
                for (int i = 0; i < 100; i++)
                {
                    DjItem item = p.Next();
                    if (item.kind != DjItemKind.Corner)
                    {
                        continue;
                    }
                    if (!seen.ContainsKey(item.cycle))
                    {
                        seen[item.cycle] = new HashSet<int>();
                    }
                    Assert.That(seen[item.cycle].Add(item.index), Is.True, $"種 {seed}：{item.cycle}周目にコーナー{item.index}が2回");
                }
                // 50本のコーナー＝5周。どの周も10本そろう
                foreach (var kv in seen)
                {
                    Assert.That(kv.Value.Count, Is.EqualTo(10));
                }
            }
        }

        [Test]
        public void 周の変わり目で同じコーナーが続かない()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var p = new DjProgram(10, 0, seed, false);
                int last = -1;
                for (int i = 0; i < 60; i++)
                {
                    DjItem item = p.Next();
                    Assert.That(item.index, Is.Not.EqualTo(last), $"種 {seed}：{i}本目");
                    last = item.index;
                }
            }
        }

        [Test]
        public void IDは順番に使う()
        {
            var p = new DjProgram(10, 3, 5, false);
            var ids = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                DjItem item = p.Next();
                if (item.kind == DjItemKind.Id)
                {
                    ids.Add(item.index);
                }
            }
            for (int i = 1; i < ids.Count; i++)
            {
                Assert.That(ids[i], Is.EqualTo((ids[i - 1] + 1) % 3));
            }
        }

        [Test]
        public void 途中から始めると最初の周は残りのコーナーだけ流れる()
        {
            bool sawMiddle = false;
            for (int seed = 0; seed < 30; seed++)
            {
                var p = new DjProgram(10, 3, seed, true);
                int firstCycleCorners = 0;
                for (int i = 0; i < 40; i++)
                {
                    DjItem item = p.Next();
                    if (item.kind == DjItemKind.Corner && item.cycle == 1)
                    {
                        firstCycleCorners++;
                    }
                }
                Assert.That(firstCycleCorners, Is.InRange(1, 10));
                sawMiddle |= firstCycleCorners < 10;
            }
            Assert.That(sawMiddle, Is.True, "途中から始まったことが一度もない");
        }

        [Test]
        public void 同じ種なら同じ並び()
        {
            var a = new DjProgram(10, 3, 42, true);
            var b = new DjProgram(10, 3, 42, true);
            for (int i = 0; i < 30; i++)
            {
                DjItem x = a.Next();
                DjItem y = b.Next();
                Assert.That(x.kind, Is.EqualTo(y.kind));
                Assert.That(x.index, Is.EqualTo(y.index));
            }
        }

        [Test]
        public void コーナーが無ければIDだけ順番に流す()
        {
            var p = new DjProgram(0, 3, 1, false);
            Assert.That(p.Next().index, Is.EqualTo(0));
            Assert.That(p.Next().index, Is.EqualTo(1));
            Assert.That(p.Next().kind, Is.EqualTo(DjItemKind.Id));
        }

        [Test]
        public void 下げるのは速く戻すのはゆっくり()
        {
            float down = DuckEnvelope.Approach(1f, 0.5f, 0.1f, 1f, 0.05f);
            Assert.That(down, Is.EqualTo(0.5f).Within(1e-5f));
            float up = DuckEnvelope.Approach(0.5f, 1f, 0.1f, 1f, 0.05f);
            Assert.That(up, Is.EqualTo(0.55f).Within(1e-5f));
        }

        [Test]
        public void 目標を越えない()
        {
            Assert.That(DuckEnvelope.Approach(0.9f, 1f, 0.1f, 0.1f, 1f), Is.EqualTo(1f));
            Assert.That(DuckEnvelope.Approach(0.6f, 0.5f, 0.1f, 0.1f, 1f), Is.EqualTo(0.5f));
        }

        [Test]
        public void しゃべり終わっても保つ時間は下げたまま()
        {
            Assert.That(DuckEnvelope.Held(true, -1f, 10f, 2f), Is.True);
            Assert.That(DuckEnvelope.Held(false, 9f, 10f, 2f), Is.True);
            Assert.That(DuckEnvelope.Held(false, 7f, 10f, 2f), Is.False);
            Assert.That(DuckEnvelope.Held(false, -1f, 10f, 2f), Is.False);
        }
    }
}
