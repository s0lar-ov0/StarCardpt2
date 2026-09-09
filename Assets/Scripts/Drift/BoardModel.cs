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

        // ---------- 整体变换（随机事件用） ----------
        //
        // 这些变换**作用于棋盘上所有牌，包括已连结的**，而且是"整体搬动"：
        // 用一张映射表一次性重排，不走 Move()。因为逐个 Move 会互相挡路，
        // 而且封锁格在整体变换里不构成障碍（牌是被"搬"过去的，不是走过去的）。
        //
        // srcOf(dst) 返回"变换后 dst 位置上的牌，原先在哪个位置"。

        private void Remap(System.Func<GridPos, GridPos> srcOf)
        {
            var newCards = new Core.StarCard[_cards.Length];
            var newOcc = new bool[_occupied.Length];

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    var dst = new GridPos(r, c);
                    var src = srcOf(dst);
                    if (!InBounds(src)) continue;
                    int si = Index(src), di = Index(dst);
                    if (!_occupied[si]) continue;
                    newCards[di] = _cards[si];
                    newOcc[di] = true;
                }
            }

            System.Array.Copy(newCards, _cards, _cards.Length);
            System.Array.Copy(newOcc, _occupied, _occupied.Length);
        }

        /// <summary>整体左平移一列，最左列绕到最右（环形）。</summary>
        public void ShiftLeft() =>
            Remap(d => new GridPos(d.Row, (d.Col + 1) % Cols));

        /// <summary>整体右平移一列，最右列绕到最左。</summary>
        public void ShiftRight() =>
            Remap(d => new GridPos(d.Row, (d.Col - 1 + Cols) % Cols));

        /// <summary>整体上平移一行，最上行绕到最下。</summary>
        public void ShiftUp() =>
            Remap(d => new GridPos((d.Row + 1) % Rows, d.Col));

        /// <summary>整体下平移一行，最下行绕到最上。</summary>
        public void ShiftDown() =>
            Remap(d => new GridPos((d.Row - 1 + Rows) % Rows, d.Col));

        /// <summary>
        /// 上下对称（镜像）：第 r 行与第 (Rows-1-r) 行互换。
        /// 4 行时即 行0↔行3、行1↔行2 —— "上两行与下两行对称"。
        /// 注意这不是"上半区整块搬到下半区"（那会是 行0→行2、行1→行3），
        /// 而是标准镜像，与左右对称的语义保持一致。
        /// </summary>
        public void MirrorVertical() =>
            Remap(d => new GridPos(Rows - 1 - d.Row, d.Col));

        /// <summary>
        /// 左右对称：最中间那一列不动，其余列左右镜像。
        /// 列数为偶数时没有"正中列"，退化成全部镜像。
        /// </summary>
        public void MirrorHorizontal()
        {
            int mid = Cols / 2;
            bool hasCenter = Cols % 2 == 1;
            Remap(d => (hasCenter && d.Col == mid) ? d : new GridPos(d.Row, Cols - 1 - d.Col));
        }

        /// <summary>
        /// 以 (top,left) 为左上角的 3x3 九宫格，外圈 8 格顺/逆时针旋转一格，中心不动。
        /// 返回 false 表示这个九宫格越界。
        /// </summary>
        public bool RotateBlock(int top, int left, bool clockwise)
        {
            if (top < 0 || left < 0 || top + 3 > Rows || left + 3 > Cols) return false;

            // 外圈按顺时针顺序：(0,0)(0,1)(0,2)(1,2)(2,2)(2,1)(2,0)(1,0)
            int[] dr = { 0, 0, 0, 1, 2, 2, 2, 1 };
            int[] dc = { 0, 1, 2, 2, 2, 1, 0, 0 };

            var ring = new Core.StarCard[8];
            var has = new bool[8];
            for (int i = 0; i < 8; i++)
            {
                var p = new GridPos(top + dr[i], left + dc[i]);
                has[i] = HasCard(p);
                if (has[i]) ring[i] = GetCard(p);
            }

            for (int i = 0; i < 8; i++)
            {
                // 顺时针：新位置 i 上放原来 i-1 的牌
                int from = clockwise ? (i - 1 + 8) % 8 : (i + 1) % 8;
                var p = new GridPos(top + dr[i], left + dc[i]);
                int idx = Index(p);
                _occupied[idx] = has[from];
                _cards[idx] = has[from] ? ring[from] : default;
            }
            return true;
        }

        /// <summary>所有合法的 3x3 九宫格左上角坐标。</summary>
        public List<GridPos> BlockOrigins()
        {
            var list = new List<GridPos>();
            for (int r = 0; r + 3 <= Rows; r++)
                for (int c = 0; c + 3 <= Cols; c++)
                    list.Add(new GridPos(r, c));
            return list;
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
