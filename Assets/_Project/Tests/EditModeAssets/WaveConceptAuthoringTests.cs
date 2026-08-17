using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-concept-blocks unit 4 — 저작된 컨셉 5종과 라이브 덱 배선.
    //
    // 여기가 «컨셉이 실제로 켜졌는가»의 정본이다. 기계(unit 2)가 다 돌아도 덱이 컨셉을
    // 참조하지 않으면 6맵은 그대로 랜덤 2종이고, 그 상태에서 다른 테스트는 전부 초록이다.
    public class WaveConceptAuthoringTests
    {
        private const string ConceptDir = "Assets/_Project/Data/WaveConcepts";
        private const string DeckDir = "Assets/_Project/Scripts/Data/Decks";
        private const string SkimmerPath = "Assets/_Project/Data/Enemies/Enemy_Skimmer.asset";

        private static readonly string[] MapDecks =
        {
            "Deck_Serpent", "Deck_Coil", "Deck_Twin", "Deck_Spiral", "Deck_Zig", "Deck_Hook",
        };

        private static readonly string[] AllDecks =
        {
            "Deck_Serpent", "Deck_Coil", "Deck_Twin", "Deck_Spiral", "Deck_Zig", "Deck_Hook",
            "Deck_Endless",
        };

        private static AttackDeck Deck(string name)
        {
            var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>($"{DeckDir}/{name}.asset");
            Assert.IsNotNull(deck, $"덱 에셋을 찾지 못했다: {name}");
            return deck;
        }

        private static WaveConceptData Concept(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<WaveConceptData>($"{ConceptDir}/{name}.asset");
            Assert.IsNotNull(c, $"컨셉 에셋을 찾지 못했다: {name}");
            return c;
        }

        private static bool IsAir(AttackUnitData u) =>
            u != null && (u.EffectiveTraversalLayers & PlacementLayer.Air) != 0;

        // ---------------- 컨셉 에셋 저작 ----------------

        [Test]
        public void FiveConcepts_Exist_WithReadableLabels()
        {
            string[] names =
            {
                "Concept_Spread", "Concept_Swarm", "Concept_Heavy",
                "Concept_Ranged", "Concept_Airstrike",
            };
            foreach (string name in names)
            {
                var c = Concept(name);
                Assert.IsNotEmpty(c.id, $"{name}: id 가 비었다");
                Assert.IsNotEmpty(c.displayName,
                    $"{name}: displayName 이 비면 브리핑·도크에 라벨이 안 나온다");
                Assert.Greater(c.slots.Length, 0, $"{name}: 슬롯이 없으면 편성을 못 만든다");
            }
        }

        [Test]
        public void Spread_IsTheOnlyConceptAvailableInBlockZero()
        {
            // 블록 0 고정은 특수 분기가 아니라 게이트로 성립한다 — 「평소」만 minWaveNumber 1.
            Assert.AreEqual(1, Concept("Concept_Spread").minWaveNumber);
            foreach (string name in new[]
                     { "Concept_Swarm", "Concept_Heavy", "Concept_Ranged", "Concept_Airstrike" })
                Assert.Greater(Concept(name).minWaveNumber, 3,
                    $"{name}: 블록 0(웨이브 1~3)에 들어오면 온보딩이 깨진다");
        }

        [Test]
        public void Spread_StaysOnTheGround()
        {
            foreach (var slot in Concept("Concept_Spread").slots)
                Assert.AreEqual(SlotAltitude.Ground, slot.altitude,
                    "「평소」가 비행을 뽑으면 대공 없는 첫 3웨이브에서 막을 수 없는 적이 나온다");
        }

        [Test]
        public void Heavy_IsASingleTankerSlot()
        {
            var heavy = Concept("Concept_Heavy");
            Assert.AreEqual(1, heavy.slots.Length,
                "2종을 만들려고 Bruiser 슬롯을 붙이면 2.0~2.5 가 섞여 벽이 흩어진다");
            Assert.AreEqual(EnemyClass.Tanker, heavy.slots[0].classFilter);
            Assert.Less(heavy.countMul, 1f, "단단한 성질은 수량을 줄여야 난이도가 성질에 안 끌려간다");
        }

        [Test]
        public void Ranged_IsAPincerOfShooters()
        {
            var ranged = Concept("Concept_Ranged");
            Assert.AreEqual(2, ranged.RequiredLaneCount, "원거리는 협공 위상");
            foreach (var slot in ranged.slots)
            {
                Assert.AreEqual(EnemyClass.Shooter, slot.classFilter);
                Assert.AreEqual(SlotAltitude.Ground, slot.altitude,
                    "고도와 성질은 직교한다 — Ground 를 명시하지 않으면 비행 Shooter 가 섞인다");
            }
            Assert.GreaterOrEqual(ranged.minWaveNumber, 7,
                "방어선을 깎는 압력이라 게이트를 늦게 둔다(실측으로 판정)");
        }

        [Test]
        public void Airstrike_IsTwoAirSlots_AndFew()
        {
            var air = Concept("Concept_Airstrike");
            // unit 7 — 슬롯 1 → 2. Air 로스터에 maxPerWave 1 인 드래곤이 들어오면서, 단일 슬롯이면
            // 드래곤을 뽑는 순간 웨이브가 1기로 붕괴한다(잘린 몫을 넘길 슬롯이 없다).
            Assert.AreEqual(2, air.slots.Length,
                "슬롯이 하나면 엘리트(maxPerWave 1)를 뽑을 때 웨이브가 1기로 붕괴한다");
            foreach (var slot in air.slots)
                Assert.AreEqual(SlotAltitude.Air, slot.altitude);
            Assert.AreEqual(1, air.RequiredLaneCount, "공습은 한 입구로 온다");
            Assert.Less(air.countMul, 0.5f,
                "소수여야 «스킬 한 발 값»으로 번역된다 — 많으면 무력감이 된다");
        }

        // wave-pull-revival unit 2 — 변주가 **고도를 교차하지 못하게** 구조로 막는다.
        //
        // 이 코드베이스에는 같은 위험을 구조로 막은 선례가 있다: `SlotAltitude` 에 `Any` 를
        // 두지 않아 「평소」가 비행을 뽑는 사고가 아예 성립하지 않게 한 것(WaveConceptData.cs).
        // 「공습 가운데에 지상을 섞지 않는다」는 그 거울상인데(대공에 투자한 플레이어가 그
        // 웨이브만 헛돈다) 주석으로만 막으면 몇 달 뒤 누가 넣어도 전부 초록이다.
        //
        // classFilter 변주는 자유다 — 막는 것은 Ground↔Air 교차뿐이다.
        [Test]
        public void VariantSlots_NeverCrossAltitude()
        {
            string[] names =
            {
                "Concept_Spread", "Concept_Swarm", "Concept_Heavy",
                "Concept_Ranged", "Concept_Airstrike",
            };
            foreach (string name in names)
            {
                var c = Concept(name);
                if (c.variantSlots == null || c.variantSlots.Length == 0) continue;

                var mainAltitudes = new HashSet<SlotAltitude>();
                foreach (var slot in c.slots)
                    if (slot != null) mainAltitudes.Add(slot.altitude);

                foreach (var slot in c.variantSlots)
                {
                    if (slot == null) continue;
                    Assert.IsTrue(mainAltitudes.Contains(slot.altitude),
                        $"{name}: 변주가 본 편성에 없는 고도({slot.altitude})를 끌어온다 — " +
                        "「공습」 가운데 지상이 섞이면 대공 배치가 그 웨이브만 헛돈다");
                }
            }
        }

        [Test]
        public void Airstrike_HasNoVariant()
        {
            var air = Concept("Concept_Airstrike");
            Assert.IsTrue(air.variantSlots == null || air.variantSlots.Length == 0,
                "「공습」은 countMul 0.3 의 소수 압박이라 3연속이어도 지루하지 않다. " +
                "변주를 넣으면 위 고도 교차 규칙과 정면으로 부딪힌다");
        }

        [Test]
        public void Swarm_IsRunnerClass_WithHigherCount()
        {
            var swarm = Concept("Concept_Swarm");
            foreach (var slot in swarm.slots)
                Assert.AreEqual(EnemyClass.Runner, slot.classFilter);
            Assert.Greater(swarm.countMul, 1f, "처리량 문제여야 하므로 수량을 올린다");
            Assert.AreEqual(1, swarm.RequiredLaneCount, "벌떼는 한 입구로 쏟아진다");
        }

        // ---------------- 덱 배선 ----------------

        [Test]
        public void EveryLiveDeck_ReferencesAllFiveConcepts()
        {
            foreach (string name in AllDecks)
            {
                var deck = Deck(name);
                Assert.AreEqual(5, deck.waveConceptPool.Length, $"{name}: 컨셉 풀 크기");
                foreach (var c in deck.waveConceptPool)
                    Assert.IsNotNull(c, $"{name}: 컨셉 참조가 비었다(GUID 유실)");
                Assert.AreEqual(3, deck.conceptHoldWaves, $"{name}: 블록 길이");
            }
        }

        [Test]
        public void MapDecks_IncludeSkimmer_AndNotAtTheEnd()
        {
            var skimmer = AssetDatabase.LoadAssetAtPath<AttackUnitData>(SkimmerPath);
            Assert.IsNotNull(skimmer, SkimmerPath);

            foreach (string name in MapDecks)
            {
                var pool = Deck(name).attackUnitPool;
                int index = System.Array.IndexOf(pool, skimmer);
                Assert.GreaterOrEqual(index, 0, $"{name}: Skimmer 가 풀에 없으면 「공습」이 성립하지 않는다");
                Assert.Less(index, pool.Length - 1,
                    $"{name}: 맨 뒤면 ResolveWaveEligibleIndex 전방 순환이 초반 웨이브를 pool[0] 로 쏠리게 한다");
            }
        }

        // unit 7 — 구 `EveryLiveDeck_HasExactlyOneAirUnit_ForNow` 를 교체했다. 그 단언은
        // 「Air 가 늘면 계약 3 을 다시 보라」는 **알람**이었고, 드래곤 편입으로 실제로 울렸다.
        // 이제 개수가 아니라 계약이 지키려던 것(속도 폭)을 직접 잰다 — Skimmer 2.5 · Dragon 2.0.
        [Test]
        public void AirRoster_StaysTightEnoughToClump()
        {
            foreach (string name in AllDecks)
            {
                float min = float.MaxValue, max = 0f;
                int air = 0;
                foreach (var u in Deck(name).attackUnitPool)
                {
                    if (!IsAir(u)) continue;
                    air++;
                    if (u.moveSpeed < min) min = u.moveSpeed;
                    if (u.moveSpeed > max) max = u.moveSpeed;
                }
                Assert.Greater(air, 0, $"{name}: Air 가 하나도 없으면 「공습」이 fail-open 으로 지상을 뽑는다");
                Assert.LessOrEqual(max - min, 1.5f,
                    $"{name}: Air 속도 폭 {max - min:0.#} — 「공습」이 흩어진다(계약 3). " +
                    "슬롯을 성질로 더 좁히거나 컨셉을 쪼개라");
            }
        }

        // unit 7 — 엘리트는 `maxPerWave = 1` 이다. **단일 슬롯 컨셉이 엘리트를 뽑으면 웨이브가
        // 1기로 붕괴한다** — ClampGroupCounts 는 잘린 몫을 다른 슬롯으로 넘기는데 넘길 곳이 없다.
        // 그래서 「공습」을 슬롯 2개로 늘렸고, 이 단언이 그 회귀를 막는다.
        // unit 8 — 저작 가드. 컨셉의 게이트 웨이브에서 필터를 통과하는 서로 다른 후보가
        // 슬롯 수만큼 있어야 한다(전원 상한 없음이면 중복 픽이 무해라 면제). 이게 깨지면
        // fail-open ②(중복배제 해제)가 같은 유닛을 두 슬롯에 넣고, ClampGroupCounts 는
        // 슬롯별 적용이라 **maxPerWave 가 슬롯 수만큼 곱해진다** — 「공습」 w4~7 의
        // Dragon×1+Dragon×1 이 그렇게 태어났다(unit 7 의 「중복 배제로 Dragon+Skimmer」
        // 전제가 그 창에서 거짓). 게이트 웨이브만 보면 충분하다 — minWaveNumber 는
        // 웨이브가 오를수록 후보를 추가만 한다.
        [Test]
        public void ConceptSlots_HaveEnoughDistinctCandidates_AtTheirGateWave()
        {
            foreach (string name in AllDecks)
            {
                var deck = Deck(name);
                if (deck.waveConceptPool == null) continue;
                foreach (var concept in deck.waveConceptPool)
                {
                    if (concept == null || concept.slots == null) continue;
                    AssertSlotSetHasCandidates(
                        name, concept, concept.slots, concept.minWaveNumber, "본 편성");
                    // 변주는 블록 가운데 웨이브(게이트+1)에 본 편성 위로 **끼어든다** —
                    // 검사 대상은 합쳐진 슬롯 집합이다. holdWaves < 3 이면 변주가 없다.
                    if (deck.conceptHoldWaves >= 3
                        && concept.variantSlots != null && concept.variantSlots.Length > 0)
                    {
                        var combined = new List<WaveConceptSlot>(concept.slots);
                        combined.AddRange(concept.variantSlots);
                        AssertSlotSetHasCandidates(
                            name, concept, combined.ToArray(), concept.minWaveNumber + 1, "변주(본+삽입)");
                    }
                }
            }
        }

        private static void AssertSlotSetHasCandidates(
            string deckName, WaveConceptData concept, WaveConceptSlot[] slots,
            int waveNumber, string label)
        {
            var pool = Deck(deckName).attackUnitPool;
            int slotCount = 0;
            foreach (var s in slots) if (s != null) slotCount++;
            if (slotCount == 0) return;

            // 슬롯별 필터가 다를 수 있으므로(성질×고도) 슬롯마다 후보를 모아 합집합의
            // distinct 를 센다 — PickSlotUnitIndex 의 중복배제가 그 합집합 위에서 돈다.
            var distinct = new HashSet<AttackUnitData>();
            bool allCapFree = true;
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                foreach (var u in pool)
                {
                    if (u == null || u.minWaveNumber > waveNumber) continue;
                    if (slot.classFilter != EnemyClass.None && u.enemyClass != slot.classFilter) continue;
                    if (!WavePatternGenerator.MatchesAltitude(u, slot.altitude)) continue;
                    distinct.Add(u);
                    if (u.maxPerWave > 0) allCapFree = false;
                }
            }

            Assert.Greater(distinct.Count, 0,
                $"{deckName} 컨셉 '{concept.id}' {label}: 게이트 웨이브 {waveNumber} 에 후보가 0이다 " +
                "— 컨셉이 열리는 순간 성질/고도 fail-open 으로만 편성된다");
            if (distinct.Count >= slotCount || allCapFree) return;
            Assert.Fail(
                $"{deckName} 컨셉 '{concept.id}' {label}: 게이트 웨이브 {waveNumber} 의 서로 다른 후보 " +
                $"{distinct.Count} < 슬롯 {slotCount} 인데 상한 있는 유닛이 섞여 있다 — 중복 픽이 " +
                "maxPerWave 를 슬롯 수만큼 곱한다(컨셉/유닛의 minWaveNumber 게이트를 맞춰라)");
        }

        [Test]
        public void EliteWaves_DoNotCollapseToASingleUnit()
        {
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    bool hasElite = false;
                    // unit 8 — maxPerWave 는 「종류별」 상한이다. 그룹별로 재면 fail-open 중복
                    // 픽(같은 유닛 2슬롯)이 각자 상한 안이라 초록으로 샌다 — 종류합으로 잰다.
                    var perUnit = new Dictionary<AttackUnitData, int>();
                    for (int g = 0; g < w.groups.Count; g++)
                    {
                        var u = w.groups[g].unit;
                        if (u == null) continue;
                        if (u.tier == EnemyTier.Elite) hasElite = true;
                        perUnit[u] = (perUnit.TryGetValue(u, out int c) ? c : 0) + w.groups[g].count;
                    }
                    foreach (var kv in perUnit)
                    {
                        if (kv.Key.maxPerWave <= 0) continue;
                        Assert.LessOrEqual(kv.Value, kv.Key.maxPerWave,
                            $"{name} 웨이브 {i + 1}('{w.conceptLabel}'): {kv.Key.id} 종류합 {kv.Value} 이 " +
                            $"maxPerWave {kv.Key.maxPerWave} 를 넘었다 — 중복 픽이 상한을 곱했다");
                    }
                    if (!hasElite) continue;
                    Assert.Greater(w.totalCount, 1,
                        $"{name} 웨이브 {i + 1}('{w.conceptLabel}'): 엘리트가 뽑혀 웨이브가 1기로 붕괴했다 " +
                        "— 그 컨셉의 슬롯이 하나뿐이라 잘린 몫을 넘길 곳이 없다");
                }
            }
        }

        [Test]
        public void SlimeOffspring_NeverEnterThePool()
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
                foreach (string name in AllDecks)
                    Assert.AreEqual(-1, System.Array.IndexOf(Deck(name).attackUnitPool, child),
                        $"{name}: {path} 는 **분열로만 태어나는 파생물**이다 — 정규 편성에 " +
                        "직접 넣으면 부모 없이 튀어나온다(각성도 0이라 보상 없는 적이 웨이브를 채운다)");
            }
        }

        [Test]
        public void GeneratorVersion_IsBumped_SoTheNewBaselineIsVisible()
        {
            // wave-pull-revival unit 2 — 3 → 4. 묶음 가운데 변주가 들어가면서 컨셉을 쓰는
            // 덱의 편성이 다시 바뀌었다(변주를 저작한 컨셉이 있는 웨이브부터 슬롯 수가 달라져
            // 이후 rng 소비가 이동한다). 계약 8 대로 버전으로 표시한다.
            foreach (string name in AllDecks)
                // 3 = 컨셉 도입(wave-concept-blocks unit 2)
                // 4 = 엘리트 2종 편입(unit 7) **그리고** 묶음 가운데 변주(wave-pull-revival
                //     unit 2) — 두 작업이 origin 과 로컬에서 **각자 4를 찍었다**
                // 5 = 그 둘의 병합. 어느 쪽 4 와도 다른 세 번째 baseline 이라 다시 올린다.
                // 6 = Skimmer 게이트 8→4(unit 8) — 공습 w4~7 창의 Dragon 중복 픽 해소.
                //     풀 불변이라 waveSeed 는 유지, 편성만 바뀌어 버전으로 표시한다.
                // 7 = 공습 상한 상향 Dragon 1→2·Skimmer 2→4 (wave-ramp-two-phase unit 2) —
                //     rng 불변(재추첨 없음), 공습 웨이브 수량만 는다. 시드 유지, v6 과 같은 판단.
                // 풀이나 편성 규칙이 바뀔 때마다 올린다.
                Assert.AreEqual(7, Deck(name).waveGeneratorVersion,
                    $"{name}: 풀/편성이 바뀌었다 — 버전으로 새 baseline 을 표시한다");
        }

        [Test]
        public void WaveSeeds_ArePinnedAndUnique()
        {
            var seen = new Dictionary<int, string>();
            foreach (string name in AllDecks)
            {
                int seed = Deck(name).waveSeed;
                Assert.AreNotEqual(0, seed, $"{name}: 0 이면 매판 달라진다");
                Assert.IsFalse(seen.ContainsKey(seed),
                    $"{name} 과 {(seen.ContainsKey(seed) ? seen[seed] : "")} 가 같은 시드를 쓴다");
                seen[seed] = name;
            }
        }

        // ---------------- 생성 결과 ----------------

        private static GeneratedWavePlan Plan(string deck, int laneCount) =>
            WavePatternGenerator.Generate(Deck(deck), Deck(deck).waveSeed, laneCount);

        [Test]
        public void FirstBlock_IsAlwaysSpread()
        {
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 2);
                for (int i = 0; i < 3; i++)
                    Assert.AreEqual("평소", plan.waves[i].conceptLabel,
                        $"{name} 웨이브 {i + 1}: 첫 접촉은 익숙해야 한다");
            }
        }

        [Test]
        public void NoAirEnemy_AppearsOutsideAirstrikeBlocks()
        {
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if (w.conceptLabel == "공습") continue;
                    for (int g = 0; g < w.groups.Count; g++)
                        Assert.IsFalse(IsAir(w.groups[g].unit),
                            $"{name} 웨이브 {i + 1}('{w.conceptLabel}'): 지상 컨셉에 비행이 섞였다");
                }
            }
        }

        [Test]
        public void AirstrikeBlocks_AreAllAir()
        {
            bool sawAirstrike = false;
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if (w.conceptLabel != "공습") continue;
                    sawAirstrike = true;
                    for (int g = 0; g < w.groups.Count; g++)
                    {
                        var u = w.groups[g].unit;
                        // 보스 웨이브의 선봉은 보스다(지상) — 호위만 본다.
                        if (u != null && u.tier == EnemyTier.Boss) continue;
                        Assert.IsTrue(IsAir(u),
                            $"{name} 웨이브 {i + 1}: 「공습」에 지상이 섞였다");
                    }
                }
            }
            Assert.IsTrue(sawAirstrike, "6맵 100웨이브에 「공습」이 한 번도 안 나왔다 — 가중치/게이트 확인");
        }

        [Test]
        // wave-pull-revival unit 2 — 계약 개정: 「중장」 묶음의 **첫·마지막** 웨이브는 여전히
        // 순수 탱커지만, **가운데**에는 변주가 끼어든다(저작 = Concept_Heavy.variantSlots).
        //
        // 왜 순수를 통째로 포기하지 않는가: 가운데도 탱커를 계속 포함해야(삽입이지 교체가
        // 아니다) 「중장 블록」이라는 라벨이 거짓말이 되지 않는다. 그래서 가운데는
        // 「탱커가 있다 + 탱커 아닌 것도 있다」를 함께 단언한다.
        public void HeavyBlocks_AreTankersExceptTheMiddleVariant()
        {
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    var w = plan.waves[i];
                    if (w.conceptLabel != "중장") continue;

                    bool isMiddle = i % 3 == 1;
                    bool sawTanker = false, sawOther = false;
                    for (int g = 0; g < w.groups.Count; g++)
                    {
                        var u = w.groups[g].unit;
                        if (u == null || u.tier == EnemyTier.Boss) continue;
                        if (u.enemyClass == EnemyClass.Tanker) sawTanker = true;
                        else sawOther = true;

                        if (!isMiddle)
                            Assert.AreEqual(EnemyClass.Tanker, u.enemyClass,
                                $"{name} 웨이브 {i + 1}: 묶음 가운데가 아닌데 「중장」에 탱커가 아닌 적이 섞였다");
                    }

                    if (!isMiddle || (!sawTanker && !sawOther)) continue;

                    Assert.IsTrue(sawTanker,
                        $"{name} 웨이브 {i + 1}: 「중장」 가운데에서 탱커가 사라졌다 — " +
                        "변주가 «삽입»이 아니라 «교체»로 동작했다");
                    // 「탱커가 남아 있다」만 보면 **변주를 통째로 지워도 초록**이다. 저작이
                    // 있는 컨셉은 가운데에 탱커 아닌 것이 실제로 들어왔는지도 함께 단언한다
                    // (완화 ladder 가 변주 클래스를 못 뽑아 탱커로 fail-open 한 경우도 잡는다).
                    if (Concept("Concept_Heavy").variantSlots.Length > 0)
                        Assert.IsTrue(sawOther,
                            $"{name} 웨이브 {i + 1}: 「중장」에 변주를 저작했는데 가운데가 여전히 " +
                            "순수 탱커다 — 변주가 편성에 반영되지 않았다");
                }
            }
        }

        [Test]
        public void ConceptsHoldForThreeWaves_OnEveryMapDeck()
        {
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                for (int i = 0; i < plan.waves.Count; i++)
                {
                    string blockLabel = plan.waves[(i / 3) * 3].conceptLabel;
                    Assert.AreEqual(blockLabel, plan.waves[i].conceptLabel,
                        $"{name} 웨이브 {i + 1}: 블록 안에서 컨셉이 바뀌었다");
                }
            }
        }

        [Test]
        public void RangedConcept_DropsOutOnSingleSpawnMaps()
        {
            var plan = Plan("Deck_Serpent", 1);
            foreach (var w in plan.waves)
                Assert.AreNotEqual("원거리", w.conceptLabel,
                    "스폰 1개 맵은 협공을 받을 수 없다(lane 요구량 게이트)");
        }

        private static string Signature(GeneratedWavePlan plan)
        {
            var sb = new StringBuilder();
            foreach (var w in plan.waves)
            {
                sb.Append(w.conceptLabel).Append('|');
                foreach (var g in w.groups)
                    sb.Append(g.unit != null ? g.unit.id : "-").Append(':')
                      .Append(g.count).Append('@').Append(g.laneIndex).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        [Test]
        public void EveryMapDeck_IsDeterministic()
        {
            foreach (string name in MapDecks)
            {
                string a = Signature(Plan(name, 3));
                string b = Signature(Plan(name, 3));
                string c = Signature(Plan(name, 3));
                Assert.AreEqual(a, b, $"{name}: 2회차가 다르다");
                Assert.AreEqual(a, c, $"{name}: 3회차가 다르다");
            }
        }

        // 맵마다 다른 컨셉 시퀀스가 나와야 «맵마다 고정된 적 패턴»이 의미를 갖는다.
        [Test]
        public void MapDecks_ProduceDifferentConceptSequences()
        {
            var seqs = new Dictionary<string, string>();
            foreach (string name in MapDecks)
            {
                var plan = Plan(name, 3);
                var sb = new StringBuilder();
                for (int i = 0; i < plan.waves.Count; i += 3) sb.Append(plan.waves[i].conceptLabel).Append('>');
                seqs[name] = sb.ToString();
            }

            var distinct = new HashSet<string>(seqs.Values);
            Assert.Greater(distinct.Count, 1,
                "6맵이 전부 같은 컨셉 순서면 waveSeed 가 컨셉 뽑기에 안 닿고 있다");
        }
    }
}
