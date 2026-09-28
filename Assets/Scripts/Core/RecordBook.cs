using System.Collections.Generic;
using UnityEngine;

namespace CrazyBowling.Core
{
    /// <summary>1レーンぶんの記録（段階6）。</summary>
    [System.Serializable]
    public class LaneRecord
    {
        /// <summary>1投目に倒した本数。</summary>
        public int first;

        /// <summary>2投目に倒した本数（投げていなければ 0）。</summary>
        public int second;

        /// <summary>投げた数（ストライクなら 1）。</summary>
        public int throws;

        /// <summary>このレーンの点数。</summary>
        public int score;

        /// <summary>種類：strike・spare・gutter（2投とも0本）・open（そのほか）。</summary>
        public string kind;
    }

    /// <summary>1ゲームぶんの記録（段階6）。</summary>
    [System.Serializable]
    public class GameRecord
    {
        /// <summary>遊んだ日時（例：2026/09/28 18:05）。</summary>
        public string playedAt;

        /// <summary>合計点。</summary>
        public int total;

        /// <summary>RANK（S〜D）。</summary>
        public string rank;

        /// <summary>レーンごとの記録（1本目から順に）。</summary>
        public LaneRecord[] lanes = new LaneRecord[0];
    }

    /// <summary>
    /// 遊んだ人の成績の記録帳（段階6）。JSON にして PlayerPrefs に残す（<see cref="RecordStore"/>）。
    /// 形を変えるときは <see cref="RecordRules.CurrentVersion"/> を上げ、古い版を読めるようにする。
    /// </summary>
    [System.Serializable]
    public class RecordBook
    {
        /// <summary>形の版の番号。</summary>
        public int version = RecordRules.CurrentVersion;

        /// <summary>合計点の自己ベスト（まだなら -1）。</summary>
        public int bestTotal = -1;

        /// <summary>自己ベストのときの RANK。</summary>
        public string bestRank = "";

        /// <summary>自己ベストを出した日時。</summary>
        public string bestPlayedAt = "";

        /// <summary>レーンごとの自己ベスト（まだなら -1）。</summary>
        public int[] laneBests = new int[0];

        /// <summary>通算：遊んだゲームの数（最後まで遊んだものだけ）。</summary>
        public int games;

        /// <summary>通算：ストライクのレーンの数。</summary>
        public int strikes;

        /// <summary>通算：スペアのレーンの数。</summary>
        public int spares;

        /// <summary>通算：ガターの数（倒した本数が 0 の投の数。GUTTER... の表示と同じ）。</summary>
        public int gutters;

        /// <summary>最近の成績（新しいものが先頭）。</summary>
        public List<GameRecord> recent = new List<GameRecord>();
    }

    /// <summary>1ゲームを記録したときに更新したもの（結果画面の NEW RECORD! と BEST の印に使う）。</summary>
    public struct RecordUpdate
    {
        /// <summary>合計点の自己ベストを更新したか（前の記録があって、それを上回ったときだけ）。</summary>
        public bool newTotalBest;

        /// <summary>レーンごとの自己ベストを更新したか（前の記録があって、それを上回ったときだけ）。</summary>
        public bool[] newLaneBests;
    }

    /// <summary>RANK の決め方（段階6。結果画面と記録で同じものを使う）。MonoBehaviour に依存しない。</summary>
    public static class RankRule
    {
        /// <summary>合計÷満点を境目（S・A・B・C の順）と比べて S〜D を決める。</summary>
        public static string Decide(int total, int perfect, float[] thresholds)
        {
            float ratio = perfect > 0 ? total / (float)perfect : 0f;
            string[] names = { "S", "A", "B", "C" };
            for (int i = 0; thresholds != null && i < thresholds.Length && i < names.Length; i++)
            {
                if (ratio >= thresholds[i])
                {
                    return names[i];
                }
            }
            return "D";
        }
    }

    /// <summary>
    /// 記録帳の決まり（段階6）。MonoBehaviour にも PlayerPrefs にも依存しない（EditMode でテストする）。
    /// </summary>
    public static class RecordRules
    {
        /// <summary>今の形の版の番号。</summary>
        public const int CurrentVersion = 1;

        public const string KindStrike = "strike";
        public const string KindSpare = "spare";
        public const string KindGutter = "gutter";
        public const string KindOpen = "open";

        /// <summary>1レーンの記録を作る。</summary>
        public static LaneRecord MakeLane(int first, int second, int throws, int pinCount, int score)
        {
            first = Mathf.Max(0, first);
            second = throws >= 2 ? Mathf.Max(0, second) : 0;
            string kind = throws <= 1 && first >= pinCount ? KindStrike
                : throws >= 2 && first + second >= pinCount ? KindSpare
                : first + second == 0 ? KindGutter
                : KindOpen;
            return new LaneRecord { first = first, second = second, throws = Mathf.Max(1, throws), score = score, kind = kind };
        }

        /// <summary>このレーンでガター（倒した本数 0 の投）が何回あったか。</summary>
        public static int GutterThrows(LaneRecord lane)
        {
            if (lane == null)
            {
                return 0;
            }
            int n = lane.first == 0 ? 1 : 0;
            if (lane.throws >= 2 && lane.second == 0)
            {
                n++;
            }
            return n;
        }

        /// <summary>
        /// 1ゲームを記録帳に足す。自己ベスト・レーンごとの自己ベスト・通算・最近の成績を更新し、
        /// 最近の成績は keep を超えたら古いものから消す。更新したものを返す。
        /// </summary>
        public static RecordUpdate Add(RecordBook book, GameRecord game, int keep)
        {
            Normalize(book, game.lanes != null ? game.lanes.Length : 0);
            var update = new RecordUpdate { newLaneBests = new bool[book.laneBests.Length] };

            bool hadBest = book.games > 0 && book.bestTotal >= 0;
            if (book.bestTotal < 0 || game.total > book.bestTotal)
            {
                update.newTotalBest = hadBest;
                book.bestTotal = game.total;
                book.bestRank = game.rank ?? "";
                book.bestPlayedAt = game.playedAt ?? "";
            }

            for (int i = 0; game.lanes != null && i < game.lanes.Length && i < book.laneBests.Length; i++)
            {
                LaneRecord lane = game.lanes[i];
                if (lane == null)
                {
                    continue;
                }
                if (book.laneBests[i] < 0 || lane.score > book.laneBests[i])
                {
                    update.newLaneBests[i] = book.laneBests[i] >= 0;
                    book.laneBests[i] = lane.score;
                }
                if (lane.kind == KindStrike) book.strikes++;
                if (lane.kind == KindSpare) book.spares++;
                book.gutters += GutterThrows(lane);
            }

            book.games++;
            book.recent.Insert(0, game);
            int max = Mathf.Max(0, keep);
            while (book.recent.Count > max)
            {
                book.recent.RemoveAt(book.recent.Count - 1);
            }
            return update;
        }

        /// <summary>記録帳の形をそろえる（足りない配列を足す。読み込んだ古い形のために）。</summary>
        public static void Normalize(RecordBook book, int laneCount)
        {
            if (book.recent == null)
            {
                book.recent = new List<GameRecord>();
            }
            book.recent.RemoveAll(g => g == null);
            int n = Mathf.Max(laneCount, book.laneBests != null ? book.laneBests.Length : 0);
            if (book.laneBests == null || book.laneBests.Length < n)
            {
                var bests = new int[n];
                for (int i = 0; i < n; i++)
                {
                    bests[i] = book.laneBests != null && i < book.laneBests.Length ? book.laneBests[i] : -1;
                }
                book.laneBests = bests;
            }
            if (book.bestRank == null) book.bestRank = "";
            if (book.bestPlayedAt == null) book.bestPlayedAt = "";
        }

        /// <summary>記録帳を JSON にする。</summary>
        public static string ToJson(RecordBook book)
        {
            book.version = CurrentVersion;
            return JsonUtility.ToJson(book);
        }

        /// <summary>
        /// JSON から記録帳を読む。空・壊れている・知らない先の版なら、空の記録帳を返す（ゲームを止めない）。
        /// ok には読めたかどうかを返す（空の文字列は「まだ記録が無い」なので true）。
        /// </summary>
        public static RecordBook FromJson(string json, int laneCount, out bool ok)
        {
            ok = true;
            RecordBook book = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    book = JsonUtility.FromJson<RecordBook>(json);
                }
                catch (System.Exception)
                {
                    book = null;
                }
                if (book == null || book.version < 1 || book.version > CurrentVersion)
                {
                    ok = false;
                    book = null;
                }
            }
            if (book == null)
            {
                book = new RecordBook();
            }
            Normalize(book, laneCount);
            return book;
        }
    }
}
