using System.Collections.Generic;

namespace StarCard.Drift
{
    /// <summary>
    /// 随机事件。全部是**棋盘整体变换**，等概率抽取。
    /// 变换作用于棋盘上所有牌 —— **包括已连结的**；变换后重算连结，
    /// 可能凭空多出连结（甚至归位），也可能让原有连结失效。
    /// </summary>
    public enum RandomEventId
    {
        ShiftLeft,          // 整体左平移，最左列绕到最右
        ShiftRight,         // 整体右平移
        ShiftUp,            // 整体上平移，最上行绕到最下
        ShiftDown,          // 整体下平移
        RotateClockwise,    // 随机一个九宫格，外圈顺时针转一格，中心不动
        RotateCounter,      // 同上，逆时针
        MirrorVertical,     // 上下对称：上两行与下两行互换
        MirrorHorizontal    // 左右对称：正中列不动，其余左右镜像
    }

    public class RandomEventDef
    {
        public RandomEventId Id;
        public string Name;
        public string Desc;

        public RandomEventDef(RandomEventId id, string name, string desc)
        {
            Id = id;
            Name = name;
            Desc = desc;
        }
    }

    public static class RandomEventDatabase
    {
        private static readonly List<RandomEventDef> All = new()
        {
            new RandomEventDef(RandomEventId.ShiftLeft,  "星河西流", "棋盘整体左移一列，最左一列绕回最右"),
            new RandomEventDef(RandomEventId.ShiftRight, "星河东流", "棋盘整体右移一列，最右一列绕回最左"),
            new RandomEventDef(RandomEventId.ShiftUp,    "天穹上引", "棋盘整体上移一行，最上一行绕回最下"),
            new RandomEventDef(RandomEventId.ShiftDown,  "天穹下沉", "棋盘整体下移一行，最下一行绕回最上"),
            new RandomEventDef(RandomEventId.RotateClockwise, "斗柄顺旋", "随机一处九宫格，外围八格顺时针转一格，中心不动"),
            new RandomEventDef(RandomEventId.RotateCounter,   "斗柄逆旋", "随机一处九宫格，外围八格逆时针转一格，中心不动"),
            new RandomEventDef(RandomEventId.MirrorVertical,  "天地翻覆", "上下对称：上两行与下两行整体互换"),
            new RandomEventDef(RandomEventId.MirrorHorizontal,"左右倒悬", "左右对称：正中一列不动，其余左右镜像")
        };

        public static IReadOnlyList<RandomEventDef> AllDefs => All;

        public static RandomEventDef Get(RandomEventId id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>等概率抽一个。</summary>
        public static RandomEventDef Roll(System.Random rng) => All[rng.Next(All.Count)];
    }
}
