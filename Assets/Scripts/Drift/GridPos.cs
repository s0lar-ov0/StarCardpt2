using System;

namespace StarCard.Drift
{
    /// <summary>棋盘坐标。Row 从上到下 0..Rows-1，Col 从左到右 0..Cols-1。</summary>
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        public int Row;
        public int Col;

        public GridPos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public static GridPos Invalid => new GridPos(-1, -1);
        public bool IsValid => Row >= 0 && Col >= 0;

        public GridPos Offset(int dRow, int dCol) => new GridPos(Row + dRow, Col + dCol);

        public bool Equals(GridPos other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is GridPos p && Equals(p);
        public override int GetHashCode() => Row * 397 ^ Col;
        public override string ToString() => $"({Row},{Col})";

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);
    }
}
