using System.Collections.Generic;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-structures unit 1 — 저작 타겟 마스크. 계약 2 의 «저작 의도» 쪽.
    //
    // 폴백(None=0 → 레거시 마스크)은 **마이그레이션이 아니라 «빈 값» 방어선**이다.
    // 실측(2026-08-09): YAML 에 키가 없는 신규 SO 필드는 필드 이니셜라이저 값을 유지한다
    // — 0 으로 로드되지 않는다. 그래서 기존 에셋의 무회귀는 이니셜라이저가 보장하고,
    // 폴백은 «저작자가 인스펙터에서 마스크를 비웠을 때 조용히 무장 해제되는 것» 을 막는다.
    // 아래 에셋 훑기 테스트는 두 경로(이니셜라이저 / 폴백) 어느 쪽이든 마스크가 0 이 아님을
    // 단정하므로, 로드 규칙이 미래에 바뀌어도 무장 해제 회귀를 잡는다.
    public class AuthoredTargetMaskTests
    {
        // 적의 기본 타겟은 **상대 진영 전부**다 — 열거가 아니라 파생 그룹으로 적는다.
        //
        // 이 단언의 값어치는 «29 인지» 가 아니라 «방어측 종류가 늘면 자동으로 그것을 요구하는가»
        // 다. 방어 진영에 새 종류가 추가되면 `Factions.AnyDefender` 가 커지고 이 테스트가
        // 기본값에게 그것을 덮으라고 요구한다 — 종류를 추가한 사람이 이 파일을 몰라도 된다.
        // (2026-08-12: 열거로 적혀 있던 탓에 방어 본능이 «아무 적도 못 보는 무적 포탑» 이었다.)
        [Test]
        public void DefaultEnemyMask_CoversWholeOpposingSide()
        {
            Assert.AreEqual(Factions.AnyDefender,
                EnemyTargetDefaults.DefaultEnemyMask & Factions.AnyDefender,
                "기본 마스크가 방어 진영의 어떤 종류를 빠뜨리면 그 종류는 전 적에게 무적이 된다");
            Assert.AreNotEqual(0,
                EnemyTargetDefaults.DefaultEnemyMask & (int)Faction.BlockingHazard,
                "방벽을 빼면 완전 봉쇄에서 적이 벽을 못 부숴 영구 교착된다");
        }

        // 저작 = «이 적은 특수하다» 는 선언이다. 특수하지 않은 적은 기본값을 그대로 쓴다.
        // 목록을 늘리려면 그 적이 왜 특수한지 여기 적어야 한다 — 그게 이 테스트의 역할이다.
        [Test]
        public void OnlySpecialEnemies_NarrowTheirTargets()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AttackUnitData"))
            {
                var so = AssetDatabase.LoadAssetAtPath<AttackUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (so == null) continue;
                int mask = EnemyTargetDefaults.Resolve((int)so.targetFactions);
                if (mask == EnemyTargetDefaults.DefaultEnemyMask) continue;

                // 마음사냥꾼 — 유닛을 노리지 않는 것이 정체성이고, 그래서 도발도 안 걸린다.
                Assert.AreEqual("heartseeker", so.id,
                    $"'{so.id}' 가 기본값을 좁혔다. 특수 타게팅이 의도라면 이 목록에 근거와 함께 추가하라");
                Assert.AreEqual(0, mask & Factions.AnyUnit, "마음사냥꾼은 유닛을 노리지 않는다(도발 면역의 근거)");
                Assert.AreEqual(Factions.AnyStructure & Factions.AnyDefender,
                    mask & Factions.AnyDefender,
                    "마음사냥꾼은 방어측 **거점 전부**(마음·본능)를 노린다 — 절반만 노리면 «거점 전담» 이 거짓말이다");
            }
        }

        [Test]
        public void Resolve_Unauthored_FallsBackToDefaultMask()
        {
            Assert.AreEqual(EnemyTargetDefaults.DefaultEnemyMask,
                EnemyTargetDefaults.Resolve((int)Faction.None),
                "0 = 미저작 → 기본값. 이게 없으면 인스펙터에서 비운 적이 무장 해제된다");
        }

        [Test]
        public void Resolve_Authored_IsRespectedVerbatim()
        {
            Assert.AreEqual((int)Faction.DefenderCore,
                EnemyTargetDefaults.Resolve((int)Faction.DefenderCore),
                "저작값은 그대로 — «거점만 때리는 적» 이 표현 가능해야 한다");

            int both = (int)(Faction.DefenderUnit | Faction.DefenderCore);
            Assert.AreEqual(both, EnemyTargetDefaults.Resolve(both));
        }

        // 실제 에셋 훑기 — 저작 필드 도입 후 12종이 전부 «현행 동치» 로 풀리는지.
        // 개별 적을 다르게 저작하는 순간 이 테스트는 «0 이 아니면 그 값 그대로» 만 지킨다.
        [Test]
        public void AllEnemyAssets_ResolveToNonZeroMask()
        {
            var guids = AssetDatabase.FindAssets("t:AttackUnitData");
            Assert.Greater(guids.Length, 0, "적 SO 를 하나도 못 찾았다 — 경로/타입 확인");

            var unauthored = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
                if (so == null) continue;

                int resolved = EnemyTargetDefaults.Resolve((int)so.targetFactions);
                Assert.AreNotEqual(0, resolved,
                    $"{so.name}: 해석된 마스크가 0 이면 이 적은 아무것도 못 때린다");

                if (so.targetFactions == Faction.None)
                {
                    unauthored.Add(so.name);
                    Assert.AreEqual(EnemyTargetDefaults.DefaultEnemyMask, resolved,
                        $"{so.name}: 미저작은 기본값(상대 진영 전부)과 동치여야 한다");
                }
            }
            // 진단용 — 미저작 목록이 곧 «아직 저작 안 한 적» 이다. 실패 조건은 아니다.
            if (unauthored.Count > 0)
                UnityEngine.Debug.Log($"[unit 1] 미저작 적 {unauthored.Count}종 → 기본값 폴백: {string.Join(", ", unauthored)}");
        }

        // 검증 질문 — 저작만으로 «거점 전담 적» 이 성립하는가.
        [Test]
        public void StructureOnlyMask_Enemy_IgnoresDefenderUnitInRange()
        {
            using var world = new World("AuthoredTargetMaskTests_StructureOnly");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            // 거점 전담 적 — 저작 마스크가 DefenderCore 단독.
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = 10f, max = 10f });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(enemy);
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddComponentData(enemy, new AttackState
            {
                range = 5f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                targetMask = EnemyTargetDefaults.Resolve((int)Faction.DefenderCore),
            });
            em.AddComponentData(enemy, new EnemyTargetFilter
            {
                classMask = -1,
                priorityClass = -1,
                factionMask = (int)Faction.DefenderCore,
            });
            var outputs = em.AddBuffer<AttackOutputElement>(enemy);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 4f },
            });

            // 방어유닛이 **더 가깝다** — 거리로는 이기지만 마스크에 없어 후보가 아니다.
            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.AddComponentData(defender, new Health { value = 100f, max = 100f });
            em.AddComponentData(defender, new FactionTag { value = Faction.DefenderUnit });
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddBuffer<IncomingDamage>(defender);

            // 마음은 더 멀다.
            var core = em.CreateEntity();
            em.AddComponentData(core, LocalTransform.FromPosition(new float3(3f, 0f, 0f)));
            em.AddComponentData(core, new Health { value = 100f, max = 100f });
            em.AddComponentData(core, new FactionTag { value = Faction.DefenderCore });
            em.AddComponent<GoalTowerTag>(core);
            em.AddBuffer<IncomingDamage>(core);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(defender).Length,
                "거점 전담 적은 더 가까운 방어유닛도 때리지 않는다 — 유인으로 막을 수 없다");
            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(core).Length,
                "마스크에 있는 유일한 후보인 마음을 때린다");
        }

        // ───────────────────────── unit 8 — 방어 측 (위의 거울) ─────────────────────────

        [Test]
        public void LegacyDefenderMask_IsEnemyUnitOnly()
        {
            Assert.AreEqual((int)Faction.EnemyUnit, DefenderTargetDefaults.LegacyDefenderMask,
                "unit 8 이전에 리터럴로 박혀 있던 값 — 이것이 적 거점을 무적으로 만들던 원인이다");
        }

        [Test]
        public void DefenderResolve_Unauthored_FallsBackToEnemyUnit()
        {
            Assert.AreEqual(DefenderTargetDefaults.LegacyDefenderMask,
                DefenderTargetDefaults.Resolve((int)Faction.None, targetAllies: false),
                "0 = 미저작 → 적 유닛 단독. 인스펙터에서 비웠을 때의 방어선");
        }

        [Test]
        public void DefenderResolve_Authored_IsRespectedVerbatim()
        {
            Assert.AreEqual(Factions.AnyEnemy,
                DefenderTargetDefaults.Resolve(Factions.AnyEnemy, targetAllies: false));
            Assert.AreEqual((int)Faction.EnemyCore,
                DefenderTargetDefaults.Resolve((int)Faction.EnemyCore, targetAllies: false),
                "«적 마음만 노리는 공성 유닛» 이 저작으로 표현 가능해야 한다");
        }

        // 힐러의 안전선. 아군 타게팅이 AnyDefender 로 넓어지면 IncomingHeal 버퍼가 없는
        // 거점이 후보에 들어 ECB playback 에서 던진다 — 그래서 DefenderUnit 단독이다.
        [Test]
        public void DefenderResolve_TargetAllies_WinsOverAuthoredMask()
        {
            Assert.AreEqual((int)Faction.DefenderUnit,
                DefenderTargetDefaults.Resolve(Factions.AnyEnemy, targetAllies: true),
                "targetAllies 가 저작 마스크를 이긴다 — 승격하지 않은 이유가 이것이다");
            Assert.AreEqual((int)Faction.DefenderUnit,
                DefenderTargetDefaults.Resolve((int)Faction.None, targetAllies: true));

            Assert.AreEqual(0,
                DefenderTargetDefaults.Resolve(Factions.AnyEnemy, targetAllies: true)
                    & Factions.AnyStructure,
                "힐러의 마스크에 거점 비트가 한 개도 없어야 한다");
        }

        // 실제 에셋 훑기 — 이니셜라이저/폴백 어느 경로든 무장 해제가 없는지.
        [Test]
        public void AllDefenderAssets_ResolveToNonZeroMask()
        {
            var guids = AssetDatabase.FindAssets("t:DefenderUnitData");
            Assert.Greater(guids.Length, 0, "방어 SO 를 하나도 못 찾았다 — 경로/타입 확인");

            int healers = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(path);
                if (so == null) continue;

                int resolved = DefenderTargetDefaults.Resolve((int)so.targetFactions, so.targetAllies);
                Assert.AreNotEqual(0, resolved,
                    $"{so.name}: 해석된 마스크가 0 이면 이 유닛은 아무것도 못 때린다");

                if (so.targetAllies)
                {
                    healers++;
                    Assert.AreEqual((int)Faction.DefenderUnit, resolved,
                        $"{so.name}: 아군 타게팅은 DefenderUnit 단독이어야 한다(거점 후보 진입 = 크래시)");
                }
            }
            UnityEngine.Debug.Log($"[unit 8] 방어 SO {guids.Length}종 중 아군 타게팅 {healers}종");
        }

        // 검증 질문 — 방어유닛이 적 거점을 **실제로** 때리는가. unit 8 이전엔 절대 불가였다.
        [Test]
        public void DefenderWithDefaultMask_AttacksEnemyCore_AndPrefersNearerUnit()
        {
            using var world = new World("AuthoredTargetMaskTests_DefenderVsCore");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var defender = em.CreateEntity();
            em.AddComponentData(defender, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponentData(defender, new Health { value = 50f, max = 50f });
            em.AddComponentData(defender, new FactionTag { value = Faction.DefenderUnit });
            em.AddComponent<DefenderUnitTag>(defender);
            em.AddBuffer<IncomingDamage>(defender);
            em.AddComponentData(defender, new AttackState
            {
                range = 6f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                // 기본 저작(AnyEnemy) — 에셋 이니셜라이저와 같은 값.
                targetMask = DefenderTargetDefaults.Resolve(Factions.AnyEnemy, targetAllies: false),
            });
            var outputs = em.AddBuffer<AttackOutputElement>(defender);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 7f },
            });

            // 적 마음만 사거리에 있다 — unit 8 이전엔 마스크에 없어 영구 무적이었다.
            var enemyCore = em.CreateEntity();
            em.AddComponentData(enemyCore, LocalTransform.FromPosition(new float3(4f, 0f, 0f)));
            em.AddComponentData(enemyCore, new Health { value = 500f, max = 500f });
            em.AddComponentData(enemyCore, new FactionTag { value = Faction.EnemyCore });
            em.AddComponentData(enemyCore, new StructureTag { cell = new int2(4, 0), faction = Faction.EnemyCore });
            em.AddBuffer<IncomingDamage>(enemyCore);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(enemyCore).Length,
                "방어유닛이 적 마음을 때린다 — 공성 승리 조건의 물리적 전제");

            // 이제 **더 가까운** 적 유닛을 놓는다. 계약 4: 타입 우선순위 없음, 거리순.
            var enemyUnit = em.CreateEntity();
            em.AddComponentData(enemyUnit, LocalTransform.FromPosition(new float3(1f, 0f, 0f)));
            em.AddComponentData(enemyUnit, new Health { value = 30f, max = 30f });
            em.AddComponentData(enemyUnit, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(enemyUnit);
            em.AddBuffer<IncomingDamage>(enemyUnit);

            em.GetBuffer<IncomingDamage>(enemyCore).Clear();
            var atk = em.GetComponentData<AttackState>(defender);
            atk.cooldownRemaining = 0f;
            em.SetComponentData(defender, atk);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            Assert.AreEqual(1, em.GetBuffer<IncomingDamage>(enemyUnit).Length,
                "가까운 적 유닛이 이긴다 — 거점 타입에 우선순위가 없다(계약 4)");
            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(enemyCore).Length,
                "같은 프레임에 둘 다 때리지 않는다(attackTargetCount = 1)");
        }

        // 힐러가 적 거점을 고르지 않는다 — 마스크가 DefenderUnit 단독이라는 것의 효과 검증.
        [Test]
        public void Healer_DoesNotTargetEnemyCore()
        {
            using var world = new World("AuthoredTargetMaskTests_HealerVsCore");
            var em = world.EntityManager;
            var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(world.CreateSystem<AttackSystem>());

            var healer = em.CreateEntity();
            em.AddComponentData(healer, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
            em.AddComponentData(healer, new Health { value = 50f, max = 50f });
            em.AddComponentData(healer, new FactionTag { value = Faction.DefenderUnit });
            em.AddComponent<DefenderUnitTag>(healer);
            em.AddBuffer<IncomingDamage>(healer);
            em.AddComponentData(healer, new AttackState
            {
                range = 6f,
                cooldownDuration = 1f,
                cooldownRemaining = 0f,
                attackTargetCount = 1,
                // 저작이 AnyEnemy 여도 targetAllies 가 이긴다.
                targetMask = DefenderTargetDefaults.Resolve(Factions.AnyEnemy, targetAllies: true),
            });
            var outputs = em.AddBuffer<AttackOutputElement>(healer);
            outputs.Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 5f },
            });

            var enemyCore = em.CreateEntity();
            em.AddComponentData(enemyCore, LocalTransform.FromPosition(new float3(2f, 0f, 0f)));
            em.AddComponentData(enemyCore, new Health { value = 500f, max = 500f });
            em.AddComponentData(enemyCore, new FactionTag { value = Faction.EnemyCore });
            em.AddComponentData(enemyCore, new StructureTag { cell = new int2(2, 0), faction = Faction.EnemyCore });
            em.AddBuffer<IncomingDamage>(enemyCore);

            world.SetTime(new TimeData(world.Time.ElapsedTime + 0.016f, 0.016f));
            simGroup.Update();

            Assert.AreEqual(0, em.GetBuffer<IncomingDamage>(enemyCore).Length,
                "힐러는 적 거점을 후보로 삼지 않는다");
        }

        // instinct-content unit 1 — 배치 배제는 **건물 자리뿐**이다. 편도 종류도 묻지 않는다.
        //
        // 이 자리엔 `IsHostileInstinct` 술어(적/중립 본능만 여유 9×9)를 단정하는 테스트가
        // 있었다. 술어째 삭제됐다 — 사용자 지시의 「배치 불가」는 건물이 선 칸을 뜻했지,
        // 본능만 특별히 넓게 막으라는 뜻이 아니었다. 남는 규칙은 footprint 하나다.
        [Test]
        public void PlacementExclusion_IsFootprintOnly_ForEveryStructureKind()
        {
            Assert.AreEqual(StructurePlacements.InstinctFootprint,
                StructurePlacements.FootprintOf(Faction.EnemyInstinct));
            Assert.AreEqual(StructurePlacements.InstinctFootprint,
                StructurePlacements.FootprintOf(Faction.DefenderInstinct),
                "편이 배제 규칙을 가르지 않는다 — 내 본능도 자기 자리를 차지한다");
            Assert.AreEqual(StructurePlacements.CoreFootprint,
                StructurePlacements.FootprintOf(Faction.EnemyCore),
                "마음은 본체 1칸 — 여유가 붙으면 인접 배치로 공성하는 경로가 막힌다");

            Assert.AreEqual(1, StructurePlacements.CoreFootprint / 2 * 2 + 1,
                "footprint 는 홀수여야 중심 대칭으로 닫힌다");
            Assert.AreEqual(3, StructurePlacements.InstinctFootprint / 2 * 2 + 1);
        }
    }
}
