using UnityEngine;
using StarCard.Core;

namespace StarCard.Drift
{
    public enum MeteorSize
    {
        /// <summary>大型白色，速度慢：行动次数 +1</summary>
        Big,
        /// <summary>中型有色，速度中：给对应属性的一张卡池星宿牌</summary>
        Medium,
        /// <summary>小型粉色，速度快：给一个随机众星祝福</summary>
        Small
    }

    /// <summary>一颗流星的定义（大小 + 属性）。</summary>
    public struct MeteorKind
    {
        public MeteorSize Size;
        /// <summary>仅 Medium 有意义。</summary>
        public Element Element;

        public MeteorKind(MeteorSize size, Element element = Element.Metal)
        {
            Size = size;
            Element = element;
        }
    }

    public static class MeteorPalette
    {
        // 中型流星：金/青/蓝/红/棕/暗黄/灰 ←→ 金/木/水/火/土/日/月
        public static Color ColorOf(Element e) => e switch
        {
            Element.Metal => new Color(1.00f, 0.84f, 0.35f), // 金
            Element.Wood  => new Color(0.35f, 0.85f, 0.60f), // 青
            Element.Water => new Color(0.38f, 0.66f, 1.00f), // 蓝
            Element.Fire  => new Color(1.00f, 0.38f, 0.32f), // 红
            Element.Earth => new Color(0.66f, 0.45f, 0.26f), // 棕
            Element.Sun   => new Color(0.78f, 0.70f, 0.20f), // 暗黄
            Element.Moon  => new Color(0.72f, 0.75f, 0.80f), // 灰
            _ => Color.white
        };

        public static Color ColorOf(MeteorKind kind) => kind.Size switch
        {
            MeteorSize.Big => Color.white,
            MeteorSize.Small => new Color(1.00f, 0.55f, 0.80f), // 粉
            _ => ColorOf(kind.Element)
        };

        public static Color ColorOfDirection(Direction d) => d switch
        {
            Direction.East  => new Color(0.36f, 0.82f, 0.55f), // 青龙
            Direction.North => new Color(0.45f, 0.60f, 0.95f), // 玄武
            Direction.West  => new Color(0.90f, 0.90f, 0.95f), // 白虎
            Direction.South => new Color(0.96f, 0.45f, 0.42f), // 朱雀
            _ => Color.white
        };
    }
}
