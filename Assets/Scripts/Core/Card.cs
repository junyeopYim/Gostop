namespace Hwatu.Core
{
    public enum CardType
    {
        Gwang,
        Yeol,
        Tti,
        Pi
    }

    public enum RibbonColor
    {
        None,
        Hong,
        Cheong,
        Cho
    }

    public sealed class Card
    {
        public int Id { get; }
        public int Month { get; }
        public CardType Type { get; }
        public RibbonColor RibbonColor { get; }
        public bool IsGodoriBird { get; }
        public int PiValue { get; }
        public string DebugName { get; }

        public Card(int id, int month, CardType type, RibbonColor ribbonColor,
                    bool isGodoriBird, int piValue, string debugName)
        {
            Id = id;
            Month = month;
            Type = type;
            RibbonColor = ribbonColor;
            IsGodoriBird = isGodoriBird;
            PiValue = piValue;
            DebugName = debugName;
        }

        public override string ToString() => DebugName;
    }
}
