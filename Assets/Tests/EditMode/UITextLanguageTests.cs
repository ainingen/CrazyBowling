using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using CrazyBowling.UI;

namespace CrazyBowling.Tests.EditMode
{
    /// <summary>英語化（段階6）：日本語と英語の文言がそろっていること、英語がフォントに焼き込んだ字だけで書かれていること。</summary>
    public class UITextLanguageTests
    {
        /// <summary>
        /// 英語版でも日本語のまま出すと決めた部分（作曲者の日本語の元の名前・英語の表記の無い名前・効果音ラボの音の日本語の題）。
        /// どれも日本語版にある字なので、フォントには焼き込み済み。
        /// </summary>
        private static readonly string[] AllowedJapanese =
        {
            "（田中芳典）", "（のる）", "（北見ヒツジ）", "（蒲鉾さちこ）", "（稿屋 隆）", "（マニーラ）",
            "えだまめ88", "えすにっく・かわひろ",
            "ボウリングのピンを倒す1", "ボウリングのピンを倒す2",
        };

        private static List<FieldInfo> Fields(System.Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(f => f.Name).ToList();
        }

        /// <summary>欄の中の文字列を全部集める（入れ子の配列も）。</summary>
        private static IEnumerable<(string where, string text)> Strings(System.Type t)
        {
            foreach (FieldInfo f in Fields(t))
            {
                object v = f.GetValue(null);
                if (v is string s)
                {
                    yield return (f.Name, s);
                }
                else if (v is string[] a)
                {
                    for (int i = 0; i < a.Length; i++) yield return ($"{f.Name}[{i}]", a[i]);
                }
                else if (v is string[][] aa)
                {
                    for (int i = 0; i < aa.Length; i++)
                        for (int j = 0; j < aa[i].Length; j++) yield return ($"{f.Name}[{i}][{j}]", aa[i][j]);
                }
            }
        }

        [Test]
        public void 日本語と英語の欄の名前と種類がそろっている()
        {
            var ja = Fields(typeof(UITextJa)).Select(f => f.Name + ":" + f.FieldType.Name).ToList();
            var en = Fields(typeof(UITextEn)).Select(f => f.Name + ":" + f.FieldType.Name).ToList();
            Assert.That(en, Is.EqualTo(ja));
            Assert.That(ja.Count, Is.GreaterThan(0));
        }

        [Test]
        public void 日本語と英語の配列の長さがそろっている()
        {
            Assert.That(UITextEn.HowToPlayHeadings.Length, Is.EqualTo(UITextJa.HowToPlayHeadings.Length));
            Assert.That(UITextEn.HowToPlayBodies.Length, Is.EqualTo(UITextJa.HowToPlayBodies.Length));
            Assert.That(UITextEn.CreditsSectionLines.Length, Is.EqualTo(UITextJa.CreditsSectionLines.Length));
            for (int i = 0; i < UITextJa.CreditsSectionLines.Length; i++)
            {
                Assert.That(UITextEn.CreditsSectionLines[i].Length, Is.EqualTo(UITextJa.CreditsSectionLines[i].Length), $"クレジットの節 {i}");
            }
        }

        [Test]
        public void 空の文言が無い()
        {
            foreach (var (where, text) in Strings(typeof(UITextJa)).Concat(Strings(typeof(UITextEn))))
            {
                Assert.That(string.IsNullOrWhiteSpace(text), Is.False, where);
            }
        }

        [Test]
        public void 英語はASCIIと矢印だけで書かれている()
        {
            foreach (var (where, text) in Strings(typeof(UITextEn)))
            {
                string rest = text;
                foreach (string ok in AllowedJapanese) rest = rest.Replace(ok, "");
                foreach (char c in rest)
                {
                    bool fine = (c >= 32 && c < 127) || c == '\n' || c == '\t' || c == '◀' || c == '▶';
                    Assert.That(fine, Is.True, $"{where} に焼き込んでいない字 '{c}'（U+{(int)c:X4}）");
                }
            }
        }

        [Test]
        public void クレジットの札の区切りは日本語と英語で同じ()
        {
            for (int i = 0; i < UITextJa.CreditsSectionLines.Length; i++)
            {
                for (int j = 0; j < UITextJa.CreditsSectionLines[i].Length; j++)
                {
                    bool jaTab = UITextJa.CreditsSectionLines[i][j].Contains("\t");
                    bool enTab = UITextEn.CreditsSectionLines[i][j].Contains("\t");
                    Assert.That(enTab, Is.EqualTo(jaTab), $"クレジット {i}-{j}");
                }
            }
        }

        [Test]
        public void 全レーンに英語の説明がありASCIIで書かれている()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:LaneData", new[] { "Assets/Data/Lanes" });
            Assert.That(guids.Length, Is.EqualTo(10));
            foreach (string g in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                var lane = UnityEditor.AssetDatabase.LoadAssetAtPath<CrazyBowling.Data.LaneData>(path);
                Assert.That(string.IsNullOrWhiteSpace(lane.DescriptionEn), Is.False, path);
                Assert.That(lane.DescriptionEn.All(c => c >= 32 && c < 127), Is.True, path);
                Assert.That(string.IsNullOrWhiteSpace(lane.DescriptionJa), Is.False, path);
            }
        }
    }
}
