using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — 경계 자폭 특성화 (짱쎈놈 r0:
    // HealthThreshold × SelfTileAoe). 이전(port) 전의 동작을 박제한다.
    //
    // 이 arm 의 관측 가능한 결과는 「체력이 경계를 지나면 **주변 방어유닛이 실제 피해를
    // 입는다**」이다. 슬롯 존재나 발동 플래그가 아니라 피해량 자체를 단언한다 — 캐리어
    // 스테이징(HealthThresholdSystem) → 브리지 드레인 → SkyFall×TileAoe 착탄까지 전 구간이
    // 살아 있어야 초록이 된다.
    //
    // ⚠ fraction < 0.5 인 이 슬롯은 **다발 경계**다(0.8·0.6·0.4·0.2 maxHp). 그래서
    // 「경계 하나 = 폭발 하나」와 「경계 사이에서는 재발동 없음」을 함께 박제한다 —
    // 「생존당 1회」가 코드에 없는 이 가족에서 재발동 규칙은 HealthThresholdEval 의
    // 래치 수학이 전부이고, 이 테스트가 그 수학의 증인이다.
    public class BossThresholdSelfAoeTest
    {
        private int _savedMap;

        // duel-live-focus — 전투를 계측하는 테스트는 자기 판을 선언한다.
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThresholdNova_DamagesNearbyDefender_OncePerBoundary()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var gm = Object.FindObjectOfType<GameManager>();

            // 피해자 = 가디언(근접 사거리 1). 자폭 반경 밖에서는 보스를 못 때리므로
            // 측정 중 보스 체력이 내가 주입한 것 외로 움직이지 않는다(경계 조기 관통 방지).
            var guardian = FindDefenderCatalog().ById("guardian");
            Assert.IsNotNull(guardian, "guardian 카탈로그");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "가디언 배치");
            var eDef = GetDefenderEntity(bridge, em, "guardian");
            Assert.AreNotEqual(Entity.Null, eDef, "가디언 엔티티");

            // 실제 스폰 경로(SpawnUnit → bake)로 보스를 세운다 — 슬롯을 손으로 만들면
            // 에셋 저작이 틀려도 초록인 테스트가 된다(BossLullabyTest 와 같은 이유).
            var boss = BattleBridgeTestAccess.SpawnEnemy(bridge, em,
                BattleBridgeTestAccess.LoadEnemy("Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset"));
            Assert.AreNotEqual(Entity.Null, boss, "짱쎈놈 스폰");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss), "mechanics 베이크됨");

            // 자폭 슬롯. 값(반경·피해·경계 간격)은 전부 에셋 저작 그대로 읽는다 —
            // 밸런스 리터럴을 못 박지 않는다(test-procedure 규율).
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            int novaIdx = -1;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].trigger == DcTriggerKind.HealthThreshold
                    && slots[i].payload == DcPayloadKind.SelfTileAoe) { novaIdx = i; break; }
            Assert.GreaterOrEqual(novaIdx, 0, "HealthThreshold×SelfTileAoe 슬롯이 베이크됐다");
            var nova = slots[novaIdx];
            Assert.Greater(nova.magnitude, 0f, "자폭 피해 저작됨");
            Assert.Greater(nova.fraction, 0f, "경계 간격 저작됨");
            Assert.Greater(nova.maxHpRef, 0f, "스폰 시점 maxHp 스냅샷");
            // 격리 전제: 반경이 보스 근접 사거리(1타일)보다 넓어야 피해자를 자폭 반경 안 ·
            // 평타 밖에 세울 수 있다. 저작이 1로 줄면 이 분리가 깨지므로 loud 하게 알린다.
            Assert.GreaterOrEqual(nova.tileRange, 2,
                "자폭 반경이 2 미만이면 평타(1타일)와 분리 측정할 수 없다 — 테스트 재설계 필요");

            var ff = GetFlowField(em);
            float tile = ff.tileSize;

            // 보스를 스폰 지점에서 먼 경로 셀에 고정한다. StartBattle 이후 웨이브가 스폰
            // 지점에서 태어나므로, 멀리 떨어져야 짧은 측정 창 동안 잡몹이 가디언에 닿지 않는다
            // (적 속도 ~1.3타일/s × 창 ~1s → 도달 불가 거리면 충분).
            float3 bossPos0 = em.GetComponentData<LocalTransform>(boss).Position;
            int2 spawnCell = GridMath.WorldToCell(bossPos0, tile, ff.gridSize, origin: ff.origin);
            int2 pinCell = FarthestWalkableCell(ff, spawnCell);
            Assert.GreaterOrEqual(Cheb(pinCell, spawnCell), 4, "스폰과 격리된 경로 셀");
            float3 pinPos = GridMath.CellToWorldCenter(pinCell, tile, bossPos0.y, origin: ff.origin);
            MoveTo(em, boss, pinPos);

            // 가디언을 자폭 반경 **경계 셀**(Chebyshev == tileRange, 판정은 ≤ 포함)에 세운다.
            // 그리드 밖으로 나가지 않게 안쪽 방향으로 오프셋.
            int dir = pinCell.x > ff.gridSize.x / 2 ? -1 : 1;
            int2 defCell = new int2(pinCell.x + dir * nova.tileRange, pinCell.y);
            float3 defPos = em.GetComponentData<LocalTransform>(eDef).Position;
            MoveTo(em, eDef, GridMath.CellToWorldCenter(defCell, tile, defPos.y, origin: ff.origin));

            // 자폭 피해는 캐리어 → **브리지 드레인** → 투사체 경로다. 드레인은
            // TickBattleFrame 의 `_running` 게이트 뒤에 있어 StartBattle 없이는
            // 캐리어가 영원히 잠든다(OnPlaceSkyStrikeTest 가 같은 이유로 StartBattle 한다).
            bridge.StartBattle();
            yield return null;

            // ── 경계 1 관통 → 폭발 1회 ────────────────────────────────────────
            float defHp0 = em.GetComponentData<Health>(eDef).value;
            var h = em.GetComponentData<Health>(boss);
            float boundary1 = nova.maxHpRef * (1f - nova.fraction);
            float boundary2 = nova.maxHpRef * (1f - 2f * nova.fraction);
            float drop1 = h.value - boundary1 + 5f;
            Assert.Greater(drop1, 0f, "만피에서 첫 경계 아래로 떨어뜨릴 수 있다");
            Assert.Greater(boundary1 - 5f, boundary2 + 1f, "첫 낙하가 둘째 경계를 침범하지 않는다");
            em.GetBuffer<IncomingDamage>(boss).Add(new IncomingDamage { amount = drop1 });

            float defHp = defHp0;
            for (int i = 0; i < 20 && defHp >= defHp0; i++)
            {
                MoveTo(em, boss, pinPos); // 보스는 행군한다 — 폭심을 셀에 고정
                yield return null;
                defHp = em.GetComponentData<Health>(eDef).value;
            }
            // 「발동했다」가 아니라 「저작된 피해가 실제로 들어갔다」. 브리지 드레인이나
            // TileAoe 판정이 죽으면 delta 0, 진영 도출이 깨지면(자기 진영 오폭) 역시 0 이다.
            Assert.AreEqual(nova.magnitude, defHp0 - defHp, 0.5f,
                "경계 관통 시 반경 안 방어유닛이 저작된 자폭 피해를 입는다");

            // ── 경계 사이 = 재발동 없음 (HealthThresholdEval 래치의 증인) ─────────
            float defHp1 = defHp;
            for (int i = 0; i < 12; i++) { MoveTo(em, boss, pinPos); yield return null; }
            Assert.Greater(em.GetComponentData<Health>(boss).value, boundary2,
                "전제: 보스가 아직 둘째 경계 위에 있다(있어야 아래 무발동 단언이 유효)");
            Assert.AreEqual(defHp1, em.GetComponentData<Health>(eDef).value, 0.01f,
                "새 경계를 지나지 않으면 자폭은 다시 터지지 않는다");

            // ── 경계 2 관통 → 다시 1회 (다발 경계 특성) ──────────────────────────
            float drop2 = em.GetComponentData<Health>(boss).value - boundary2 + 5f;
            em.GetBuffer<IncomingDamage>(boss).Add(new IncomingDamage { amount = drop2 });
            defHp = defHp1;
            for (int i = 0; i < 20 && defHp >= defHp1; i++)
            {
                MoveTo(em, boss, pinPos);
                yield return null;
                defHp = em.GetComponentData<Health>(eDef).value;
            }
            Assert.AreEqual(nova.magnitude, defHp1 - defHp, 0.5f,
                "다음 경계를 지나면 같은 피해로 다시 터진다 — fraction<0.5 슬롯은 다발 경계다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static FlowFieldSingleton GetFlowField(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();
            Assert.IsTrue(ff.walkMask.IsCreated, "walkMask");
            return ff;
        }

        private static int Cheb(int2 a, int2 b) => math.max(math.abs(a.x - b.x), math.abs(a.y - b.y));

        // from 에서 Chebyshev 거리가 최대인 걷기 가능 셀(경로 위 = 연결 보장). 스캔 순서
        // 고정이라 결정론.
        private static int2 FarthestWalkableCell(in FlowFieldSingleton ff, int2 from)
        {
            int2 best = from; int bestD = -1;
            for (int y = 0; y < ff.gridSize.y; y++)
                for (int x = 0; x < ff.gridSize.x; x++)
                {
                    if (ff.walkMask[y * ff.gridSize.x + x] == 0) continue;
                    int d = Cheb(new int2(x, y), from);
                    if (d > bestD) { bestD = d; best = new int2(x, y); }
                }
            Assert.GreaterOrEqual(bestD, 0, "걷기 가능 셀이 있다");
            return best;
        }

        private static void MoveTo(EntityManager em, Entity e, float3 pos)
        {
            var t = em.GetComponentData<LocalTransform>(e);
            t.Position = pos;
            em.SetComponentData(e, t);
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            Assert.Greater(all.Length, 0, "DefenderCatalog 로드됨");
            return all[0];
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
            var dict = (System.Collections.IDictionary)BattleBridgeTestAccess.Field(bridge, "_defenderByTile");
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
