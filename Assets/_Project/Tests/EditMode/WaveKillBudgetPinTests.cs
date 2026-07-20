using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-score-formula unit 1 — README 가 기재한 고정 시드 스케줄과 킬 예산의 회귀 고정.
    //
    // 산식이 아니라 **문서값**을 지키는 테스트다. 킬 만점은 런타임 누적이므로(spec 계약 7)
    // 이 값이 바뀐다고 점수 계산이 깨지지는 않는다. 다만 바뀌면 README 예산 표를 고쳐야 하고,
    // 이 테스트가 그 신호다.
    //
    // 실패하면 **테스트가 아니라 README 를 고친다** — 덱 파라미터/생성기가 바뀐 것이다.
    public class WaveKillBudgetPinTests
    {
        private const string DeckPath = "Assets/_Project/Scripts/Data/Decks/WaveA.asset";
        private const float Spacing = 0.35f;

        // README "웨이브 스케줄 실측" 절.
        private static readonly int[] ExpectedTotals = { 5, 5, 8, 8, 5, 7, 6, 8, 8, 5 };

        private static AttackDeck LoadDeck()
        {
            var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(DeckPath);
            Assert.IsNotNull(deck, $"덱 에셋을 찾지 못했다: {DeckPath}");
            return deck;
        }

        private static bool IsBoss(AttackUnitData u)
            => u != null && u.nightmareMechanics != null && u.nightmareMechanics.Length > 0;

        // waveSeed 가 비0 = 고정 오버라이드 → 모든 플레이어가 같은 스케줄을 받는다
        // (BattleBridge.cs:1543). 0 이 되면 매판 랜덤이 되고 킬 예산이 흔들린다.
        [Test]
        public void DeckSeed_IsPinned_SoTheScheduleIsDeterministic()
        {
            Assert.AreNotEqual(0, LoadDeck().waveSeed,
                "waveSeed 가 0 이면 매치마다 스케줄이 달라져 킬 예산이 8,700~16,200 으로 흔들린다");
        }

        [Test]
        public void FixedSeedSchedule_MatchesDocumentedWaveTotals()
        {
            var deck = LoadDeck();
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed);

            Assert.AreEqual(ExpectedTotals.Length, plan.waves.Count, "웨이브 수");
            for (int i = 0; i < plan.waves.Count; i++)
                Assert.AreEqual(ExpectedTotals[i], plan.waves[i].totalCount, $"웨이브 {i + 1} 스폰 수");
        }

        // bossWaveInterval=5 → 0-index 4·9 (5·10번째 웨이브)가 보스 웨이브.
        [Test]
        public void BossWaves_LandOnTheDocumentedIndices()
        {
            var deck = LoadDeck();
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed);

            for (int i = 0; i < plan.waves.Count; i++)
            {
                bool hasBoss = false;
                var groups = plan.waves[i].groups;
                for (int g = 0; g < groups.Count; g++)
                    if (IsBoss(groups[g].unit)) hasBoss = true;

                bool expected = (i + 1) % deck.bossWaveInterval == 0;
                Assert.AreEqual(expected, hasBoss, $"웨이브 {i + 1} 보스 여부");
            }
        }

        // README 예산 표의 근거: 65기 = 잡몹 63 + 보스 2 → 10,300.
        [Test]
        public void KillBudget_MatchesReadmeTable()
        {
            var deck = LoadDeck();
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed);

            int mobs = 0, bosses = 0, budget = 0;
            for (int i = 0; i < plan.waves.Count; i++)
            {
                var groups = plan.waves[i].groups;
                for (int g = 0; g < groups.Count; g++)
                {
                    var u = groups[g].unit;
                    if (u == null) continue;
                    int count = groups[g].count;
                    budget += u.killScore * count;
                    if (IsBoss(u)) bosses += count; else mobs += count;
                }
            }

            Assert.AreEqual(63, mobs, "잡몹 수");
            Assert.AreEqual(2, bosses, "보스 수");
            Assert.AreEqual(65, mobs + bosses, "총 스폰");
            Assert.AreEqual(10_300, budget, "킬 예산 — 바뀌었으면 README 예산 표를 갱신할 것");
        }

        // 당기기 없이 도달 가능한 마지막 스폰. 시간점수 상한(약 1,660)의 근거값이다.
        [Test]
        public void LastSpawnTime_MatchesReadme()
        {
            var deck = LoadDeck();
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed);

            float last = 0f;
            for (int i = 0; i < plan.waves.Count; i++)
            {
                var wave = plan.waves[i];
                var entries = WavePatternGenerator.ExpandWave(wave, wave.triggerTimeSec, 4, Spacing);
                for (int e = 0; e < entries.Count; e++)
                    if (entries[e].triggerTimeSec > last) last = entries[e].triggerTimeSec;
            }

            Assert.AreEqual(163.40f, last, 0.01f, "마지막 스폰 시각");
        }
    }
}
