using System;

namespace StarCard.Core
{
    [Serializable]
    public struct StarCard : IEquatable<StarCard>
    {
        public MansionName Name;
        public Direction Direction;
        public Element Element;
        public int BasePoint;

        public const int DefaultBasePoint = 10;

        public StarCard(MansionName name, Direction direction, Element element, int basePoint = DefaultBasePoint)
        {
            Name = name;
            Direction = direction;
            Element = element;
            BasePoint = basePoint;
        }

        public bool Equals(StarCard other) =>
            Name == other.Name && Direction == other.Direction && Element == other.Element && BasePoint == other.BasePoint;

        public override bool Equals(object obj) => obj is StarCard c && Equals(c);

        public override int GetHashCode() =>
            ((int)Name * 397) ^ ((int)Direction * 31) ^ ((int)Element * 17) ^ BasePoint;

        public override string ToString() => $"{Name}({Direction}/{Element})";
    }
}
