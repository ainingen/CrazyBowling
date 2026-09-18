using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>レーン1本ぶんの投球の結果。得点を出すための材料。</summary>
    public struct LaneThrowResult
    {
        /// <summary>1投目で倒した本数。</summary>
        public int firstThrowFallen;

        /// <summary>2投目で新たに倒した本数。1投で終わったなら0。</summary>
        public int secondThrowFallen;

        /// <summary>実際に投げた回数。</summary>
        public int throwCount;

        /// <summary>立っていたピンの本数。通常は10。</summary>
        public int pinCount;

        /// <summary>このレーンで倒した合計本数。</summary>
        public int TotalFallen => firstThrowFallen + secondThrowFallen;
    }

    /// <summary>レーン1本ぶんの得点。</summary>
    public struct LaneScore
    {
        /// <summary>倒した合計本数。</summary>
        public int fallen;

        /// <summary>倍率をかける前の得点。</summary>
        public int baseScore;

        /// <summary>倍率をかけて小数点以下を切り捨てた、最終的な得点。</summary>
        public int score;

        /// <summary>1投目で全部倒したか。</summary>
        public bool isStrike;

        /// <summary>2投目までで全部倒したか（1投目で倒しきった場合は含めない）。</summary>
        public bool isSpare;
    }

    /// <summary>
    /// レーン完結型の得点計算。
    /// 1レーンの得点はその場で確定し、あとのレーンには影響しない。
    /// MonoBehaviour に依存しないので EditMode テストで確かめられる。
    /// </summary>
    public static class ScoreCalculator
    {
        /// <summary>ストライクの得点。</summary>
        public const int StrikeScore = 30;

        /// <summary>スペアの得点。</summary>
        public const int SpareScore = 20;

        /// <summary>
        /// レーン1本ぶんの得点を出す。
        /// 1投目で全部倒せばストライクで30点、2投合計で全部ならスペアで20点、
        /// それ以外は倒した本数。最後に倍率をかけて小数点以下を切り捨てる。
        /// </summary>
        public static LaneScore Calculate(LaneThrowResult result, float scoreMultiplier)
        {
            int pinCount = Mathf.Max(0, result.pinCount);
            int first = Mathf.Clamp(result.firstThrowFallen, 0, pinCount);
            int second = Mathf.Clamp(result.secondThrowFallen, 0, pinCount - first);
            int fallen = first + second;

            // ピンが0本のレーンは、倒しようがないので「全部倒した」とはみなさない
            bool clearedAll = pinCount > 0 && fallen >= pinCount;
            bool isStrike = clearedAll && result.throwCount <= 1;
            bool isSpare = clearedAll && !isStrike;

            int baseScore = isStrike ? StrikeScore
                : isSpare ? SpareScore
                : fallen;

            return new LaneScore
            {
                fallen = fallen,
                baseScore = baseScore,
                score = ApplyMultiplier(baseScore, scoreMultiplier),
                isStrike = isStrike,
                isSpare = isSpare,
            };
        }

        /// <summary>
        /// 倍率をかけて小数点以下を切り捨てる。
        /// 負の倍率はありえないので0として扱う。
        /// </summary>
        public static int ApplyMultiplier(int baseScore, float scoreMultiplier)
        {
            if (scoreMultiplier <= 0f || baseScore <= 0)
            {
                return 0;
            }

            return Mathf.FloorToInt(baseScore * scoreMultiplier);
        }

        /// <summary>レーンごとの得点を足し合わせる。</summary>
        public static int CalculateTotal(LaneScore[] laneScores)
        {
            if (laneScores == null)
            {
                return 0;
            }

            int total = 0;
            foreach (LaneScore lane in laneScores)
            {
                total += lane.score;
            }
            return total;
        }

        /// <summary>
        /// 倍率なしで取れる満点。10レーンすべてストライクなら300点。
        /// リザルトで「300点中◯点」と出すのに使う。
        /// </summary>
        public static int CalculatePerfectScore(int laneCount)
        {
            return Mathf.Max(0, laneCount) * StrikeScore;
        }
    }
}
