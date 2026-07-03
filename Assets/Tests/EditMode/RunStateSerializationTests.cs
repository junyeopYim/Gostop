using System.Collections.Generic;
using Hwatu.Run;
using NUnit.Framework;
using UnityEngine;

namespace Hwatu.Core.Tests
{
    /// <summary>RunState가 JsonUtility 왕복에서 손실 없이 복원되는지 고정한다.</summary>
    public class RunStateSerializationTests
    {
        [Test]
        public void RunState_JSON_왕복_후_모든_필드가_일치한다()
        {
            var original = new RunState
            {
                runSeed = 987654321,
                characterId = "gambler",
                currentDay = 17,
                honbul = 2,
                nojatdon = 42,
                relicIds = new List<string>
                {
                    RelicIds.MoonScroll,
                    RelicIds.Gombangdae,
                },
                deck = CardSpecs.CreateStandardDeckSpecs(),
                salpuriCount = 3,
                relicSlotLimit = 5,
                chasa = new ChasaState { jeong = 3, revealedUntilDay = 21 },
                dayAttempt = 2,
                stateVersion = RunStateMigration.CurrentVersion,
                journey = JourneyGenerator.Generate(987654321),
                currentNodeIndex = 1,
                honbulMax = 4,
                todayNodeCleared = true,
                jaetnalHealedToday = true,
            };
            original.deck[0].enhancements.Add("enh_test_a");
            original.deck[0].enhancements.Add("enh_test_b");
            original.deck[47].enhancements.Add("enh_test_c");
            original.deck.RemoveAt(46);

            string json = JsonUtility.ToJson(original);
            var restored = JsonUtility.FromJson<RunState>(json);

            Assert.AreEqual(original.runSeed, restored.runSeed);
            Assert.AreEqual(original.characterId, restored.characterId);
            Assert.AreEqual(original.currentDay, restored.currentDay);
            Assert.AreEqual(original.honbul, restored.honbul);
            Assert.AreEqual(original.nojatdon, restored.nojatdon);
            Assert.AreEqual(original.dayAttempt, restored.dayAttempt);
            Assert.AreEqual(original.salpuriCount, restored.salpuriCount);
            Assert.AreEqual(original.relicSlotLimit, restored.relicSlotLimit);
            CollectionAssert.AreEqual(original.relicIds, restored.relicIds);
            Assert.AreEqual(original.chasa.jeong, restored.chasa.jeong);
            Assert.AreEqual(original.chasa.revealedUntilDay, restored.chasa.revealedUntilDay);

            // v2 (49일 여정) 필드
            Assert.AreEqual(original.stateVersion, restored.stateVersion);
            Assert.AreEqual(original.currentNodeIndex, restored.currentNodeIndex);
            Assert.AreEqual(original.honbulMax, restored.honbulMax);
            Assert.AreEqual(original.todayNodeCleared, restored.todayNodeCleared);
            Assert.AreEqual(original.jaetnalHealedToday, restored.jaetnalHealedToday);
            JourneyTestUtil.AssertStructurallyEqual(original.journey, restored.journey, "journey 왕복");

            Assert.AreEqual(original.deck.Count, restored.deck.Count);
            for (int i = 0; i < original.deck.Count; i++)
            {
                var a = original.deck[i];
                var b = restored.deck[i];
                Assert.AreEqual(a.id, b.id, $"deck[{i}].id");
                Assert.AreEqual(a.month, b.month, $"deck[{i}].month");
                Assert.AreEqual(a.type, b.type, $"deck[{i}].type");
                Assert.AreEqual(a.ribbon, b.ribbon, $"deck[{i}].ribbon");
                Assert.AreEqual(a.godoriBird, b.godoriBird, $"deck[{i}].godoriBird");
                Assert.AreEqual(a.piValue, b.piValue, $"deck[{i}].piValue");
                CollectionAssert.AreEqual(a.enhancements, b.enhancements, $"deck[{i}].enhancements");
            }
        }
    }
}
