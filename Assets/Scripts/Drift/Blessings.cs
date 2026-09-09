using System.Collections.Generic;
using StarCard.Core;

namespace StarCard.Drift
{
    /// <summary>众星祝福（小型粉色流星掉落，同时最多持有 3 个）。</summary>
    public enum BlessingId
    {
        None = 0,
        YaoGuangStride,   // 摇光-续行
        TianJiWeave,      // 天玑-织星
        YuHengGather,     // 玉衡-聚灵
        KaiYangSteady,    // 开阳-定星
        TianXuanHeart,    // 天璇-连心
        TianShuMirror,    // 天枢-镜位
        TianQuanShift,    // 天权-移山
        YaoGuangGaze      // 瑶光-延目
    }

    public class BlessingDef
    {
        public BlessingId Id;
        public string Name;
        public string Desc;

        public BlessingDef(BlessingId id, string name, string desc)
        {
            Id = id;
            Name = name;
            Desc = desc;
        }
    }

    public static class BlessingDatabase
    {
        private static readonly List<BlessingDef> All = new()
        {
            new BlessingDef(BlessingId.YaoGuangStride, "摇光-续行", "每回合行动次数 +1"),
            new BlessingDef(BlessingId.TianJiWeave,    "天玑-织星", "漂移时，每张非连结牌有 50% 概率原地不动"),
            new BlessingDef(BlessingId.YuHengGather,   "玉衡-聚灵", "每回合棋盘操作开始时，从卡池抽 1 张牌到手牌"),
            new BlessingDef(BlessingId.KaiYangSteady,  "开阳-定星", "随机事件的触发间隔 +1 次行动"),
            new BlessingDef(BlessingId.TianXuanHeart,  "天璇-连心", "连结所需的最少同方位牌数由 3 降为 2"),
            new BlessingDef(BlessingId.TianShuMirror,  "天枢-镜位", "阵型模板额外允许 180° 旋转摆放"),
            new BlessingDef(BlessingId.TianQuanShift,  "天权-移山", "每回合首次“移动”行动不消耗行动次数"),
            new BlessingDef(BlessingId.YaoGuangGaze,   "瑶光-延目", "流星定位阶段时长 +4 秒")
        };

        public static IReadOnlyList<BlessingDef> AllDefs => All;

        public static BlessingDef Get(BlessingId id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static string GetName(BlessingId id) => Get(id)?.Name ?? "?";
        public static string GetDesc(BlessingId id) => Get(id)?.Desc ?? "";

        /// <summary>抽一个玩家还没有（也不在待选列表里）的祝福。全都有了返回 None。</summary>
        public static BlessingId RollNew(System.Random rng, ICollection<BlessingId> exclude)
        {
            var pool = new List<BlessingId>();
            for (int i = 0; i < All.Count; i++)
                if (exclude == null || !exclude.Contains(All[i].Id)) pool.Add(All[i].Id);
            if (pool.Count == 0) return BlessingId.None;
            return pool[rng.Next(pool.Count)];
        }
    }

    /// <summary>方位祝福（该方位“归位”后永久获得）。</summary>
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
            Direction.East => "青龙-奋鳞",
            Direction.North => "玄武-镇渊",
            Direction.West => "白虎-啸风",
            Direction.South => "朱雀-衔火",
            _ => "?"
        };

        public static string GetDesc(Direction dir) => dir switch
        {
            Direction.East => "行动次数 +2",
            Direction.North => "漂移时随机半数非连结牌原地不动",
            Direction.West => "流星定位阶段时长 +5 秒",
            Direction.South => "每回合棋盘操作开始时，从卡池抽 1 张牌到手牌",
            _ => ""
        };
    }
}
