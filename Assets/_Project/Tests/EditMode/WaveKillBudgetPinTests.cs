using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-score-formula unit 1 — 점수 산식이 웨이브 구성에 대해 기대하는 **구조 불변식**.
    //
    // 처음에는 고정 시드 스케줄의 실측값(웨이브별 수량 배열 / 킬 예산 10,300 / 마지막 스폰
    // 163.40s)을 그대로 못박았다. 그게 틀렸다 — 그 값들은 순수 밸런스 산물이고, 계약 7이
    // 이미 "킬 만점을 상수로 박지 않는다(런타임 누적)"고 정하고 있다. 밸런스가 바뀌어도
    // 점수 시스템은 아무것도 깨지지 않는데 테스트만 빨개져서, 방어 가치 없이 마찰만 남았다.
    // (실제로 wave-pattern 밸런싱 머지에서 즉시 깨졌다: 63 → 72기)
    //
    // 그래서 밸런스에 무관한 것만 남긴다. 수량·간격·타이밍은 **자유롭게 튜닝해도 된다.**
    public class WaveKillBudgetPinTests
    {
        private const string DeckPath = "Assets/_Project/Scripts/Data/Decks/WaveA.asset";

        private static AttackDeck LoadDeck()
        {
            var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(DeckPath);
            Assert.IsNotNull(deck, $"덱 에셋을 찾지 못했다: {DeckPath}");
            return deck;
        }

        private static bool IsBoss(AttackUnitData u)
            => u != null && u.nightmareMechanics != null && u.nightmareMechanics.Length > 0;

        // waveSeed 가 비0 = 고정 오버라이드 → 모든 플레이어가 같은 스케줄을 받는다
        // (BattleBridge.cs:1543). 0 이 되면 매판 랜덤이라 같은 점수라도 난이도가 달라져
        // 비동기 토너먼트의 비교 가능성이 무너진다.
        [Test]
        public void DeckSeed_IsPinned_SoEveryPlayerGetsTheSameSchedule()
        {
            Assert.AreNotEqual(0, LoadDeck().waveSeed,
                "waveSeed 가 0 이면 매치마다 스케줄이 달라져 점수를 서로 비교할 수 없다");
        }

        // 같은 시드로 두 번 생성하면 같은 결과가 나와야 한다. 생성기에 비결정 요소가
        // 끼어들면(시간·전역 RNG) 여기서 잡힌다.
        [Test]
        public void Generation_IsDeterministic_ForTheSameSeed()
        {
            var deck = LoadDeck();
            var a = WavePatternGenerator.Generate(deck, deck.waveSeed);
            var b = WavePatternGenerator.Generate(deck, deck.waveSeed);

            Assert.AreEqual(a.waves.Count, b.waves.Count, "웨이브 수");
            for (int i = 0; i < a.waves.Count; i++)
                Assert.AreEqual(a.waves[i].totalCount, b.waves[i].totalCount, $"웨이브 {i + 1} 스폰 수");
        }

        // 보스는 bossWaveInterval 주기에만 온다. 킬 가치가 잡몹의 20배라 이 편성 계약이
        // 깨지면 점수 분포가 통째로 달라진다 — 수량 튜닝과 달리 이건 구조다.
        [Test]
        public void BossWaves_LandOnTheConfiguredInterval()
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

        // 킬 예산은 **스폰 구성에서 나온다**(계약 7). 상수로 박으면 안 되고, 유닛별
        // killScore 가 0 으로 비어 있어도 안 된다 — 그러면 킬축이 통째로 죽는다.
        [Test]
        public void KillBudget_ComesFromActualSpawns_AndEveryUnitCarriesValue()
        {
            var deck = LoadDeck();
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed);

            int spawns = 0, budget = 0, bosses = 0;
            for (int i = 0; i < plan.waves.Count; i++)
            {
                var groups = plan.waves[i].groups;
                for (int g = 0; g < groups.Count; g++)
                {
                    var u = groups[g].unit;
                    if (u == null) continue;
                    Assert.Greater(u.killScore, 0, $"'{u.id}' 의 killScore 가 0 — 처치해도 점수가 안 붙는다");
                    int count = groups[g].count;
                    spawns += count;
                    budget += u.killScore * count;
                    if (IsBoss(u)) bosses += count;
                }
            }

            Assert.Greater(spawns, 0, "스폰이 하나도 없다");
            Assert.Greater(budget, 0, "킬 예산이 0");
            Assert.Greater(bosses, 0, "보스가 하나도 없다 — 보스 편성 계약 확인");
            // 보스가 잡몹보다 확실히 비싸야 한다는 것만 확인한다(구체 배율은 밸런스).
            Assert.Greater(deck.bossUnit.killScore, 100, "보스 killScore 가 잡몹 기본값 이하");
        }

        // 당기기 없이도 제한시간 안에 마지막 적이 나온다. 이게 깨지면 정상 플레이로
        // 전멸 승리가 불가능해져 시간축이 영구히 0 이 된다.
        [Test]
        public void LastSpawn_FitsInsideTheTimeLimit()
        {
            var deck = LoadDeck();
            var plan = WavePatternGenerator.Generate(deck, deck.waveSeed);

            float last = 0f;
            for (int i = 0; i < plan.waves.Count; i++)
            {
                var wave = plan.waves[i];
                // wave-pattern unit 11 — 리드인만큼 스폰 base 가 밀리므로 pin 도 같은 base 를
                // 써야 한다. 트리거만 보면 실제보다 리드인만큼 이른 값을 재 pin 이 무력화된다.
                var entries = WavePatternGenerator.ExpandWave(
                    wave, wave.triggerTimeSec + plan.spawnLeadInSec, 4, deck.intraWaveSpacingSec);
                for (int e = 0; e < entries.Count; e++)
                    if (entries[e].triggerTimeSec > last) last = entries[e].triggerTimeSec;
            }

            Assert.Less(last, deck.timerDurationSec,
                $"마지막 스폰 {last:F2}s 가 제한시간 {deck.timerDurationSec}s 를 넘는다 "
                + $"(리드인 {plan.spawnLeadInSec:F1}s 포함) — 정상 클리어 불가");
        }
    }
}
