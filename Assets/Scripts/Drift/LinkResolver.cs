using System.Collections.Generic;
using StarCard.Core;

namespace StarCard.Drift
{
    /// <summary>一次连结判定的结果。</summary>
    public class LinkState
    {
        /// <summary>所有处于连结状态的格子。</summary>
        public readonly HashSet<GridPos> LinkedCells = new();

        /// <summary>本次判定中达成“归位”的方位（该方位 7 宿完整填满某个阵型摆放）。</summary>
        public readonly List<Direction> Homecoming = new();

        /// <summary>各方位当前最佳摆放命中的牌数（图鉴/提示用）。</summary>
        public readonly Dictionary<Direction, int> BestCount = new();

        /// <summary>各方位最佳摆放的格子集合（高亮提示用）。</summary>
        public readonly Dictionary<Direction, GridPos[]> BestPlacement = new();

        public bool IsLinked(GridPos p) => LinkedCells.Contains(p);
    }

    public static class LinkResolver
    {
        /// <param name="minLink">构成连结所需的最少同方位牌数（默认 3，天璇·连心 降为 2）。</param>
        /// <param name="allowRotation">是否允许阵型 180° 摆放（天枢·镜位）。</param>
        /// <param name="skipDirections">已归位的方位不再参与判定。</param>
        public static LinkState Resolve(BoardModel board, int minLink, bool allowRotation,
                                        ICollection<Direction> skipDirections = null)
        {
            var state = new LinkState();

            foreach (Direction dir in System.Enum.GetValues(typeof(Direction)))
            {
                if (skipDirections != null && skipDirections.Contains(dir)) continue;

                var occupied = new HashSet<GridPos>(board.PositionsOfDirection(dir));
                state.BestCount[dir] = 0;
                if (occupied.Count == 0) continue;

                var placements = FormationDatabase.GetPlacements(dir, board.Rows, board.Cols, allowRotation);
                foreach (var placement in placements)
                {
                    int hit = 0;
                    for (int i = 0; i < placement.Length; i++)
                        if (occupied.Contains(placement[i])) hit++;

                    if (hit > state.BestCount[dir])
                    {
                        state.BestCount[dir] = hit;
                        state.BestPlacement[dir] = placement;
                    }

                    if (hit >= minLink)
                    {
                        for (int i = 0; i < placement.Length; i++)
                            if (occupied.Contains(placement[i])) state.LinkedCells.Add(placement[i]);
                    }

                    // 阵型 7 格全被本方位的牌占满 → 归位
                    if (hit == placement.Length && !state.Homecoming.Contains(dir))
                        state.Homecoming.Add(dir);
                }
            }

            return state;
        }
    }
}
