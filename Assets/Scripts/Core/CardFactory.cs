using System.Collections.Generic;

namespace Hwatu.Core
{
    public static class CardFactory
    {
        /// <summary>
        /// 표준 48장 덱을 생성한다. Id는 0~47, 월 순서대로 부여된다
        /// (m월의 카드는 Id (m-1)*4 ~ (m-1)*4+3).
        /// </summary>
        public static List<Card> CreateStandardDeck()
        {
            var cards = new List<Card>(48);
            int id = 0;

            void Add(int month, CardType type, RibbonColor ribbon = RibbonColor.None,
                     bool godoriBird = false, int piValue = 0)
            {
                string name = month + "월 " + NameOf(type, ribbon, piValue);
                cards.Add(new Card(id++, month, type, ribbon, godoriBird, piValue, name));
            }

            // 1월(송학): 광, 홍단, 피, 피
            Add(1, CardType.Gwang);
            Add(1, CardType.Tti, RibbonColor.Hong);
            Add(1, CardType.Pi, piValue: 1);
            Add(1, CardType.Pi, piValue: 1);

            // 2월(매조): 열끗[고도리새], 홍단, 피, 피
            Add(2, CardType.Yeol, godoriBird: true);
            Add(2, CardType.Tti, RibbonColor.Hong);
            Add(2, CardType.Pi, piValue: 1);
            Add(2, CardType.Pi, piValue: 1);

            // 3월(벚꽃): 광, 홍단, 피, 피
            Add(3, CardType.Gwang);
            Add(3, CardType.Tti, RibbonColor.Hong);
            Add(3, CardType.Pi, piValue: 1);
            Add(3, CardType.Pi, piValue: 1);

            // 4월(흑싸리): 열끗[고도리새], 초단, 피, 피
            Add(4, CardType.Yeol, godoriBird: true);
            Add(4, CardType.Tti, RibbonColor.Cho);
            Add(4, CardType.Pi, piValue: 1);
            Add(4, CardType.Pi, piValue: 1);

            // 5월(난초): 열끗, 초단, 피, 피
            Add(5, CardType.Yeol);
            Add(5, CardType.Tti, RibbonColor.Cho);
            Add(5, CardType.Pi, piValue: 1);
            Add(5, CardType.Pi, piValue: 1);

            // 6월(모란): 열끗, 청단, 피, 피
            Add(6, CardType.Yeol);
            Add(6, CardType.Tti, RibbonColor.Cheong);
            Add(6, CardType.Pi, piValue: 1);
            Add(6, CardType.Pi, piValue: 1);

            // 7월(홍싸리): 열끗, 초단, 피, 피
            Add(7, CardType.Yeol);
            Add(7, CardType.Tti, RibbonColor.Cho);
            Add(7, CardType.Pi, piValue: 1);
            Add(7, CardType.Pi, piValue: 1);

            // 8월(공산): 광, 열끗[고도리새], 피, 피
            Add(8, CardType.Gwang);
            Add(8, CardType.Yeol, godoriBird: true);
            Add(8, CardType.Pi, piValue: 1);
            Add(8, CardType.Pi, piValue: 1);

            // 9월(국화): 열끗, 청단, 피, 피
            Add(9, CardType.Yeol);
            Add(9, CardType.Tti, RibbonColor.Cheong);
            Add(9, CardType.Pi, piValue: 1);
            Add(9, CardType.Pi, piValue: 1);

            // 10월(단풍): 열끗, 청단, 피, 피
            Add(10, CardType.Yeol);
            Add(10, CardType.Tti, RibbonColor.Cheong);
            Add(10, CardType.Pi, piValue: 1);
            Add(10, CardType.Pi, piValue: 1);

            // 11월(오동): 광, 쌍피, 피, 피
            Add(11, CardType.Gwang);
            Add(11, CardType.Pi, piValue: 2);
            Add(11, CardType.Pi, piValue: 1);
            Add(11, CardType.Pi, piValue: 1);

            // 12월(비): 광(비광), 열끗, 띠(None), 쌍피
            Add(12, CardType.Gwang);
            Add(12, CardType.Yeol);
            Add(12, CardType.Tti);
            Add(12, CardType.Pi, piValue: 2);

            return cards;
        }

        private static string NameOf(CardType type, RibbonColor ribbon, int piValue)
        {
            switch (type)
            {
                case CardType.Gwang: return "광";
                case CardType.Yeol: return "열끗";
                case CardType.Tti:
                    switch (ribbon)
                    {
                        case RibbonColor.Hong: return "홍단";
                        case RibbonColor.Cheong: return "청단";
                        case RibbonColor.Cho: return "초단";
                        default: return "띠";
                    }
                default: return piValue >= 2 ? "쌍피" : "피";
            }
        }
    }
}
