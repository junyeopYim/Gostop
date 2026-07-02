using System.Collections.Generic;
using System.Linq;

namespace Hwatu.Core
{
    public static class ScoreCalculator
    {
        public const string NameGwang = "광";
        public const string NameGodori = "고도리";
        public const string NameYeol = "열끗";
        public const string NameHongdan = "홍단";
        public const string NameCheongdan = "청단";
        public const string NameChodan = "초단";
        public const string NameTti = "띠";
        public const string NamePi = "피";

        /// <summary>고 횟수 → 배수. 0고=1, 1고=2, 2고=3, 3고부터 직전 값의 2배.</summary>
        public static int GetMultiplier(int goCount)
        {
            if (goCount <= 0) return 1;
            if (goCount == 1) return 2;
            int multiplier = 3;
            for (int i = 3; i <= goCount; i++) multiplier *= 2;
            return multiplier;
        }

        /// <summary>획득 카드 목록만으로 점수 내역을 계산하는 순수 함수.</summary>
        public static ScoreBreakdown Calculate(IReadOnlyList<Card> captured)
        {
            var entries = new List<ScoreEntry>();

            // 광: 5장=15 / 4장=4 / 3장+비광 포함=2 / 3장+비광 미포함=3
            var gwang = captured.Where(c => c.Type == CardType.Gwang).ToList();
            if (gwang.Count >= 3)
            {
                int score;
                if (gwang.Count == 5) score = 15;
                else if (gwang.Count == 4) score = 4;
                else score = gwang.Any(c => c.Month == 12) ? 2 : 3;
                entries.Add(new ScoreEntry(NameGwang, score, Ids(gwang)));
            }

            // 고도리: 2·4·8월 열끗(고도리새) 3장 모두 보유 = 5점
            var birds = captured.Where(c => c.IsGodoriBird).ToList();
            if (birds.Count == 3)
                entries.Add(new ScoreEntry(NameGodori, 5, Ids(birds)));

            // 열끗: 5장 이상 → (장수-4)점
            var yeol = captured.Where(c => c.Type == CardType.Yeol).ToList();
            if (yeol.Count >= 5)
                entries.Add(new ScoreEntry(NameYeol, yeol.Count - 4, Ids(yeol)));

            // 홍단/청단/초단: 각 색 3장 세트 = 각 3점
            AddRibbonSet(entries, captured, RibbonColor.Hong, NameHongdan);
            AddRibbonSet(entries, captured, RibbonColor.Cheong, NameCheongdan);
            AddRibbonSet(entries, captured, RibbonColor.Cho, NameChodan);

            // 띠: 5장 이상 → (장수-4)점 (색 세트와 중복 가산, 비띠 포함)
            var tti = captured.Where(c => c.Type == CardType.Tti).ToList();
            if (tti.Count >= 5)
                entries.Add(new ScoreEntry(NameTti, tti.Count - 4, Ids(tti)));

            // 피: PiValue 합 10 이상 → (합-9)점
            var pi = captured.Where(c => c.Type == CardType.Pi).ToList();
            int piSum = pi.Sum(c => c.PiValue);
            if (piSum >= 10)
                entries.Add(new ScoreEntry(NamePi, piSum - 9, Ids(pi)));

            return entries.Count == 0 ? ScoreBreakdown.Empty : new ScoreBreakdown(entries);
        }

        private static void AddRibbonSet(List<ScoreEntry> entries, IReadOnlyList<Card> captured,
                                         RibbonColor color, string name)
        {
            var set = captured.Where(c => c.Type == CardType.Tti && c.RibbonColor == color).ToList();
            if (set.Count == 3)
                entries.Add(new ScoreEntry(name, 3, Ids(set)));
        }

        private static int[] Ids(List<Card> cards) => cards.Select(c => c.Id).ToArray();
    }
}
