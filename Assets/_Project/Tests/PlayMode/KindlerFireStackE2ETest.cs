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
    // enemy-fire-stack-shooter unit 3 — 킨들러 e2e.
    //
    // 사슬 전체를 태운다: 배송 에셋(Enemy_Kindler) 스폰 → 레인저 조준 → 파이어볼 히트 →
    // 화염 스택 누적 → 5스택 임계 → (Stack, Fire) 도트.
    //
    // 배치를 이렇게 잡는 이유:
    //   - 킨들러를 **아처(Ranger 클래스) 위**에 두고 가디언은 체비셰프 4칸 떨어뜨린다.
    //     4 ≤ 킨들러 사거리(4) 라 가디언도 **후보에는 들어오지만**, 가디언 자신의 사거리는
    //     1 이라 킨들러를 때리지 못한다 → 어그로가 걸리지 않는다. 어그로 sticky override 는
    //     클래스 필터를 덮는 것이 사양이므로(계약), 그 경로를 배제해야 base 필터를 본다.
    //   - 그래서 "가디언이 사거리 안인데도 화염 스택을 한 번도 못 받는다" 가
    //     targetClassMask=Ranger 하드 필터의 직접 증거가 된다.
    //
    // 실웨이브 공존 허용 — 다른 적은 Fire 스택 producer 가 아니므로 단언이 오염되지 않는다.
    public class KindlerFireStackE2ETest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator Kindler_TargetsRangerOnly_AndFireStackReachesThresholdDot()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            // Fire 임계 규칙 배선(unit 1) 이 없으면 스택만 쌓이고 아무 일도 안 일어난다 — 회귀 가드.
            var fireRules = BattleBridge.GetStackThresholds(StackKind.Fire);
            Assert.Greater(fireRules.Length, 0,
                "StackModifier_Fire 가 BattleBridge.stackModifierAuthoring 에 배선돼 있어야 함");

            var defCatalog = FindDefenderCatalog();
            var archer = defCatalog.ById("archer");
            var guardian = defCatalog.ById("guardian");
            Assert.AreEqual(DefenderClass.Ranger, archer.role, "아처 = Ranger 클래스");
            Assert.AreEqual(DefenderClass.Guardian, guardian.role, "가디언 = Guardian 클래스");

            // ⚠ EnemyCatalog 로 찾지 말 것 — 그 에셋은 OutgameScene 의 UnitStatRuntimeRefresher
            // 에서만 참조돼 BattleScene 에는 **로드되지 않는다**(초판이 여기서 NRE 로 죽었다).
            // BattleScene 은 MapDocumentPool 을 참조하고 그 풀이 라이브 덱 6종을 물고 있어서,
            // 덱의 attackUnitPool 에 등록된 유닛은 씬 로드와 함께 메모리에 올라온다 —
            // 즉 이 조회가 성공한다는 것 자체가 unit 3 의 덱 등록에 대한 간접 증거다.
            var kindler = FindLoadedEnemy("kindler");
            Assert.IsNotNull(kindler,
                "kindler 가 로드돼 있어야 함 — 라이브 덱 attackUnitPool 등록 확인");

            bridge.SetDefenderPool(new[] { archer, guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            // 체비셰프 거리 정확히 4 인 배치 가능 셀 쌍을 찾는다(위 주석의 배치 근거).
            Assert.IsTrue(FindCellPair(bridge, archer, guardian, 4, out var aCell, out var gCell),
                "거리 4 배치 셀 쌍");
            Assert.IsTrue(bridge.PlaceDefenderAs(aCell.x, aCell.y, archer), "place archer");
            Assert.IsTrue(bridge.PlaceDefenderAs(gCell.x, gCell.y, guardian), "place guardian");

            var archerE = FindDefenderAt(bridge, em, aCell);
            var guardianE = FindDefenderAt(bridge, em, gCell);
            Assert.AreNotEqual(Entity.Null, archerE, "archer entity");
            Assert.AreNotEqual(Entity.Null, guardianE, "guardian entity");

            // 투사체 드레인이 `if (!_running) return;` 뒤에 있다 — StartBattle 필수.
            bridge.StartBattle();
            yield return null;

            var kindlerE = SpawnEnemy(bridge, kindler);
            Assert.AreNotEqual(Entity.Null, kindlerE, "kindler entity");

            // 아처 위로 이동 + 관측 창 동안 살아남게 체력 부여(아처가 45짜리 킨들러를 먼저 죽인다).
            var aPos = em.GetComponentData<LocalTransform>(archerE).Position;
            var kt = em.GetComponentData<LocalTransform>(kindlerE);
            kt.Position = aPos;
            em.SetComponentData(kindlerE, kt);
            SetHuge(em, kindlerE);
            SetHuge(em, archerE);   // 실웨이브 적에게 맞아 죽지 않게
            SetHuge(em, guardianE);

            int maxFireSlots = 0, maxFireCount = 0;
            bool sawStackDot = false, guardianGotFire = false;
            float t = 0f;
            while (t < 15f && !sawStackDot)
            {
                t += Time.deltaTime;

                if (em.Exists(archerE) && em.HasBuffer<StackModifierSlot>(archerE))
                {
                    var st = em.GetBuffer<StackModifierSlot>(archerE);
                    int slots = 0;
                    for (int i = 0; i < st.Length; i++)
                    {
                        if (st[i].kind != StackKind.Fire) continue;
                        slots++;
                        if (st[i].stackCount > maxFireCount) maxFireCount = st[i].stackCount;
                    }
                    if (slots > maxFireSlots) maxFireSlots = slots;
                }
                if (em.Exists(archerE) && em.HasBuffer<DotEffect>(archerE))
                {
                    var dots = em.GetBuffer<DotEffect>(archerE);
                    for (int i = 0; i < dots.Length; i++)
                        if (dots[i].origin == DotOrigin.Stack
                            && dots[i].element == DotElement.Fire
                            && dots[i].remainingTime > 0f) { sawStackDot = true; break; }
                }
                if (!guardianGotFire && em.Exists(guardianE) && em.HasBuffer<StackModifierSlot>(guardianE))
                {
                    var gs = em.GetBuffer<StackModifierSlot>(guardianE);
                    for (int i = 0; i < gs.Length; i++)
                        if (gs[i].kind == StackKind.Fire) { guardianGotFire = true; break; }
                }
                yield return null;
            }

            Assert.Greater(maxFireSlots, 0, "킨들러 파이어볼이 아처에게 Fire 스택을 부여해야 함");
            Assert.AreEqual(1, maxFireSlots, "스택은 사수 단위 단일 슬롯에 누적돼야 함(unit 0)");
            Assert.GreaterOrEqual(maxFireCount, 2, "stackCount 가 누적돼야 함");
            Assert.IsFalse(guardianGotFire,
                "targetClassMask=Ranger — 사거리 안이어도 가디언은 조준 대상이 아니다");
            Assert.IsTrue(sawStackDot,
                "5스택 임계가 발화해 (Stack, Fire) 도트까지 이어져야 함");
        }

        // ── helpers ──
        private static void SetHuge(EntityManager em, Entity e)
        {
            if (!em.Exists(e) || !em.HasComponent<Health>(e)) return;
            const float Hp = 1_000_000f;
            em.SetComponentData(e, new Health { value = Hp, max = Hp });
        }

        private static Entity SpawnEnemy(BattleBridge bridge, AttackUnitData unit)
        {
            // SpawnUnit 은 private (PendingSpawnEntry 도 private nested) — 리플렉션으로 실제
            // 스폰 경로를 그대로 탄다. 직접 엔티티를 조립하면 outputs/EnemyTargetFilter bake 를
            // 테스트가 복제하게 되어 정작 검증하려는 배선을 우회한다.
            var bridgeType = typeof(BattleBridge);
            var pendingType = bridgeType.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            var pending = System.Activator.CreateInstance(pendingType);
            pendingType.GetField("entry").SetValue(pending,
                new SpawnEntry { unitType = unit, spawnIndex = 0, triggerTimeSec = 0f });
            pendingType.GetField("deckIndex").SetValue(pending, 0);

            var before = SnapshotAttackers();
            bridgeType.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
                      .Invoke(bridge, new[] { pending });
            foreach (var e in SnapshotAttackers())
                if (!before.Contains(e)) return e;
            return Entity.Null;
        }

        private static System.Collections.Generic.HashSet<Entity> SnapshotAttackers()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var set = new System.Collections.Generic.HashSet<Entity>();
            for (int i = 0; i < arr.Length; i++) set.Add(arr[i]);
            arr.Dispose();
            return set;
        }

        private static bool FindCellPair(BattleBridge bridge, DefenderUnitData a, DefenderUnitData b,
            int chebyshev, out Vector2Int aCell, out Vector2Int bCell)
        {
            aCell = default; bCell = default;
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, a, out _)) continue;
                    for (int dx = -chebyshev; dx <= chebyshev; dx++)
                        for (int dy = -chebyshev; dy <= chebyshev; dy++)
                        {
                            if (math.max(math.abs(dx), math.abs(dy)) != chebyshev) continue;
                            if (!bridge.CanPlaceDefenderAt(x + dx, y + dy, b, out _)) continue;
                            aCell = new Vector2Int(x, y);
                            bCell = new Vector2Int(x + dx, y + dy);
                            return true;
                        }
                }
            return false;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static AttackUnitData FindLoadedEnemy(string id)
        {
            foreach (var u in Resources.FindObjectsOfTypeAll<AttackUnitData>())
                if (u != null && u.id == id) return u;
            return null;
        }

        private static Entity FindDefenderAt(BattleBridge bridge, EntityManager em, Vector2Int cell)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var val = dict[cell];
            var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
            return em.Exists(entity) ? entity : Entity.Null;
        }
    }
}
