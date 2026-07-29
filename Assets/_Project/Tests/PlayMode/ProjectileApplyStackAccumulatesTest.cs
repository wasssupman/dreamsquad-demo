using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // enemy-fire-stack-shooter unit 0 — 투사체가 부여한 스택이 **누적되는가**.
    //
    // StackModifierSlot 의 병합 키는 (header.source, kind) 다(ModifierApplySystem). 근접 경로
    // (AttackSystem)는 source = attackerEntity 라 같은 슬롯에 쌓이지만, 투사체 경로
    // (ProjectileHitSystem)는 source 로 **투사체 엔티티**를 실었다. 투사체는 발사마다 새
    // 엔티티이므로 매 히트가 새 슬롯을 만들어 stackCount 가 영원히 1이고 임계에 절대 도달하지
    // 못했다. ApplyStack outputs 를 쓰는 유일한 배포 에셋이 난도질꾼(근접)이라 이 경로에
    // 사용자가 0이었고 결함이 잠복해 있었다.
    //
    // DefenderApplyStackOutputTest 와 헷갈리지 말 것: 그쪽은 **근접** outputs 경로가 큐로
    // 나가는지를 보고, 여기는 **투사체** 경로의 귀속(= 누적 가능 여부)을 본다.
    //
    // 핵심 단언은 "Bleed 슬롯이 1개를 넘지 않는다" 다 — 수정 전이라면 히트 수만큼 슬롯이
    // 늘어나므로 이것이 결함의 직접 지문이다. stackCount 는 보조 단언으로만 쓴다(Consume 이
    // 임계에서 소모하므로 특정 값으로 단언하면 폴링 타이밍에 따라 실패한다 —
    // bleed-fighter-defender 계약 1 경고).
    public class ProjectileApplyStackAccumulatesTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator ProjectileApplyStack_AccumulatesInSingleSlot()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            // 카탈로그 아처를 복제해 outputs 만 갈아끼운다 — 투사체 발사 경로를 그대로 쓰면서
            // 유닛 에셋에 선행 의존하지 않는다. Bleed 를 쓰는 이유는 임계 규칙이 이미 배선돼
            // 있어서다(Fire 는 unit 1 이 만든다) — 이 테스트가 보는 것은 원소가 아니라 귀속이다.
            var catalog = FindDefenderCatalog();
            Assert.IsNotNull(catalog, "DefenderCatalog 필요");
            var shooter = Object.Instantiate(catalog.ById("archer"));
            shooter.id = "test_stack_shooter";
            shooter.attackTargetCount = 1;
            // 관측 창 안에 히트를 충분히 쌓기 위한 복제본 전용 공속(라이브 아처 무영향).
            shooter.attackCooldown = 0.4f;
            shooter.outputs = new[]
            {
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 1f },
                new AttackOutput
                {
                    kind          = AttackOutputKind.ApplyStack,
                    magnitude     = 1f,      // countDelta — 누적을 보려면 반드시 1
                    duration      = 6f,      // perAppDuration. 공속(0.4)보다 훨씬 크게 잡아
                                             // 폴링 중 슬롯 만료로 인한 flake 를 배제한다
                    stackKind     = StackKind.Bleed,
                    stackMaxStack = 5,
                },
            };

            bridge.SetDefenderPool(new[] { shooter });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            Assert.IsTrue(PlaceFirstValid(bridge, shooter), "place shooter");

            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            // ⚠ StartBattle 필수. 투사체는 ECS 가 ProjectileSpawnRequest 를 stage 하고
            // BattleBridge 가 드레인해 스폰하는 2단계인데, 그 드레인이
            // `if (!_running) return;` 뒤에 있다(BattleBridge.cs:2261). 배틀을 시작하지 않으면
            // 요청만 쌓이고 투사체가 영영 안 나가 스택이 0으로 남는다 — 근접 경로
            // (DefenderApplyStackOutputTest)는 이 게이트를 안 타서 통과한다.
            // BeamPresentationTest 가 같은 함정을 문서화하고 있다. 실웨이브 공존은 허용
            // (더미가 사거리 0.05 라 항상 최근접이므로 타겟팅을 뺏기지 않는다).
            bridge.StartBattle();
            yield return null;

            var enemy = SpawnDummyEnemy(em, defender);

            int maxSlots = 0;
            int maxCount = 0;
            float t = 0f;
            while (t < 6f)
            {
                t += Time.deltaTime;
                if (em.Exists(enemy) && em.HasBuffer<StackModifierSlot>(enemy))
                {
                    var st = em.GetBuffer<StackModifierSlot>(enemy);
                    int slots = 0;
                    for (int i = 0; i < st.Length; i++)
                    {
                        if (st[i].kind != StackKind.Bleed) continue;
                        slots++;
                        if (st[i].stackCount > maxCount) maxCount = st[i].stackCount;
                    }
                    if (slots > maxSlots) maxSlots = slots;
                }
                yield return null;
            }
            if (em.Exists(enemy)) em.DestroyEntity(enemy);
            Object.Destroy(shooter);

            Assert.Greater(maxSlots, 0,
                "투사체 outputs 의 ApplyStack 이 대상에 Bleed StackModifierSlot 을 부여해야 함");
            Assert.AreEqual(1, maxSlots,
                "투사체 스택은 사수(owner) 단위로 병합돼야 한다 — 슬롯이 2개 이상이면 "
                + "source 로 투사체 엔티티가 실린 것(발사마다 새 슬롯 = 누적 불가)");
            Assert.GreaterOrEqual(maxCount, 2,
                "같은 슬롯에 stackCount 가 누적돼야 한다(1 에서 멈추면 임계에 영원히 못 닿음)");
        }

        // ── helpers (DefenderApplyStackOutputTest 와 같은 형태) ──
        private static Entity SpawnDummyEnemy(EntityManager em, Entity defender)
        {
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            const float Hp = 1_000_000f; // 죽지 않게 — 사격이 계속 이어지도록
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = Hp, max = Hp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddBuffer<CcEffect>(enemy);
            em.AddBuffer<DotEffect>(enemy);
            return enemy;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
