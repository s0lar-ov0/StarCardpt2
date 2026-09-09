using System.Collections.Generic;
using StarCard.Core;

namespace StarCard.Drift
{
    /// <summary>众星祝福（点小型粉色流星获得，同时最多持有 3 个，各自可升级）。</summary>
    public enum BlessingId
    {
        None = 0,
        PoJun,      // 破军：流星移动速度减慢
        TanLang,    // 贪狼：流星定位限时 +
        JuMen,      // 巨门：流星体型变大
        WenQu,      // 文曲：随机事件的触发间隔 +
        WuQu,       // 武曲：回合结束有概率不漂移
        LuCun,      // 禄存：每回合行动次数 +
        LianZhen    // 廉贞：总回合数 +
    }

    public class BlessingDef
    {
        public BlessingId Id;
        public string Name;

        /// <summary>等级上限。</summary>
        public int MaxLevel;

        private readonly string _descFormat;
        private readonly System.Func<int, string> _valueAt;

        /// <param name="descFormat">描述模板，{0} 替换成该等级的实际数值</param>
        /// <param name="valueAt">等级 → 效果数值</param>
        public BlessingDef(BlessingId id, string name, int maxLevel,
                           string descFormat, System.Func<int, string> valueAt)
        {
            Id = id;
            Name = name;
            MaxLevel = maxLevel;
            _descFormat = descFormat;
            _valueAt = valueAt;
        }

        /// <summary>某等级下的完整描述。</summary>
        public string DescAt(int level) => string.Format(_descFormat, _valueAt(level));
    }

    public static class BlessingDatabase
    {
        private static readonly List<BlessingDef> All = new()
        {
            new BlessingDef(BlessingId.PoJun,    "破军", 5,
                "流星移动速度减慢至 {0} 倍", lv => (1f - lv * 0.08f).ToString("0.##")),

            new BlessingDef(BlessingId.TanLang,  "贪狼", 5,
                "流星定位限时 +{0} 秒", lv => (2 * lv).ToString()),

            new BlessingDef(BlessingId.JuMen,    "巨门", 5,
                "流星体型变大至 {0} 倍", lv => (1f + lv * 0.16f).ToString("0.##")),

            new BlessingDef(BlessingId.WenQu,    "文曲", 2,
                "随机事件所需的行动数额外 +{0}", lv => lv.ToString()),

            new BlessingDef(BlessingId.WuQu,     "武曲", 2,
                "每回合结束时有 {0}% 概率不发生漂移", lv => (20 * lv).ToString()),

            new BlessingDef(BlessingId.LuCun,    "禄存", 3,
                "每回合获得的行动次数额外 +{0}", lv => lv.ToString()),

            new BlessingDef(BlessingId.LianZhen, "廉贞", 3,
                "总回合数额外 +{0}", lv => lv.ToString())
        };

        public static IReadOnlyList<BlessingDef> AllDefs => All;

        public static BlessingDef Get(BlessingId id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static string GetName(BlessingId id) => Get(id)?.Name ?? "?";
        public static int GetMaxLevel(BlessingId id) => Get(id)?.MaxLevel ?? 1;
        public static string GetDesc(BlessingId id, int level) => Get(id)?.DescAt(level) ?? "";

        /// <summary>抽一个 exclude 里没有的祝福。全都有了返回 None。</summary>
        public static BlessingId RollNew(System.Random rng, ICollection<BlessingId> exclude)
        {
            var pool = new List<BlessingId>();
            for (int i = 0; i < All.Count; i++)
                if (exclude == null || !exclude.Contains(All[i].Id)) pool.Add(All[i].Id);
            if (pool.Count == 0) return BlessingId.None;
            return pool[rng.Next(pool.Count)];
        }
    }

    /// <summary>玩家持有的一个祝福：id + 当前等级。</summary>
    [System.Serializable]
    public struct OwnedBlessing
    {
        public BlessingId Id;
        public int Level;

        public OwnedBlessing(BlessingId id, int level = 1)
        {
            Id = id;
            Level = level;
        }

        public int MaxLevel => BlessingDatabase.GetMaxLevel(Id);
        public bool IsMaxed => Level >= MaxLevel;
        public string Name => BlessingDatabase.GetName(Id);
        public string Desc => BlessingDatabase.GetDesc(Id, Level);

        /// <summary>「破军-3」这种带等级的标题。</summary>
        public string Title => $"{Name}-{Level}";
    }


    /// <summary>
    /// 方位祝福：该方位七宿「归位」后永久获得。
    /// 效果统一为**屏蔽一类随机事件** —— 抽到被屏蔽的事件时，该事件不触发。
    ///
    ///   青龙（东）→ 左右平移      白虎（西）→ 上下平移
    ///   朱雀（南）→ 九宫格旋转    玄武（北）→ 对称变换
    ///
    /// 八个随机事件正好被四个方位两两分掉，所以四方全归位时所有事件都失效
    /// —— 但那时已经通关了。
    /// </summary>
    public static class DirectionBlessing
    {
        public static string GetBeastName(Direction dir) => dir switch
        {
            Direction.East => "青龙",
            Direction.North => "玄武",
            Direction.West => "白虎",
            Direction.South => "朱雀",
            _ => "?"
        };

        public static string GetName(Direction dir) => dir switch
        {
            Direction.East => "青龙-镇河",
            Direction.North => "玄武-定衡",
            Direction.West => "白虎-锁枢",
            Direction.South => "朱雀-静斗",
            _ => "?"
        };

        public static string GetDesc(Direction dir) => dir switch
        {
            Direction.East => "左右平移的随机事件不再发生",
            Direction.North => "对称变换的随机事件不再发生",
            Direction.West => "上下平移的随机事件不再发生",
            Direction.South => "九宫格旋转的随机事件不再发生",
            _ => ""
        };

        /// <summary>该方位祝福是否屏蔽这个随机事件。</summary>
        public static bool Blocks(Direction dir, RandomEventId id) => dir switch
        {
            Direction.East  => id == RandomEventId.ShiftLeft || id == RandomEventId.ShiftRight,
            Direction.West  => id == RandomEventId.ShiftUp   || id == RandomEventId.ShiftDown,
            Direction.South => id == RandomEventId.RotateClockwise || id == RandomEventId.RotateCounter,
            Direction.North => id == RandomEventId.MirrorVertical  || id == RandomEventId.MirrorHorizontal,
            _ => false
        };
    }
}
