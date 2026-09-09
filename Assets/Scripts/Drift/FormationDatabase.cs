using System.Collections.Generic;
using StarCard.Core;

namespace StarCard.Drift
{
    /// <summary>
    /// 四方位的“特殊阵型”模板。每个模板恰好 7 格 —— 与该方位的 7 宿一一对应。
    /// 模板可平移、默认不可旋转（拿到「天枢·镜位」祝福后额外允许 180° 点对称摆放）。
    /// 需要改形状就直接改下面的字符画：'X' 是阵型格，'.' 是空格。
    /// </summary>
    public static class FormationDatabase
    {
        // 青龙（东）3x7
        private static readonly string[] EastShape =
        {
            "X......",
            ".XX.XX.",
            "...X..X"
        };

        // 玄武（北）2x4
        private static readonly string[] NorthShape =
        {
            ".XXX",
            "XXXX"
        };

        // 白虎（西）3x5
        private static readonly string[] WestShape =
        {
            "....X",
            "XXXX.",
            ".X.X."
        };

        // 朱雀（南）4x4
        private static readonly string[] SouthShape =
        {
            "X.XX",
            ".XX.",
            "..X.",
            "...X"
        };

        private static readonly Dictionary<Direction, GridPos[]> Templates = new();
        private static readonly Dictionary<Direction, List<GridPos[]>> PlacementCache = new();
        private static readonly Dictionary<Direction, List<GridPos[]>> RotatedPlacementCache = new();

        static FormationDatabase()
        {
            Templates[Direction.East] = Parse(EastShape);
            Templates[Direction.North] = Parse(NorthShape);
            Templates[Direction.West] = Parse(WestShape);
            Templates[Direction.South] = Parse(SouthShape);
        }

        private static GridPos[] Parse(string[] shape)
        {
            var list = new List<GridPos>();
            for (int r = 0; r < shape.Length; r++)
            {
                for (int c = 0; c < shape[r].Length; c++)
                {
                    if (shape[r][c] == 'X') list.Add(new GridPos(r, c));
                }
            }
            return list.ToArray();
        }

        /// <summary>模板原型（左上角对齐到 (0,0)）。仅用于图鉴显示。</summary>
        public static GridPos[] GetTemplate(Direction dir) => Templates[dir];

        public static string[] GetShapeRows(Direction dir) => dir switch
        {
            Direction.East => EastShape,
            Direction.North => NorthShape,
            Direction.West => WestShape,
            Direction.South => SouthShape,
            _ => new[] { "" }
        };

        /// <summary>该方位所有合法摆放（平移后完全落在棋盘内）。allowRotation 时追加 180° 版本。</summary>
        public static IReadOnlyList<GridPos[]> GetPlacements(Direction dir, int rows, int cols, bool allowRotation)
        {
            var cache = allowRotation ? RotatedPlacementCache : PlacementCache;
            if (cache.TryGetValue(dir, out var cached)) return cached;

            var result = new List<GridPos[]>();
            AddTranslations(result, Templates[dir], rows, cols);
            if (allowRotation) AddTranslations(result, Rotate180(Templates[dir]), rows, cols);
            cache[dir] = result;
            return result;
        }

        private static void AddTranslations(List<GridPos[]> result, GridPos[] template, int rows, int cols)
        {
            int maxRow = 0, maxCol = 0;
            foreach (var p in template)
            {
                if (p.Row > maxRow) maxRow = p.Row;
                if (p.Col > maxCol) maxCol = p.Col;
            }

            for (int dr = 0; dr + maxRow < rows; dr++)
            {
                for (int dc = 0; dc + maxCol < cols; dc++)
                {
                    var placement = new GridPos[template.Length];
                    for (int i = 0; i < template.Length; i++)
                        placement[i] = template[i].Offset(dr, dc);
                    result.Add(placement);
                }
            }
        }

        private static GridPos[] Rotate180(GridPos[] template)
        {
            int maxRow = 0, maxCol = 0;
            foreach (var p in template)
            {
                if (p.Row > maxRow) maxRow = p.Row;
                if (p.Col > maxCol) maxCol = p.Col;
            }
            var res = new GridPos[template.Length];
            for (int i = 0; i < template.Length; i++)
                res[i] = new GridPos(maxRow - template[i].Row, maxCol - template[i].Col);
            return res;
        }

        /// <summary>缓存与棋盘尺寸相关，改尺寸时调用。</summary>
        public static void ClearCache()
        {
            PlacementCache.Clear();
            RotatedPlacementCache.Clear();
        }
    }
}
