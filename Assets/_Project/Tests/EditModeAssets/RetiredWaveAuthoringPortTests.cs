using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCore.Wave;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using CoreAltitude = Wassup.BattleCore.Wave.SlotAltitude;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 9 구현 2 — 옛 `WaveConceptAuthoringTests` · `WaveKillBudgetPinTests` 의 규칙을
    // **라이브 덱 × 코어 생성기**로 옮긴다. 옛 쪽은 SO 를 옛 `WavePatternGenerator` 에 직접 넣었다 —
    // 여기는 라이브 판과 같은 문(`MatchDefinitionBuilder.CollectEnemies` → `Build` → `ToDeckDef`)을 지나
    // 정의표를 만들고 코어 `WaveGenerator.Generate` 로 굴린다. 그래야 「빌더가 덱을 옮기다 컨셉을 흘렸다」도 잡힌다.
    //
    // 저작 자체만 보는 단언(컨셉 에셋의 슬롯·배율·게이트)은 SO 를 그대로 읽는다 — 생성기와 무관하지만
    // 옛 파일이 통째로 지워지므로 같이 옮긴다.
    //
    // ⚠ 밸런스 수치를 리터럴로 못박지 않는다 — 옛 테스트가 그랬듯 계약(구조)과 저작 필드에서 파생한다.
    // 예외: `GeneratorVersion` 7 은 «baseline 표시» 핀이라 옛 값을 그대로 옮겼다(편성 규칙이 바뀌면 올리는 손잡이).
    public class RetiredWaveAuthoringPortTests
    {
        private const string ConceptDir = "Assets/_Project/Data/WaveConcepts";
        private const string DeckDir = "Assets/_Project/Data/Decks";
        // battle-content-finish unit 2 — 킬 예산 기준 덱은 라이브 풀 밖의 **테스트 픽스처**다.
        private const string FixtureDir = "Assets/_Project/Tests/Fixtures";
        private const string SkimmerPath = "Assets/_Project/Data/Enemies/Enemy_Skimmer.asset";
        private const string KillBudgetDeck = "WaveA";

        // battle-content-finish unit 2 — 라이브 맵 풀(`MapStagePool`)의 덱만 증언한다. 사라진 맵의 덱(Twin · Spiral · Hook ·
        // Ford · Isle)은 지웠다 — 테스트가 죽은 저작을 살려 두는 것이 더 나쁘다.
        private static readonly string[] MapDecks = { "Deck_Serpent", "Deck_Coil", "Deck_Zig" };

        private static readonly string[] SiegeDecks = { "Deck_Duel" };

        private static readonly string[] ConceptNames =
        {
            "Concept_Spread", "Concept_Swarm", "Concept_Heavy", "Concept_Ranged", "Concept_Airstrike",
        };

        // ── 라이브 정의표 ─────────────────────────────────────────────────────

        private sealed class Live
        {
            public AttackDeck Deck;
            public AttackUnitData[] Units;   // 정의표 인덱스 순서 = `Enemies` 와 1:1
            public EnemyDef[] Enemies;
            public WaveDeckDef WaveDeck;
        }

        private static readonly Dictionary<string, Live> Cache = new Dictionary<string, Live>();

        private static AttackDeck LoadDeck(string name)
        {
            var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>($"{DeckDir}/{name}.asset");
            if (deck == null) deck = AssetDatabase.LoadAssetAtPath<AttackDeck>($"{FixtureDir}/{name}.asset");
            Assert.IsNotNull(deck, $"덱 에셋을 찾지 못했다: {name}");
            return deck;
        }

        /// <summary>라이브 판과 같은 문으로 덱을 정의표에 싣는다(적 인덱스를 매기는 쪽과 웨이브가 같은 표를 본다).</summary>
        private static Live Load(string name)
        {
            if (Cache.TryGetValue(name, out var hit) && hit.Deck != null) return hit;
            var deck = LoadDeck(name);
            var units = MatchDefinitionBuilder.CollectEnemies(deck, null, null);
            var def = MatchDefinitionBuilder.Build(System.Array.Empty<DefenderUnitData>(), units, 1, ModeDef.Default());
            var live = new Live
            {
                Deck = deck,
                Units = units,
                Enemies = def.Enemies,
                WaveDeck = MatchDefinitionBuilder.ToDeckDef(deck, units),
            };
            Cache[name] = live;
            return live;
        }

        /// <summary>코어 생성기. 시드는 런타임과 같은 규칙 — 비0 고정 오버라이드(`WaveScheduler.Begin`).</summary>
        private static WavePlan Plan(Live live, int lanes, int seedOverride = 0, System.Action<string> report = null)
        {
            int seed = seedOverride != 0 ? seedOverride : live.WaveDeck.WaveSeed;
            Assert.AreNotEqual(0, seed, "라이브 덱은 고정 시드여야 한다(아래 WaveSeeds 핀)");
            return WaveGenerator.Generate(in live.WaveDeck, live.Enemies, seed, lanes, report);
        }

        private static WavePlan Plan(string name, int lanes) => Plan(Load(name), lanes);

        private static int SiegeLanes => StructurePlacements.SiegeSpawnOffsets.Length;

        private static bool IsAir(in EnemyDef e) => WaveGenerator.MatchesAltitude(in e, CoreAltitude.Air);
        private static bool IsBoss(in EnemyDef e) => e.Tier == (int)EnemyTier.Boss;
        private static bool IsElite(in EnemyDef e) => e.Tier == (int)EnemyTier.Elite;

        private static int MainWaves(Live live)
            => live.WaveDeck.RampBreakWave >= 2 ? live.WaveDeck.RampBreakWave - 1 : 15;

        private static WaveConceptData Concept(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<WaveConceptData>($"{ConceptDir}/{name}.asset");
            Assert.IsNotNull(c, $"컨셉 에셋을 찾지 못했다: {name}");
            return c;
        }

        private static string Signature(WavePlan plan, Live live)
        {
            var sb = new StringBuilder();
            foreach (var w in plan.Waves)
            {
                sb.Append(w.ConceptLabel).Append('|');
                foreach (var g in w.Groups)
                    sb.Append(live.Enemies[g.EnemyIndex].Id).Append(':').Append(g.Count).Append('@').Append(g.LaneIndex).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        // 24 · 컨셉 에셋 저작 (옛 WaveConceptAuthoringTests — 생성기 무관)
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveConceptAuthoringTests::FiveConcepts_Exist_WithReadableLabels — 컨셉 5종 · 라벨 · 슬롯
        [Test]
        public void 컨셉_5종이_있고_라벨과_슬롯이_있다()
        {
            foreach (string name in ConceptNames)
            {
                var c = Concept(name);
                Assert.IsNotEmpty(c.id, $"{name}: id 가 비었다");
                Assert.IsNotEmpty(c.displayName, $"{name}: displayName 이 비면 브리핑·도크에 라벨이 안 나온다");
                Assert.Greater(c.slots.Length, 0, $"{name}: 슬롯이 없으면 편성을 못 만든다");
            }
        }

        // 옛 WaveConceptAuthoringTests::Spread_IsTheOnlyConceptAvailableInBlockZero — 블록 0 은 「평소」만(게이트로)
        [Test]
        public void 블록_0에서는_평소만_열린다()
        {
            Assert.AreEqual(1, Concept("Concept_Spread").minWaveNumber);
            foreach (string name in new[] { "Concept_Swarm", "Concept_Heavy", "Concept_Ranged", "Concept_Airstrike" })
                Assert.Greater(Concept(name).minWaveNumber, 3, $"{name}: 블록 0(웨이브 1~3)에 들어오면 온보딩이 깨진다");
        }

        // 옛 WaveConceptAuthoringTests::Spread_StaysOnTheGround — 「평소」는 지상
        [Test]
        public void 평소는_지상만_뽑는다()
        {
            foreach (var slot in Concept("Concept_Spread").slots)
                Assert.AreEqual(Wassup.Data.SlotAltitude.Ground, slot.altitude,
                    "「평소」가 비행을 뽑으면 대공 없는 첫 3웨이브에서 막을 수 없는 적이 나온다");
        }

        // 옛 WaveConceptAuthoringTests::Heavy_IsASingleTankerSlot — 「중장」 = 탱커 단일 슬롯 · 배율 < 1
        [Test]
        public void 중장은_탱커_단일_슬롯이고_수량을_줄인다()
        {
            var heavy = Concept("Concept_Heavy");
            Assert.AreEqual(1, heavy.slots.Length, "2종을 만들려고 슬롯을 붙이면 벽이 흩어진다");
            Assert.AreEqual(EnemyClass.Tanker, heavy.slots[0].classFilter);
            Assert.Less(heavy.countMul, 1f, "단단한 성질은 수량을 줄여야 난이도가 성질에 안 끌려간다");
        }

        // 옛 WaveConceptAuthoringTests::Ranged_IsAPincerOfShooters — 「원거리」 = 지상 사수 협공 · 늦은 게이트
        [Test]
        public void 원거리는_지상_사수_협공이고_늦게_열린다()
        {
            var ranged = Concept("Concept_Ranged");
            Assert.AreEqual(2, ranged.RequiredLaneCount, "원거리는 협공 위상");
            foreach (var slot in ranged.slots)
            {
                Assert.AreEqual(EnemyClass.Shooter, slot.classFilter);
                Assert.AreEqual(Wassup.Data.SlotAltitude.Ground, slot.altitude,
                    "고도와 성질은 직교한다 — Ground 를 명시하지 않으면 비행 Shooter 가 섞인다");
            }
            Assert.GreaterOrEqual(ranged.minWaveNumber, 7, "방어선을 깎는 압력이라 게이트를 늦게 둔다");
        }

        // 옛 WaveConceptAuthoringTests::Airstrike_IsTwoAirSlots_AndFew — 「공습」 = 공중 2슬롯 · 한 입구 · 소수
        [Test]
        public void 공습은_공중_2슬롯_한_입구_소수다()
        {
            var air = Concept("Concept_Airstrike");
            Assert.AreEqual(2, air.slots.Length, "슬롯이 하나면 엘리트(동시 등장 1)를 뽑을 때 웨이브가 1기로 붕괴한다");
            foreach (var slot in air.slots) Assert.AreEqual(Wassup.Data.SlotAltitude.Air, slot.altitude);
            Assert.AreEqual(1, air.RequiredLaneCount, "공습은 한 입구로 온다");
            Assert.Less(air.countMul, 0.5f, "소수여야 «스킬 한 발 값»으로 번역된다");
        }

        // 옛 WaveConceptAuthoringTests::VariantSlots_NeverCrossAltitude — 변주는 고도를 교차하지 않는다
        [Test]
        public void 변주_슬롯은_본_편성에_없는_고도를_끌어오지_않는다()
        {
            foreach (string name in ConceptNames)
            {
                var c = Concept(name);
                if (c.variantSlots == null || c.variantSlots.Length == 0) continue;
                var main = new HashSet<Wassup.Data.SlotAltitude>();
                foreach (var s in c.slots) if (s != null) main.Add(s.altitude);
                foreach (var s in c.variantSlots)
                    if (s != null)
                        Assert.IsTrue(main.Contains(s.altitude),
                            $"{name}: 변주가 본 편성에 없는 고도({s.altitude})를 끌어온다 — 대공 배치가 그 웨이브만 헛돈다");
            }
        }

        // 옛 WaveConceptAuthoringTests::Airstrike_HasNoVariant — 「공습」은 변주 없음
        [Test]
        public void 공습에는_변주가_없다()
        {
            var air = Concept("Concept_Airstrike");
            Assert.IsTrue(air.variantSlots == null || air.variantSlots.Length == 0,
                "변주를 넣으면 고도 교차 규칙과 정면으로 부딪힌다");
        }

        // 옛 WaveConceptAuthoringTests::Swarm_IsRunnerClass_WithHigherCount — 「벌떼」 = 러너 · 배율 > 1 · 한 입구
        [Test]
        public void 벌떼는_러너이고_수량을_늘리고_한_입구로_온다()
        {
            var swarm = Concept("Concept_Swarm");
            foreach (var slot in swarm.slots) Assert.AreEqual(EnemyClass.Runner, slot.classFilter);
            Assert.Greater(swarm.countMul, 1f, "처리량 문제여야 하므로 수량을 올린다");
            Assert.AreEqual(1, swarm.RequiredLaneCount, "벌떼는 한 입구로 쏟아진다");
        }

        // ════════════════════════════════════════════════════════════════════
        // 24 · 덱 배선 — 정의표 경유
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveConceptAuthoringTests::EveryLiveDeck_ReferencesAllFiveConcepts — 라이브 덱은 컨셉 5종 · 블록 3
        [Test]
        public void 라이브_덱은_정의표에_컨셉_5종과_블록_3을_싣는다()
        {
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                Assert.AreEqual(ConceptNames.Length, live.Deck.waveConceptPool.Length, $"{name}: 컨셉 풀 크기");
                foreach (var c in live.Deck.waveConceptPool) Assert.IsNotNull(c, $"{name}: 컨셉 참조가 비었다(GUID 유실)");
                Assert.AreEqual(ConceptNames.Length, live.WaveDeck.Concepts.Length, $"{name}: 빌더가 컨셉을 흘렸다");
                Assert.AreEqual(3, live.WaveDeck.ConceptHoldWaves, $"{name}: 블록 길이");
            }
        }

        // 옛 WaveConceptAuthoringTests::MapDecks_IncludeSkimmer_AndNotAtTheEnd — Skimmer 는 풀에 있고 맨 뒤가 아니다
        [Test]
        public void 맵_덱_풀에_스키머가_있고_맨_뒤가_아니다()
        {
            var skimmer = AssetDatabase.LoadAssetAtPath<AttackUnitData>(SkimmerPath);
            Assert.IsNotNull(skimmer, SkimmerPath);
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                int row = System.Array.IndexOf(live.Units, skimmer);
                int at = row >= 0 ? System.Array.IndexOf(live.WaveDeck.EnemyPool, row) : -1;
                Assert.GreaterOrEqual(at, 0, $"{name}: Skimmer 가 풀에 없으면 「공습」이 성립하지 않는다");
                Assert.Less(at, live.WaveDeck.EnemyPool.Length - 1,
                    $"{name}: 맨 뒤면 등장 게이트의 전방 순환이 초반 웨이브를 풀 첫 칸으로 쏠리게 한다");
            }
        }

        // 옛 WaveConceptAuthoringTests::AirRoster_StaysTightEnoughToClump — 공중 로스터 속도 폭 ≤ 1.5(뭉침 계약)
        [Test]
        public void 공중_로스터는_뭉칠_만큼_속도_폭이_좁다()
        {
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                float min = float.MaxValue, max = 0f;
                int air = 0;
                foreach (int row in live.WaveDeck.EnemyPool)
                {
                    if (!IsAir(live.Enemies[row])) continue;
                    air++;
                    min = System.Math.Min(min, live.Enemies[row].MoveSpeed);
                    max = System.Math.Max(max, live.Enemies[row].MoveSpeed);
                }
                Assert.Greater(air, 0, $"{name}: Air 가 하나도 없으면 「공습」이 fail-open 으로 지상을 뽑는다");
                Assert.LessOrEqual(max - min, 1.5f, $"{name}: Air 속도 폭 {max - min:0.#} — 「공습」이 흩어진다");
            }
        }

        // 옛 WaveConceptAuthoringTests::ConceptSlots_HaveEnoughDistinctCandidates_AtTheirGateWave — 게이트 웨이브 후보 ≥ 슬롯 수
        [Test]
        public void 컨셉_슬롯은_게이트_웨이브에_슬롯_수만큼_서로_다른_후보가_있다()
        {
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                foreach (var c in live.WaveDeck.Concepts)
                {
                    if (c.Slots == null || c.Slots.Length == 0) continue;
                    AssertSlotSetHasCandidates(name, live, c.Id, c.Slots, c.MinWaveNumber, "본 편성");
                    // 변주는 블록 가운데(게이트+1)에 본 편성 위로 **끼어든다** — 합친 슬롯 집합을 본다.
                    if (live.WaveDeck.ConceptHoldWaves >= 3 && c.VariantSlots != null && c.VariantSlots.Length > 0)
                    {
                        var combined = new List<WaveSlotDef>(c.Slots);
                        combined.AddRange(c.VariantSlots);
                        AssertSlotSetHasCandidates(name, live, c.Id, combined.ToArray(), c.MinWaveNumber + 1, "변주(본+삽입)");
                    }
                }
            }
        }

        private static void AssertSlotSetHasCandidates(string deck, Live live, string conceptId,
                                                        WaveSlotDef[] slots, int waveNumber, string label)
        {
            var distinct = new HashSet<int>();
            bool allCapFree = true;
            foreach (var slot in slots)
                foreach (int row in live.WaveDeck.EnemyPool)
                {
                    ref var e = ref live.Enemies[row];
                    if (e.MinWaveNumber > waveNumber) continue;
                    if (slot.ClassFilter != 0 && e.EnemyClass != slot.ClassFilter) continue;
                    if (!WaveGenerator.MatchesAltitude(in e, slot.Altitude)) continue;
                    distinct.Add(row);
                    if (e.MaxPerWave > 0) allCapFree = false;
                }

            Assert.Greater(distinct.Count, 0,
                $"{deck} 컨셉 '{conceptId}' {label}: 게이트 웨이브 {waveNumber} 에 후보가 0이다 — 성질/고도 fail-open 으로만 편성된다");
            if (distinct.Count >= slots.Length || allCapFree) return;
            Assert.Fail($"{deck} 컨셉 '{conceptId}' {label}: 게이트 웨이브 {waveNumber} 의 서로 다른 후보 {distinct.Count} < 슬롯 "
                        + $"{slots.Length} 인데 상한 있는 유닛이 섞여 있다 — 중복 픽이 동시 등장 상한을 슬롯 수만큼 곱한다");
        }

        // 옛 WaveConceptAuthoringTests::EliteWaves_DoNotCollapseToASingleUnit — 종류합 ≤ 상한 · 엘리트 웨이브 > 1기
        [Test]
        public void 엘리트_웨이브는_1기로_붕괴하지_않고_종류합이_상한을_넘지_않는다()
        {
            foreach (string name in MapDecks) AssertNoEliteCollapse(name, Load(name), 3);
            // 공성 3덱이 최대 위험군이다: 클라이맥스는 변주가 매 웨이브 붙어 중복 픽 기회가 가장 많다.
            foreach (string name in SiegeDecks) AssertNoEliteCollapse(name, Load(name), SiegeLanes);
        }

        private static void AssertNoEliteCollapse(string name, Live live, int lanes)
        {
            var plan = Plan(live, lanes);
            for (int i = 0; i < plan.WaveCount; i++)
            {
                var w = plan.Waves[i];
                bool hasElite = false;
                // 상한은 「종류별」이다 — 그룹별로 재면 중복 픽(같은 유닛 2슬롯)이 각자 상한 안이라 초록으로 샌다.
                var perUnit = new Dictionary<int, int>();
                foreach (var g in w.Groups)
                {
                    if (IsElite(live.Enemies[g.EnemyIndex])) hasElite = true;
                    perUnit[g.EnemyIndex] = (perUnit.TryGetValue(g.EnemyIndex, out int c) ? c : 0) + g.Count;
                }
                foreach (var kv in perUnit)
                {
                    int cap = live.Enemies[kv.Key].MaxPerWave;
                    if (cap <= 0) continue;
                    Assert.LessOrEqual(kv.Value, cap,
                        $"{name} 웨이브 {i + 1}('{w.ConceptLabel}'): {live.Enemies[kv.Key].Id} 종류합 {kv.Value} > 상한 {cap} — 중복 픽이 상한을 곱했다");
                }
                if (hasElite)
                    Assert.Greater(w.TotalCount, 1,
                        $"{name} 웨이브 {i + 1}('{w.ConceptLabel}'): 엘리트가 뽑혀 웨이브가 1기로 붕괴했다");
            }
        }

        // 옛 WaveConceptAuthoringTests::SlimeOffspring_NeverEnterThePool — 분열 파생물은 정규 풀에 없다
        [Test]
        public void 분열로만_태어나는_슬라임은_정규_풀에_없다()
        {
            string[] offspring =
            {
                "Assets/_Project/Data/Enemies/Enemy_Slime_Mid.asset",
                "Assets/_Project/Data/Enemies/Enemy_Slime_Small.asset",
            };
            foreach (string path in offspring)
            {
                var child = AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
                if (child == null) continue;
                foreach (string name in MapDecks)
                {
                    var live = Load(name);
                    int row = System.Array.IndexOf(live.Units, child);
                    Assert.IsTrue(row < 0 || System.Array.IndexOf(live.WaveDeck.EnemyPool, row) < 0,
                        $"{name}: {path} 는 분열로만 태어나는 파생물이다 — 정규 편성에 넣으면 부모 없이 튀어나온다");
                }
            }
        }

        // 옛 WaveConceptAuthoringTests::GeneratorVersion_IsBumped_SoTheNewBaselineIsVisible — baseline 표시 핀(정의표까지 실린다)
        [Test]
        public void 생성기_버전_핀이_정의표에_실린다()
        {
            // 풀이나 편성 규칙이 바뀔 때마다 올린다(이력은 옛 테스트 주석 — 3 컨셉 · 4/5 엘리트+변주 병합 · 6 Skimmer 게이트 · 7 공습 상한).
            const int baseline = 7;
            foreach (string name in System.Linq.Enumerable.Concat(MapDecks, SiegeDecks))
            {
                Assert.AreEqual(baseline, LoadDeck(name).waveGeneratorVersion, $"{name}: 풀/편성이 바뀌었다 — 버전으로 새 baseline 을 표시한다");
                Assert.AreEqual(baseline, Load(name).WaveDeck.GeneratorVersion, $"{name}: 빌더가 버전을 흘렸다");
            }
        }

        // 옛 WaveConceptAuthoringTests::WaveSeeds_ArePinnedAndUnique — 시드 비0 · 덱마다 유일
        [Test]
        public void 웨이브_시드는_고정이고_덱마다_다르다()
        {
            var seen = new Dictionary<int, string>();
            foreach (string name in System.Linq.Enumerable.Concat(MapDecks, SiegeDecks))
            {
                int seed = Load(name).WaveDeck.WaveSeed;
                Assert.AreNotEqual(0, seed, $"{name}: 0 이면 매판 달라진다");
                Assert.IsFalse(seen.ContainsKey(seed), $"{name} 과 {(seen.ContainsKey(seed) ? seen[seed] : "")} 가 같은 시드를 쓴다");
                seen[seed] = name;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // 24 · 라이브 덱 × 코어 생성기 결과
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveConceptAuthoringTests::FirstBlock_IsAlwaysSpread — 첫 블록은 「평소」
        [Test]
        public void 첫_블록은_언제나_평소다()
        {
            string spread = Concept("Concept_Spread").displayName;
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 2);
                for (int i = 0; i < 3; i++)
                    Assert.AreEqual(spread, plan.Waves[i].ConceptLabel, $"{name} 웨이브 {i + 1}: 첫 접촉은 익숙해야 한다");
            }
        }

        // 옛 WaveConceptAuthoringTests::NoAirEnemy_AppearsOutsideAirstrikeBlocks — 공습 밖에 비행 없음
        [Test]
        public void 공습_블록_밖에는_비행이_없다()
        {
            string airLabel = Concept("Concept_Airstrike").displayName;
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                var plan = Plan(live, 3);
                for (int i = 0; i < plan.WaveCount; i++)
                {
                    var w = plan.Waves[i];
                    if (w.ConceptLabel == airLabel) continue;
                    foreach (var g in w.Groups)
                        Assert.IsFalse(IsAir(live.Enemies[g.EnemyIndex]),
                            $"{name} 웨이브 {i + 1}('{w.ConceptLabel}'): 지상 컨셉에 비행이 섞였다");
                }
            }
        }

        // 옛 WaveConceptAuthoringTests::AirstrikeBlocks_AreAllAir — 공습 블록은 (보스 선봉 제외) 전부 비행
        [Test]
        public void 공습_블록은_보스_선봉을_빼면_전부_비행이다()
        {
            string airLabel = Concept("Concept_Airstrike").displayName;
            bool saw = false;
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                var plan = Plan(live, 3);
                for (int i = 0; i < plan.WaveCount; i++)
                {
                    var w = plan.Waves[i];
                    if (w.ConceptLabel != airLabel) continue;
                    saw = true;
                    foreach (var g in w.Groups)
                    {
                        if (IsBoss(live.Enemies[g.EnemyIndex])) continue;
                        Assert.IsTrue(IsAir(live.Enemies[g.EnemyIndex]), $"{name} 웨이브 {i + 1}: 「공습」에 지상이 섞였다");
                    }
                }
            }
            Assert.IsTrue(saw, "6맵에 「공습」이 한 번도 안 나왔다 — 가중치/게이트 확인");
        }

        // 옛 WaveConceptAuthoringTests::HeavyBlocks_AreTankersExceptTheMiddleVariant — 「중장」은 가운데 변주 외 탱커만
        [Test]
        public void 중장_블록은_가운데_변주를_빼면_탱커뿐이다()
        {
            var heavyAsset = Concept("Concept_Heavy");
            string heavyLabel = heavyAsset.displayName;
            bool heavyHasVariant = heavyAsset.variantSlots != null && heavyAsset.variantSlots.Length > 0;
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                var plan = Plan(live, 3);
                for (int i = 0; i < plan.WaveCount; i++)
                {
                    var w = plan.Waves[i];
                    if (w.ConceptLabel != heavyLabel) continue;
                    bool isMiddle = i % 3 == 1;
                    bool sawTanker = false, sawOther = false;
                    foreach (var g in w.Groups)
                    {
                        ref var e = ref live.Enemies[g.EnemyIndex];
                        if (IsBoss(e)) continue;
                        if (e.EnemyClass == (int)EnemyClass.Tanker) sawTanker = true; else sawOther = true;
                        if (!isMiddle)
                            Assert.AreEqual((int)EnemyClass.Tanker, e.EnemyClass,
                                $"{name} 웨이브 {i + 1}: 묶음 가운데가 아닌데 「중장」에 탱커가 아닌 적이 섞였다");
                    }
                    if (!isMiddle || (!sawTanker && !sawOther)) continue;
                    Assert.IsTrue(sawTanker, $"{name} 웨이브 {i + 1}: 「중장」 가운데에서 탱커가 사라졌다 — 변주가 교체로 동작했다");
                    if (heavyHasVariant)
                        Assert.IsTrue(sawOther, $"{name} 웨이브 {i + 1}: 변주를 저작했는데 가운데가 여전히 순수 탱커다");
                }
            }
        }

        // 옛 WaveConceptAuthoringTests::ConceptsHoldForThreeWaves_OnEveryMapDeck — 라이브 덱도 3웨이브 유지
        [Test]
        public void 라이브_덱에서도_컨셉은_3웨이브_유지된다()
        {
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                for (int i = 0; i < plan.WaveCount; i++)
                    Assert.AreEqual(plan.Waves[(i / 3) * 3].ConceptLabel, plan.Waves[i].ConceptLabel,
                        $"{name} 웨이브 {i + 1}: 블록 안에서 컨셉이 바뀌었다");
            }
        }

        // 옛 WaveConceptAuthoringTests::RangedConcept_DropsOutOnSingleSpawnMaps — 스폰 1개 맵엔 「원거리」 없음
        [Test]
        public void 스폰_1개_맵에는_원거리가_안_나온다()
        {
            string rangedLabel = Concept("Concept_Ranged").displayName;
            foreach (var w in Plan("Deck_Serpent", 1).Waves)
                Assert.AreNotEqual(rangedLabel, w.ConceptLabel, "스폰 1개 맵은 협공을 받을 수 없다(입구 요구량 게이트)");
        }

        // 옛 WaveConceptAuthoringTests::EveryMapDeck_IsDeterministic — 맵 덱 3회 signature 일치
        [Test]
        public void 맵_덱은_결정론이다()
        {
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                string a = Signature(Plan(live, 3), live);
                Assert.AreEqual(a, Signature(Plan(live, 3), live), $"{name}: 2회차가 다르다");
                Assert.AreEqual(a, Signature(Plan(live, 3), live), $"{name}: 3회차가 다르다");
            }
        }

        private static string BlockSequence(WavePlan plan, int waves)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < waves && i < plan.WaveCount; i += 3) sb.Append(plan.Waves[i].ConceptLabel).Append('>');
            return sb.ToString();
        }

        // 옛 WaveConceptAuthoringTests::MapDecks_ProduceDifferentConceptSequences — 6맵 시퀀스가 전부 같지 않다
        [Test]
        public void 맵_덱마다_컨셉_시퀀스가_다르다()
        {
            var distinct = new HashSet<string>();
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                distinct.Add(BlockSequence(plan, plan.WaveCount));
            }
            Assert.Greater(distinct.Count, 1, "라이브 맵이 전부 같은 컨셉 순서면 waveSeed 가 컨셉 뽑기에 안 닿고 있다");
        }

        // 옛 WaveConceptAuthoringTests::SiegeDecks_MainPhase_ShowsFourPlusConcepts_IncludingAirstrike — 공성 본편 4종+ · 공습 포함
        [Test]
        public void 공성_덱_본편은_컨셉_4종_이상이고_공습을_포함한다([ValueSource(nameof(SiegeDecks))] string name)
        {
            var live = Load(name);
            var plan = Plan(live, SiegeLanes);
            string airLabel = Concept("Concept_Airstrike").displayName;
            int main = MainWaves(live);   // 본편 = break **이전** — break 웨이브는 클라이맥스의 첫 웨이브다
            var labels = new HashSet<string>();
            for (int i = 0; i < main && i < plan.WaveCount; i++) labels.Add(plan.Waves[i].ConceptLabel);
            Assert.GreaterOrEqual(labels.Count, 4, $"{name}: 본편(w1~{main})에 컨셉 4종+ — 현재 {string.Join(", ", labels)}");
            Assert.IsTrue(labels.Contains(airLabel), $"{name}: 본편에 공습이 없다 — 이 spec 의 출발 증상 재발");
        }

        // 옛 WaveConceptAuthoringTests::SiegeDecks_MainPhase_ShowsEveryNonBossEnemy — 공성 본편에 보스 외 전 종 등장
        [Test]
        public void 공성_덱_본편에_보스를_뺀_모든_적이_나온다([ValueSource(nameof(SiegeDecks))] string name)
        {
            var live = Load(name);
            var plan = Plan(live, SiegeLanes);
            int main = MainWaves(live);
            var pool = new HashSet<int>(live.WaveDeck.EnemyPool);
            var seen = new HashSet<int>();
            for (int i = 0; i < main && i < plan.WaveCount; i++)
                foreach (var g in plan.Waves[i].Groups)
                    if (pool.Contains(g.EnemyIndex)) seen.Add(g.EnemyIndex);   // 보스는 보스 풀 소속 — 세지 않는다

            var missing = new List<string>();
            foreach (int row in live.WaveDeck.EnemyPool) if (!seen.Contains(row)) missing.Add(live.Enemies[row].Id);
            Assert.IsEmpty(missing, $"{name}: 본편(w1~{main})에 안 나오는 적 — {string.Join(", ", missing)}. "
                                    + "그 클래스를 받는 컨셉 슬롯을 주거나 시드를 다시 골라라(전 종 시드 스캐너)");
        }

        // 옛 WaveConceptAuthoringTests::SiegeDecks_MainPhase_SequencesDiffer — 공성 3덱 본편 시퀀스가 서로 다르다
        [Test]
        public void 공성_덱마다_본편_시퀀스가_다르다()
        {
            var seqs = new Dictionary<string, string>();
            foreach (string name in SiegeDecks)
            {
                var live = Load(name);
                string seq = BlockSequence(Plan(live, SiegeLanes), MainWaves(live));
                foreach (var kv in seqs)
                    Assert.AreNotEqual(kv.Value, seq, $"{name} 과 {kv.Key} 의 본편 시퀀스가 같다 — 맵마다 다른 판이어야 한다");
                seqs[name] = seq;
            }
        }

        // 옛 WaveConceptAuthoringTests::Scan_FullRosterSeeds — 전 종 등장 시드 스캐너(수동). **코어 생성기가 정본**이다.
        [Test, Explicit("시드 재선정 때만 수동 실행 — enemy-wave-integration 스킬 「값 재도출」")]
        public void 시드_스캐너_본편_전_종_등장()
        {
            var live = Load("Deck_Duel");   // 공성 3덱은 풀·파라미터가 같아 후보가 서로 통용된다
            int main = MainWaves(live);
            string airLabel = Concept("Concept_Airstrike").displayName;
            var pool = new HashSet<int>(live.WaveDeck.EnemyPool);
            int best = 0, hits = 0;
            for (int seed = 20260850; seed <= 20264000; seed++)
            {
                var plan = Plan(live, SiegeLanes, seed);
                var seen = new HashSet<int>();
                var labels = new HashSet<string>();
                for (int i = 0; i < main && i < plan.WaveCount; i++)
                {
                    labels.Add(plan.Waves[i].ConceptLabel);
                    foreach (var g in plan.Waves[i].Groups) if (pool.Contains(g.EnemyIndex)) seen.Add(g.EnemyIndex);
                }
                if (seen.Count > best)
                {
                    best = seen.Count;
                    UnityEngine.Debug.Log($"[RosterScan] best {seed}: {seen.Count}/{pool.Count} 종, 컨셉 {labels.Count}");
                }
                if (seen.Count == pool.Count && labels.Count >= 4 && labels.Contains(airLabel) && ++hits <= 14)
                    UnityEngine.Debug.Log($"[RosterScan] FULL {seed}: 컨셉 {labels.Count}종 {BlockSequence(plan, main)}");
            }
            UnityEngine.Debug.Log($"[RosterScan] mainWaves={main} poolSize={pool.Count} 최대달성={best} 전종시드={hits}");
        }

        // 옛 WaveConceptAuthoringTests::Scan_SiegeSeedCandidates — 공성 시드 후보 스캐너(수동). **코어 생성기가 정본**이다.
        [Test, Explicit("시드 재선정 때만 수동 실행 — enemy-wave-integration 스킬 「값 재도출」")]
        public void 시드_스캐너_공성_후보()
        {
            var live = Load("Deck_Duel");
            string airLabel = Concept("Concept_Airstrike").displayName;
            int found = 0;
            for (int seed = 20260850; seed <= 20261300 && found < 24; seed++)
            {
                var plan = Plan(live, SiegeLanes, seed);
                var labels = new HashSet<string>();
                for (int i = 0; i < 15 && i < plan.WaveCount; i++) labels.Add(plan.Waves[i].ConceptLabel);
                if (labels.Count < 4 || !labels.Contains(airLabel)) continue;
                found++;
                UnityEngine.Debug.Log($"[SeedScan] {seed} [{labels.Count}종] {BlockSequence(plan, 15)}");
            }
            Assert.GreaterOrEqual(found, 3, "후보가 3개 미만 — 스캔 범위를 넓혀라");
        }

        // ════════════════════════════════════════════════════════════════════
        // 25 · 킬 예산 구조 불변식 (옛 WaveKillBudgetPinTests)
        // ════════════════════════════════════════════════════════════════════

        // 옛 WaveKillBudgetPinTests::DeckSeed_IsPinned_SoEveryPlayerGetsTheSameSchedule — 시드 고정(정의표까지)
        [Test]
        public void 킬_예산_덱의_시드가_고정돼_모두가_같은_스케줄을_받는다()
        {
            Assert.AreNotEqual(0, Load(KillBudgetDeck).WaveDeck.WaveSeed,
                "0 이면 매치마다 스케줄이 달라져 점수를 서로 비교할 수 없다");
        }

        // 옛 WaveKillBudgetPinTests::Generation_IsDeterministic_ForTheSameSeed — 같은 시드 = 같은 스폰 수
        [Test]
        public void 킬_예산_덱은_같은_시드에서_결정론이다()
        {
            var live = Load(KillBudgetDeck);
            var a = Plan(live, 2);
            var b = Plan(live, 2);
            Assert.AreEqual(a.WaveCount, b.WaveCount, "웨이브 수");
            Assert.AreEqual(Signature(a, live), Signature(b, live));
        }

        // 옛 WaveKillBudgetPinTests::BossWaves_LandOnTheConfiguredInterval — 보스는 저작 간격에만
        [Test]
        public void 킬_예산_덱의_보스는_저작_간격에만_온다()
        {
            var live = Load(KillBudgetDeck);
            int interval = live.WaveDeck.BossWaveInterval;
            Assert.Greater(interval, 0, "전제: 보스 간격이 저작돼 있다");
            var plan = Plan(live, 2);
            for (int i = 0; i < plan.WaveCount; i++)
            {
                bool hasBoss = false;
                foreach (var g in plan.Waves[i].Groups) if (IsBoss(live.Enemies[g.EnemyIndex])) hasBoss = true;
                Assert.AreEqual((i + 1) % interval == 0, hasBoss, $"웨이브 {i + 1} 보스 여부");
            }
        }

        // 옛 WaveKillBudgetPinTests::KillBudget_ComesFromActualSpawns — 킬 예산은 실제 스폰에서(상수 아님)
        [Test]
        public void 킬_예산은_실제_스폰_구성에서_나온다()
        {
            var live = Load(KillBudgetDeck);
            var plan = Plan(live, 2);
            int spawns = 0, bosses = 0;
            foreach (var w in plan.Waves)
                foreach (var g in w.Groups)
                {
                    spawns += g.Count;
                    if (IsBoss(live.Enemies[g.EnemyIndex])) bosses += g.Count;
                }
            Assert.Greater(spawns, 0, "스폰이 하나도 없다 — 킬 예산이 0 이라는 뜻이다");
            Assert.Greater(bosses, 0, "보스가 하나도 없다 — 보스 편성 계약 확인");
        }

        // 옛 WaveKillBudgetPinTests::SpawnWindow_FitsInsideTheWaveIntervalCap — 스폰 창 < 상한 간격(전멸 즉시 진행)
        [Test]
        public void 스폰_창은_상한_간격_안에_끝난다()
        {
            foreach (string name in MapDecks)
            {
                var live = Load(name);
                ref var d = ref live.WaveDeck;
                Assert.Greater(d.MaxWaveIntervalSec, 0f, $"{name}: 상한 간격이 0 이면 케이던스가 폭주한다");
                float window = d.SpawnLeadInSec + (d.MaxUnitsPerWave - 1) * d.IntraWaveSpacingSec;
                Assert.Less(window, d.MaxWaveIntervalSec,
                    $"{name}: 스폰 창 {window:F2}s ≥ 상한 간격 {d.MaxWaveIntervalSec}s — 전멸 즉시 진행이 성립하지 않는다");

                // 코어 생성기도 같은 술어로 loud 하게 경고한다 — 라이브 덱에서 그 경고가 한 번도 안 울려야 한다.
                var warnings = new List<string>();
                Plan(live, 3, report: warnings.Add);
                foreach (var m in warnings)
                    StringAssert.DoesNotContain("스폰 창", m, $"{name}: 생성기가 스폰 창 위반을 보고했다");
            }
        }
    }
}
