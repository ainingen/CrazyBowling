using CrazyBowling.Core;
using UnityEngine;

namespace CrazyBowling.Data
{
    /// <summary>
    /// RANK の表（段階6）。結果画面・記録の係・記録の画面が同じものを使う。
    /// 名前と境目は同じ順（上の RANK から）に並べる。境目は「この点数以上」。どれにも届かなければ <see cref="lowest"/>。
    /// 決め方は <see cref="RankRule"/>。
    /// </summary>
    [CreateAssetMenu(menuName = "CrazyBowling/Rank Table", fileName = "RankTable")]
    public class RankTable : ScriptableObject
    {
        [Tooltip("RANK の名前（上から）。")]
        public string[] names = { "SSS", "SS", "S", "A", "B", "C" };

        [Tooltip("それぞれの RANK になる点数（この点数以上）。names と同じ順に並べる。満点は 300。")]
        public int[] minScores = { 300, 280, 250, 200, 150, 100 };

        [Tooltip("どの境目にも届かなかったときの RANK。")]
        public string lowest = "D";

        /// <summary>合計点から RANK を決める。</summary>
        public string Decide(int total)
        {
            return RankRule.Decide(total, minScores, names, lowest);
        }

        /// <summary>いちばん上の RANK か（SSS）。</summary>
        public bool IsTop(string rank)
        {
            return RankRule.IsTop(rank, names);
        }

        /// <summary>表が無いときも既定の値で決める。</summary>
        public static string Decide(RankTable table, int total)
        {
            return table != null ? table.Decide(total) : RankRule.Decide(total);
        }

        /// <summary>表が無いときも既定の値で見る。</summary>
        public static bool IsTop(RankTable table, string rank)
        {
            return table != null ? table.IsTop(rank) : RankRule.IsTop(rank, RankRule.DefaultNames);
        }
    }
}
