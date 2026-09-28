using System.Collections.Generic;

namespace CrazyBowling.Core
{
    /// <summary>DJ のラジオ番組で次に流すもの。</summary>
    public enum DjItemKind
    {
        /// <summary>コーナー（提供・天気予報・ニュースなど）。</summary>
        Corner,

        /// <summary>ID（局名のお知らせ）。コーナーとコーナーの間に挟む。</summary>
        Id,
    }

    /// <summary>DJ のラジオ番組の1つぶん。</summary>
    public struct DjItem
    {
        public DjItemKind kind;

        /// <summary>コーナーなら 0〜コーナーの数−1、ID なら 0〜ID の数−1。</summary>
        public int index;

        /// <summary>コーナーの何周目か（1から数える）。ID では直前のコーナーの周。</summary>
        public int cycle;
    }

    /// <summary>
    /// DJ のラジオ番組の並びを決める（段階6）。MonoBehaviour に依存しない。
    /// コーナー → ID → コーナー → ID … を繰り返す。
    /// コーナーは並べ替えた順に流し、全部を1周するまで同じコーナーは流さない。1周したら並べ替えてもう1周
    /// （並べ替えた最初が、前の周の最後と同じにならないようにする）。ID は順番に使う。
    /// </summary>
    public class DjProgram
    {
        private readonly int _cornerCount;
        private readonly int _idCount;
        private readonly System.Random _random;
        private readonly List<int> _order = new List<int>();
        private int _position;
        private int _idIndex;
        private bool _nextIsCorner = true;
        private int _lastCorner = -1;

        /// <summary>今のコーナーの周（1から数える）。</summary>
        public int Cycle { get; private set; }

        /// <summary>今の周のコーナーの並び（確かめるとき用）。</summary>
        public IReadOnlyList<int> Order => _order;

        /// <param name="cornerCount">コーナーの数。</param>
        /// <param name="idCount">ID の数（0 なら ID を挟まない）。</param>
        /// <param name="seed">並べ替えの乱数の種。</param>
        /// <param name="startInMiddle">true なら、並びの途中から始める（ゲームを始めるたびに違う所から）。</param>
        public DjProgram(int cornerCount, int idCount, int seed, bool startInMiddle)
        {
            _cornerCount = System.Math.Max(0, cornerCount);
            _idCount = System.Math.Max(0, idCount);
            _random = new System.Random(seed);
            Shuffle();
            if (startInMiddle && _cornerCount > 0)
            {
                _position = _random.Next(_cornerCount);
                _idIndex = _idCount > 0 ? _random.Next(_idCount) : 0;
                _nextIsCorner = _idCount == 0 || _random.Next(2) == 0;
            }
        }

        /// <summary>次に流すものを決めて進める。コーナーが無ければ ID だけを順番に返す。</summary>
        public DjItem Next()
        {
            bool corner = _cornerCount > 0 && (_nextIsCorner || _idCount == 0);
            if (_idCount == 0 && _cornerCount == 0)
            {
                return new DjItem { kind = DjItemKind.Id, index = -1, cycle = Cycle };
            }

            if (corner)
            {
                if (_position >= _order.Count)
                {
                    Shuffle();
                }
                int index = _order[_position++];
                _lastCorner = index;
                _nextIsCorner = false;
                return new DjItem { kind = DjItemKind.Corner, index = index, cycle = Cycle };
            }

            int id = _idIndex % _idCount;
            _idIndex = (_idIndex + 1) % _idCount;
            _nextIsCorner = true;
            return new DjItem { kind = DjItemKind.Id, index = id, cycle = Cycle };
        }

        /// <summary>コーナーを並べ替えて、新しい周を始める。</summary>
        private void Shuffle()
        {
            _order.Clear();
            for (int i = 0; i < _cornerCount; i++)
            {
                _order.Add(i);
            }
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
            // 前の周の最後と同じコーナーで始めない（続けて同じものが流れないように）
            if (_order.Count > 1 && _order[0] == _lastCorner)
            {
                int j = 1 + _random.Next(_order.Count - 1);
                (_order[0], _order[j]) = (_order[j], _order[0]);
            }
            _position = 0;
            Cycle++;
        }
    }
}
