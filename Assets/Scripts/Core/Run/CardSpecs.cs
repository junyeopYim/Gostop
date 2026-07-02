using System;
using System.Collections.Generic;
using Hwatu.Core;

namespace Hwatu.Run
{
    /// <summary>
    /// CardSpec ↔ Card 변환. 새 런의 초기 덱은 CardFactory 구성을 스펙으로 변환해 채우고,
    /// 판을 시작할 때마다 스펙 리스트에서 런타임 Card를 다시 만든다.
    /// </summary>
    public static class CardSpecs
    {
        /// <summary>표준 48장 구성을 스펙 리스트로 변환한다 (새 런의 초기 덱).</summary>
        public static List<CardSpec> CreateStandardDeckSpecs() => FromCards(CardFactory.CreateStandardDeck());

        public static CardSpec FromCard(Card card)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            return new CardSpec
            {
                id = card.Id,
                month = card.Month,
                type = card.Type,
                ribbon = card.RibbonColor,
                godoriBird = card.IsGodoriBird,
                piValue = card.PiValue,
            };
        }

        public static List<CardSpec> FromCards(IReadOnlyList<Card> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            var specs = new List<CardSpec>(cards.Count);
            for (int i = 0; i < cards.Count; i++) specs.Add(FromCard(cards[i]));
            return specs;
        }

        public static Card ToCard(CardSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            return new Card(spec.id, spec.month, spec.type, spec.ribbon,
                spec.godoriBird, spec.piValue, BuildDebugName(spec));
        }

        public static List<Card> ToCards(IReadOnlyList<CardSpec> specs)
        {
            if (specs == null) throw new ArgumentNullException(nameof(specs));
            var cards = new List<Card>(specs.Count);
            for (int i = 0; i < specs.Count; i++) cards.Add(ToCard(specs[i]));
            return cards;
        }

        /// <summary>
        /// CardFactory의 명명 규칙을 재현한다. 두 구현의 일치는 CardSpecTests의
        /// "표준 덱 왕복 완전 동일" 테스트가 고정한다 — CardFactory 쪽 명명이 바뀌면 거기서 깨진다.
        /// </summary>
        private static string BuildDebugName(CardSpec spec)
        {
            string kind;
            switch (spec.type)
            {
                case CardType.Gwang: kind = "광"; break;
                case CardType.Yeol: kind = "열끗"; break;
                case CardType.Tti:
                    switch (spec.ribbon)
                    {
                        case RibbonColor.Hong: kind = "홍단"; break;
                        case RibbonColor.Cheong: kind = "청단"; break;
                        case RibbonColor.Cho: kind = "초단"; break;
                        default: kind = "띠"; break;
                    }
                    break;
                default: kind = spec.piValue >= 2 ? "쌍피" : "피"; break;
            }
            return spec.month + "월 " + kind;
        }
    }
}
