using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // boss-mamemo unit 1 — 자장가.
    //
    // 이 테스트가 겨누는 것은 "내 분기가 호출됐나" 가 아니라 **증상**이다:
    // *마메모 근처 방어유닛이 실제로 잠들고, 마메모가 때릴 수 있는 거리의 유닛은 안 잔다.*
    // 그래서 실제 스폰 경로(BattleBridge.SpawnUnit → BakeNightmareMechanics)로 보스를 세우고
    // CcEffect 버퍼를 직접 읽는다 — arm 을 우회해 슬롯을 손으로 만들면 bake 가 빠져
    // "에셋 저작이 틀려도 초록" 인 테스트가 된다.
    public class BossLullabyTest
    {
        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Lullaby_SleepsOutsideAttackRange_NotInside()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindDefenderCatalog();
            var near = cat.ById("guardian");
            var far = cat.ById("ranger");
            Assert.IsNotNull(near); Assert.IsNotNull(far);

            bridge.SetDefenderPool(new[] { near, far });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, near), "place guardian");
            Assert.IsTrue(PlaceFirstValid(bridge, far), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var eNear = GetDefenderEntity(bridge, em, "guardian");
            var eFar = GetDefenderEntity(bridge, em, "ranger");
            Assert.AreNotEqual(Entity.Null, eNear);
            Assert.AreNotEqual(Entity.Null, eFar);

            var boss = SpawnMamemo(bridge, em);
            Assert.AreNotEqual(Entity.Null, boss, "마메모 스폰");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss),
                "bake 가 자장가 슬롯을 붙였다 — 없으면 에셋 저작이 bake 가드에 걸린 것");

            // 발동 주기를 테스트 길이로 줄인다. 슬롯의 나머지(반경·인원·수면 초)는 **에셋 값
            // 그대로** 둔다 — 저작이 틀리면 이 테스트가 빨개져야 한다.
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            int lullaby = -1;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].payload == DcPayloadKind.AreaSleep) { lullaby = i; break; }
            Assert.GreaterOrEqual(lullaby, 0, "AreaSleep 슬롯이 베이크됐다");
            var slot = slots[lullaby];
            int sleepRange = slot.tileRange;
            Assert.Greater(slot.duration, 0f, "수면 초 저작됨");
            Assert.GreaterOrEqual((int)slot.magnitude, 1, "재울 인원 저작됨");
            slot.periodSeconds = 0.01f;
            slot.elapsed = 0f;
            slots[lullaby] = slot;

            // 두 방어유닛을 마메모 기준 안/밖으로 옮긴다.
            //   near = **정확히 사거리 링**(Chebyshev == attackTiles) — 여기가 함정이다.
            //          공격 판정이 `tileDist > tileRange` 라 이 링은 **때릴 수 있는 칸**이고,
            //          도넛의 min 이 inclusive 라 +1 을 빠뜨리면 바로 이 칸이 재워진다.
            //          그리고 보스는 방어유닛을 사냥해 붙으므로 대상이 스스로 이 링에 선다 =
            //          최빈 케이스다. Chebyshev 1 로 두면 이 경계를 비껴가 구멍이 열린다.
            //   far  = Chebyshev sleepRange (도넛 바깥 경계 → 재워야 한다)
            int attackTiles = AttackTilesOf(em, boss);
            Assert.Greater(sleepRange, attackTiles + 1,
                $"저작 sanity: tileRange({sleepRange}) 가 사거리({attackTiles})+1 보다 커야 도넛에 칸이 남는다");
            float tile = ResolveTileSize(em);
            float3 bossPos = em.GetComponentData<LocalTransform>(boss).Position;
            MoveTo(em, eNear, bossPos + new float3(attackTiles * tile, 0f, 0f));
            MoveTo(em, eFar, bossPos + new float3(sleepRange * tile, 0f, 0f));

            // 마메모는 방어유닛을 사냥하러 움직인다 — 매 프레임 위치를 되돌려 판정이 이동에
            // 흔들리지 않게 한다(리뷰 L2). 주기 0.01s 라 몇 프레임 안에 끝난다.
            bool farAsleep = false;
            for (int i = 0; i < 12 && !farAsleep; i++)
            {
                MoveTo(em, boss, bossPos);
                yield return null;
                farAsleep = HasCc(em, eFar, CcKind.Sleep);
            }

            Assert.IsTrue(farAsleep, "사거리 밖 방어유닛이 잔다 (자장가 발동)");
            Assert.IsFalse(HasCc(em, eNear, CcKind.Sleep),
                "**사거리 링**의 방어유닛은 안 잔다 — 재우면 마메모 자기 평타가 곧바로 깨워 자기무효화된다");
        }

        // 계약: 마메모 자신은 보스라 CC 면역이다("재우는 보스가 자기는 안 잔다").
        //
        // ⚠ 이 테스트의 초판은 **공허하게 참**이었다(리뷰 M5): 방어유닛을 하나도 안 놓고
        // "보스가 안 잔다" 를 단언했는데, 자장가의 후보 풀이 방어유닛이라 보스는 애초에
        // 구조적으로 들어갈 수 없다 — 어떤 구현 버그로도 빨개지지 않았다.
        // 이제 **CC 채널에 Sleep 을 직접 넣어** CcApplySystem 의 IsBossImmune 게이트를 때린다.
        // 대조군(일반 적은 같은 경로로 잔다)을 같이 세워 "0" 이 "채널이 죽은 판" 과 구분되게 한다.
        [UnityTest]
        public IEnumerator BossIsImmuneToSleep_WhileOrdinaryEnemySleeps()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var boss = SpawnMamemo(bridge, em);
            var grunt = SpawnEnemy(bridge, em, "Assets/_Project/Data/Enemies/Enemy_Basic.asset");
            Assert.AreNotEqual(Entity.Null, grunt, "대조군 잡몹");

            var q = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.EnemyCcEventsSingleton>());
            Assert.AreNotEqual(0, q.CalculateEntityCount(), "CC 채널이 살아 있다");
            var queue = q.GetSingleton<Wassup.Battle.Effects.EnemyCcEventsSingleton>().queue;
            var sleep = new CcEffect { kind = CcKind.Sleep, remainingTime = 30f };
            queue.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent { target = boss, effect = sleep });
            queue.Enqueue(new Wassup.Battle.Effects.EnemyCcEvent { target = grunt, effect = sleep });

            bool gruntAsleep = false;
            for (int i = 0; i < 10 && !gruntAsleep; i++)
            {
                yield return null;
                gruntAsleep = HasCc(em, grunt, CcKind.Sleep);
            }

            Assert.IsTrue(gruntAsleep, "대조군: 일반 적은 같은 채널로 잔다 (채널이 죽은 판이 아니다)");
            Assert.IsFalse(HasCc(em, boss, CcKind.Sleep), "마메모는 보스라 수면 면역이다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static Entity SpawnMamemo(BattleBridge bridge, EntityManager em)
        {
            // 에셋을 경로로 직접 로드한다. 두 우회로가 다 막혀 있다: EnemyCatalog 은 씬이
            // 참조하지 않아 Resources.FindObjectsOfTypeAll 로 안 잡히고(로드 0),
            // BattleScene 의 기본 deck 은 WaveA(bossPool 비어 있음)라 pool 로도 못 꺼낸다.
            // 덱 투입은 EditMode 생성기 테스트가 따로 지킨다 — 여기선 arm 동작만 본다.
            var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(
                "Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset");
            Assert.IsNotNull(unit, "Enemy_Boss_Mamemo.asset");

            var bt = typeof(BattleBridge);
            var pendingType = bt.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            var pending = System.Activator.CreateInstance(pendingType);
            var entry = new SpawnEntry { triggerTimeSec = 0f, unitType = unit, spawnIndex = 0 };
            pendingType.GetField("entry").SetValue(pending, entry);
            pendingType.GetField("laneIndex").SetValue(pending, 0);

            var before = CountBosses(em);
            bt.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(bridge, new[] { pending });

            // 스폰은 즉시 엔티티를 만든다 — 새로 생긴 슬롯 캐리어 하나를 집는다.
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Combat.BossTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity found = Entity.Null;
            for (int i = 0; i < arr.Length; i++) found = arr[i];
            arr.Dispose();
            Assert.Greater(CountBosses(em), before - 1);
            return found;
        }

        // 대조군용 일반 적 — 새로 생긴 엔티티를 차집합으로 집는다(보스 태그가 없어
        // BossTag 쿼리로는 못 찾는다).
        private static Entity SpawnEnemy(BattleBridge bridge, EntityManager em, string assetPath)
        {
            var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(assetPath);
            Assert.IsNotNull(unit, assetPath);

            var bt = typeof(BattleBridge);
            var pendingType = bt.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            var pending = System.Activator.CreateInstance(pendingType);
            pendingType.GetField("entry").SetValue(pending,
                new SpawnEntry { triggerTimeSec = 0f, unitType = unit, spawnIndex = 0 });
            pendingType.GetField("laneIndex").SetValue(pending, 0);

            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var before = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var known = new System.Collections.Generic.HashSet<Entity>();
            for (int i = 0; i < before.Length; i++) known.Add(before[i]);
            before.Dispose();

            bt.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(bridge, new[] { pending });

            var after = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity spawned = Entity.Null;
            for (int i = 0; i < after.Length; i++) if (!known.Contains(after[i])) spawned = after[i];
            after.Dispose();
            return spawned;
        }

        private static int CountBosses(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Combat.BossTag>());
            return q.CalculateEntityCount();
        }

        // 공격 판정과 **같은 변환**을 테스트도 쓴다 — 여기서 상수 2 를 박으면
        // 에셋 사거리를 바꿨을 때 테스트가 조용히 다른 링을 겨눈다.
        private static int AttackTilesOf(EntityManager em, Entity e)
        {
            if (!em.HasComponent<Wassup.Battle.Combat.AttackState>(e)) return 0;
            return Wassup.Battle.Movement.GridMath.RangeToTiles(
                em.GetComponentData<Wassup.Battle.Combat.AttackState>(e).range);
        }

        private static float ResolveTileSize(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            if (q.CalculateEntityCount() == 0) return 1f;
            return q.GetSingleton<FlowFieldSingleton>().tileSize;
        }

        private static void MoveTo(EntityManager em, Entity e, float3 pos)
        {
            var t = em.GetComponentData<LocalTransform>(e);
            t.Position = pos;
            em.SetComponentData(e, t);
        }

        private static bool HasCc(EntityManager em, Entity e, CcKind kind)
        {
            if (e == Entity.Null || !em.Exists(e) || !em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++) if (buf[i].kind == kind) return true;
            return false;
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

        private static Entity GetDefenderEntity(BattleBridge bridge, EntityManager em, string id)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value; var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(val);
                if (data.id == id && em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
