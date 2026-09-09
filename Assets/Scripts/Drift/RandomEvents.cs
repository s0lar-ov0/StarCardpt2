using System.Collections.Generic;

namespace StarCard.Drift
{
    public enum RandomEventId
    {
        StardustSquall,   // 星尘骤起：立刻额外漂移一次
        MeteorImpact,     // 陨石撞击：一张非连结牌被打回卡池
        OrbitPull,        // 星轨牵引：一张非连结牌被拉到能连结的位置
        Gift,             // 天赐流光：卡池抽 1 张到手牌
        VoidBite,         // 虚空吞噬：一个空位被封锁 2 回合
        SkyReverse,       // 星象逆转：本回合行动次数 +2
        StarTide          // 星潮涌动：所有非连结牌朝同一方向平移 1 格
    }

    public class RandomEventDef
    {
        public RandomEventId Id;
        public string Name;
        public string Desc;
        public int Weight;

        public RandomEventDef(RandomEventId id, string name, string desc, int weight)
        {
            Id = id;
            Name = name;
            Desc = desc;
            Weight = weight;
        }
    }

    public static class RandomEventDatabase
    {
        private static readonly List<RandomEventDef> All = new()
        {
            new RandomEventDef(RandomEventId.StardustSquall, "星尘骤起", "棋盘上所有非连结的星宿牌立刻漂移一次", 18),
            new RandomEventDef(RandomEventId.MeteorImpact,   "陨石撞击", "随机一张非连结的星宿牌被击回卡池", 14),
            new RandomEventDef(RandomEventId.OrbitPull,      "星轨牵引", "随机一张非连结牌被拉入能构成连结的空位", 14),
            new RandomEventDef(RandomEventId.Gift,           "天赐流光", "从卡池随机抽 1 张星宿牌到手牌", 16),
            new RandomEventDef(RandomEventId.VoidBite,       "虚空吞噬", "随机 1 个空位被封锁 2 回合，期间不可落牌", 12),
            new RandomEventDef(RandomEventId.SkyReverse,     "星象逆转", "本回合剩余行动次数 +2", 14),
            new RandomEventDef(RandomEventId.StarTide,       "星潮涌动", "所有非连结牌朝同一随机方向平移 1 格", 12)
        };

        public static IReadOnlyList<RandomEventDef> AllDefs => All;

        public static RandomEventDef Get(RandomEventId id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static RandomEventDef Roll(System.Random rng)
        {
            int total = 0;
            for (int i = 0; i < All.Count; i++) total += All[i].Weight;
            int roll = rng.Next(total);
            for (int i = 0; i < All.Count; i++)
            {
                roll -= All[i].Weight;
                if (roll < 0) return All[i];
            }
            return All[0];
        }
    }
}
