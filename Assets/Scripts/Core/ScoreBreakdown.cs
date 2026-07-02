using System.Collections.Generic;

namespace Hwatu.Core
{
    /// <summary>채점 결과의 한 항목 (족보 이름, 점수, 해당 카드 Id들).</summary>
    public sealed class ScoreEntry
    {
        public string Name { get; }
        public int Score { get; }
        public IReadOnlyList<int> CardIds { get; }

        public ScoreEntry(string name, int score, IReadOnlyList<int> cardIds)
        {
            Name = name;
            Score = score;
            CardIds = cardIds;
        }
    }

    public sealed class ScoreBreakdown
    {
        public IReadOnlyList<ScoreEntry> Entries { get; }
        public int Total { get; }

        public ScoreBreakdown(IReadOnlyList<ScoreEntry> entries)
        {
            Entries = entries;
            int total = 0;
            for (int i = 0; i < entries.Count; i++) total += entries[i].Score;
            Total = total;
        }

        public static ScoreBreakdown Empty { get; } = new ScoreBreakdown(new ScoreEntry[0]);
    }
}
