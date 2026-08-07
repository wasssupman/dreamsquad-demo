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
            // 보스가 잡몹보다 확실히 비싸야 한다 — **상대 비교**다. 예전엔 잡몹 기본값 100 을
            // 리터럴로 박았는데, 티어 재장전(three-minute-survival unit 3)으로 스케일이 바뀌면
            // 구조가 멀쩡한데도 빨개진다.
            var mob = deck.ResolveAttackUnitPool()[0];
            Assert.Greater(deck.bossUnit.killScore, mob.killScore,
                $"보스 killScore({deck.bossUnit.killScore}) 가 잡몹({mob.id}={mob.killScore}) 이하");
        }

        // three-minute-survival unit 2 — **스폰 창 불변식**. 구 `LastSpawn_FitsInsideTheTimeLimit`
        // (마지막 스폰이 제한시간 안)을 대체한다: 시각 그리드가 명목값이 되면서 "마지막 웨이브
        // 시각" 은 더 이상 실제 스폰 시각이 아니다(100웨이브 × 20초 = 2000초).
        //
        // 대신 지켜야 할 것은 이것이다: 한 웨이브의 스폰이 상한 간격 안에 끝나야 `_pending` 이
        // 비고, 그래야 "전멸 즉시 다음 웨이브" 가 성립한다. 위반하면 증상이 "웨이브가 항상
        // 20초로만 온다" 는 형태라 원인 추적이 매우 어렵다.
        [Test]
        public void SpawnWindow_FitsInsideTheWaveIntervalCap()
        {
            string[] paths =
            {
                "Assets/_Project/Scripts/Data/Decks/Deck_Serpent.asset",
                "Assets/_Project/Scripts/Data/Decks/Deck_Coil.asset",
                "Assets/_Project/Scripts/Data/Decks/Deck_Twin.asset",
                "Assets/_Project/Scripts/Data/Decks/Deck_Spiral.asset",
                "Assets/_Project/Scripts/Data/Decks/Deck_Zig.asset",
                "Assets/_Project/Scripts/Data/Decks/Deck_Hook.asset",
                "Assets/_Project/Scripts/Data/Decks/Deck_Endless.asset",
            };

            foreach (string path in paths)
            {
                var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(path);
                Assert.IsNotNull(deck, $"덱 에셋을 찾지 못했다: {path}");
                Assert.Greater(deck.maxWaveIntervalSec, 0f, $"{deck.name}: 상한 간격이 0 이면 케이던스가 폭주한다");

                float window = deck.waveSpawnLeadInSec + (deck.maxUnitsPerWave - 1) * deck.intraWaveSpacingSec;
                Assert.Less(window, deck.maxWaveIntervalSec,
                    $"{deck.name}: 스폰 창 {window:F2}s ≥ 상한 간격 {deck.maxWaveIntervalSec}s "
                    + $"(리드인 {deck.waveSpawnLeadInSec} + (maxUnits {deck.maxUnitsPerWave}−1) × spacing {deck.intraWaveSpacingSec}) "
                    + "— 전멸 즉시 진행이 성립하지 않는다");
            }
        }
    }
}
