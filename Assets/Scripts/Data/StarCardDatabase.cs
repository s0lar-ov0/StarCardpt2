using System.Collections.Generic;
using StarCard.Core;

namespace StarCard.Data
{
    public static class StarCardDatabase
    {
        private static readonly MansionName[] EastGroup =
        {
            MansionName.Jiao, MansionName.Kang, MansionName.Di, MansionName.Fang,
            MansionName.Xin,  MansionName.Wei1, MansionName.Ji
        };
        private static readonly MansionName[] NorthGroup =
        {
            MansionName.Dou,  MansionName.Niu,  MansionName.Nyu, MansionName.Xu,
            MansionName.Wei2, MansionName.Shi,  MansionName.Bi1
        };
        private static readonly MansionName[] WestGroup =
        {
            MansionName.Kui,  MansionName.Lou, MansionName.Wei3, MansionName.Mao,
            MansionName.Bi2,  MansionName.Zi,  MansionName.Shen
        };
        private static readonly MansionName[] SouthGroup =
        {
            MansionName.Jing, MansionName.Gui,   MansionName.Liu, MansionName.Xing,
            MansionName.Zhang,MansionName.Yi,    MansionName.Zhen
        };

        private static readonly Element[] ElementCycle =
        {
            Element.Wood,  Element.Metal, Element.Earth, Element.Sun,
            Element.Moon,  Element.Fire,  Element.Water
        };

        private static readonly Dictionary<MansionName, string> ChineseName = new()
        {
            { MansionName.Jiao, "角" }, { MansionName.Kang, "亢" }, { MansionName.Di, "氐" },
            { MansionName.Fang, "房" }, { MansionName.Xin, "心" },  { MansionName.Wei1, "尾" },
            { MansionName.Ji,   "箕" },
            { MansionName.Dou,  "斗" }, { MansionName.Niu,  "牛" }, { MansionName.Nyu, "女" },
            { MansionName.Xu,   "虚" }, { MansionName.Wei2, "危" }, { MansionName.Shi, "室" },
            { MansionName.Bi1,  "壁" },
            { MansionName.Kui,  "奎" }, { MansionName.Lou,  "娄" }, { MansionName.Wei3,"胃" },
            { MansionName.Mao,  "昴" }, { MansionName.Bi2,  "毕" }, { MansionName.Zi,  "觜" },
            { MansionName.Shen, "参" },
            { MansionName.Jing, "井" }, { MansionName.Gui,  "鬼" }, { MansionName.Liu, "柳" },
            { MansionName.Xing, "星" }, { MansionName.Zhang,"张" }, { MansionName.Yi,  "翼" },
            { MansionName.Zhen, "轸" }
        };

        private static readonly Dictionary<Direction, string> ChineseDirection = new()
        {
            { Direction.East,  "东" }, { Direction.North, "北" },
            { Direction.West,  "西" }, { Direction.South, "南" }
        };

        private static readonly Dictionary<Element, string> ChineseElement = new()
        {
            { Element.Metal, "金" }, { Element.Wood,  "木" }, { Element.Water, "水" },
            { Element.Fire,  "火" }, { Element.Earth, "土" }, { Element.Sun,   "日" },
            { Element.Moon,  "月" }
        };

        public static IReadOnlyList<Core.StarCard> BuildFullDeck()
        {
            var list = new List<Core.StarCard>(28);
            AddGroup(list, EastGroup,  Direction.East);
            AddGroup(list, NorthGroup, Direction.North);
            AddGroup(list, WestGroup,  Direction.West);
            AddGroup(list, SouthGroup, Direction.South);
            return list;
        }

        private static void AddGroup(List<Core.StarCard> list, MansionName[] group, Direction dir)
        {
            for (int i = 0; i < group.Length; i++)
            {
                list.Add(new Core.StarCard(group[i], dir, ElementCycle[i % ElementCycle.Length]));
            }
        }

        public static string GetChineseName(MansionName n) => ChineseName[n];
        public static string GetChineseDirection(Direction d) => ChineseDirection[d];
        public static string GetChineseElement(Element e) => ChineseElement[e];

        public static IReadOnlyList<MansionName> GetMansionsOfDirection(Direction d) => d switch
        {
            Direction.East  => EastGroup,
            Direction.North => NorthGroup,
            Direction.West  => WestGroup,
            Direction.South => SouthGroup,
            _ => System.Array.Empty<MansionName>()
        };
    }
}
