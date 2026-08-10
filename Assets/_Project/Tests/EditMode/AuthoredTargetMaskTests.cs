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
        [Test]
        public void LegacyEnemyMask_IsUnitPlusHazardPlusDefenderCore()
        {
            Assert.AreEqual(
                (int)(Faction.DefenderUnit | Faction.BlockingHazard | Faction.DefenderCore),
                EnemyTargetDefaults.LegacyEnemyMask,
                "DefenderCore 가 빠지면 적이 골 타워를 못 때려 공성이 사라진다");
        }

        [Test]
        public void Resolve_Unauthored_FallsBackToLegacyMask()
        {
            Assert.AreEqual(EnemyTargetDefaults.LegacyEnemyMask,
                EnemyTargetDefaults.Resolve((int)Faction.None),
                "0 = 미저작 → 레거시 마스크. 이게 없으면 기존 에셋이 전부 무장 해제된다");
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
                    Assert.AreEqual(EnemyTargetDefaults.LegacyEnemyMask, resolved,
                        $"{so.name}: 미저작은 레거시 마스크와 동치여야 한다(행동 변화 0)");
                }
            }
            // 진단용 — 미저작 목록이 곧 «아직 저작 안 한 적» 이다. 실패 조건은 아니다.
            if (unauthored.Count > 0)
                UnityEngine.Debug.Log($"[unit 1] 미저작 적 {unauthored.Count}종 → 레거시 폴백: {string.Join(", ", unauthored)}");
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
    }
}
