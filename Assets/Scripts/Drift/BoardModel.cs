using System.Collections.Generic;

namespace StarCard.Drift
{
    /// <summary>
    /// 棋盘数据层：4×7 = 28 个空位，每个空位最多一张牌。
    /// 只管“谁在哪”，不管连结/归位（那是 LinkResolver 的事）。
    /// </summary>
    public class BoardModel
    {
        public readonly int Rows;
        public readonly int Cols;

        private readonly Core.StarCard[] _cards;
        private readonly bool[] _occupied;
        private readonly int[] _blockedTurns; // >0 表示被「虚空吞噬」封锁的剩余回合数

        public BoardModel(int rows = 4, int cols = 7)
        {
            Rows = rows;
            Cols = cols;
            int n = rows * cols;
            _cards = new Core.StarCard[n];
            _occupied = new bool[n];
            _blockedTurns = new int[n];
        }

        public int SlotCount => Rows * Cols;
        private int Index(GridPos p) => p.Row * Cols + p.Col;

        public bool InBounds(GridPos p) => p.Row >= 0 && p.Row < Rows && p.Col >= 0 && p.Col < Cols;
        public bool HasCard(GridPos p) => InBounds(p) && _occupied[Index(p)];
        public bool IsBlocked(GridPos p) => InBounds(p) && _blockedTurns[Index(p)] > 0;
        public int BlockedTurns(GridPos p) => InBounds(p) ? _blockedTurns[Index(p)] : 0;
        /// <summary>可以落牌的空位：在界内、没牌、没被封锁。</summary>
        public bool IsFree(GridPos p) => InBounds(p) && !_occupied[Index(p)] && _blockedTurns[Index(p)] == 0;

        public Core.StarCard GetCard(GridPos p) => _cards[Index(p)];

        public int CardCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _occupied.Length; i++) if (_occupied[i]) n++;
                return n;
            }
        }

        public IEnumerable<KeyValuePair<GridPos, Core.StarCard>> AllCards()
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var p = new GridPos(r, c);
                    if (_occupied[Index(p)])
                        yield return new KeyValuePair<GridPos, Core.StarCard>(p, _cards[Index(p)]);
                }
            }
        }

        public List<GridPos> FreeSlots()
        {
            var list = new List<GridPos>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var p = new GridPos(r, c);
                    if (IsFree(p)) list.Add(p);
                }
            return list;
        }

        public List<GridPos> PositionsOfDirection(Core.Direction dir)
        {
            var list = new List<GridPos>();
            foreach (var kv in AllCards())
                if (kv.Value.Direction == dir) list.Add(kv.Key);
            return list;
        }

        public bool Place(GridPos p, Core.StarCard card)
        {
            if (!IsFree(p)) return false;
            _cards[Index(p)] = card;
            _occupied[Index(p)] = true;
            return true;
        }

        public bool Remove(GridPos p, out Core.StarCard card)
        {
            card = default;
            if (!HasCard(p)) return false;
            card = _cards[Index(p)];
            _occupied[Index(p)] = false;
            _cards[Index(p)] = default;
            return true;
        }

        public bool Move(GridPos from, GridPos to)
        {
            if (!HasCard(from) || !IsFree(to)) return false;
            Remove(from, out var card);
            Place(to, card);
            return true;
        }

        public bool Swap(GridPos a, GridPos b)
        {
            if (!HasCard(a) || !HasCard(b) || a == b) return false;
            var ca = _cards[Index(a)];
            _cards[Index(a)] = _cards[Index(b)];
            _cards[Index(b)] = ca;
            return true;
        }

        public void Block(GridPos p, int turns)
        {
            if (!InBounds(p)) return;
            _blockedTurns[Index(p)] = turns;
        }

        /// <summary>每回合末调用一次，封锁倒计时 -1。</summary>
        public void TickBlocks()
        {
            for (int i = 0; i < _blockedTurns.Length; i++)
                if (_blockedTurns[i] > 0) _blockedTurns[i]--;
        }

        public void Clear()
        {
            for (int i = 0; i < _occupied.Length; i++)
            {
                _occupied[i] = false;
                _cards[i] = default;
                _blockedTurns[i] = 0;
            }
        }
    }
}
