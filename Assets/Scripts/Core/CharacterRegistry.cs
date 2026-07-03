using System.Collections.Generic;

namespace Hwatu.Core
{
    public readonly struct CharacterDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string SinLabel;
        public readonly string Intro;
        public readonly string StartBonusText;

        public CharacterDefinition(string id, string displayName, string sinLabel, string intro, string startBonusText)
        {
            Id = id;
            DisplayName = displayName;
            SinLabel = sinLabel;
            Intro = intro;
            StartBonusText = startBonusText;
        }
    }

    public static class CharacterRegistry
    {
        private static readonly CharacterDefinition[] Characters =
        {
            new CharacterDefinition(
                "gambler",
                "노름꾼",
                "도박",
                "노름빚을 지고 끌려온 망자.",
                "시작 조건: 기본 화투패 묶음\n초기 보탬: 없음")
        };

        public static IReadOnlyList<CharacterDefinition> All => Characters;

        public static bool TryGet(string id, out CharacterDefinition character)
        {
            for (int i = 0; i < Characters.Length; i++)
            {
                if (Characters[i].Id != id) continue;
                character = Characters[i];
                return true;
            }

            character = default;
            return false;
        }
    }
}
