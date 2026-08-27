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
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — 궁극기 도약 특성화 (짱쎈놈 r3:
    // HealthThreshold × UltimateLeap). 이전(port) 전의 동작을 박제한다.
    //
    // 이 arm 은 이탈→예고→강습 3단 시퀀스고, 관측 가능한 결과는 세 개다:
    //  1) 이탈 중 **들어온 피해가 실제로 버려진다** (판 밖 = 무적. UltimateLeapState 존재가
    //     아니라 체력 무변으로 단언한다 — DamageApplicationSystem 의 버퍼 드랍이 증인)
    //  2) 강습이 **발동 프레임에 고정된 착지점**으로 위치를 실제로 옮긴다 (예고 = 약속 계약)
    //  3) 착지 슬램이 예고 범위 안 방어유닛에 **저작된 피해를 실제로 넣는다**
    //     (일반 도약과 달리 슬램 피해는 sim 소유 — UltimateLeapSystem 이 캐리어를 스테이징)
    //
    // 결정론 장치: 발동은 체력 직접 주입으로 강제하고, 예고 카운트다운은 sim 컴포넌트
    // (UltimateLeapState.remaining)를 줄여 수 프레임으로 압축한다 — 프레임 수 대기 같은
    // emergent 타이밍에 걸지 않는다(BossLullabyLiveTest 삭제 사유 회피).
    public class BossUltimateLeapTest
    {
        private int _savedMap;

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
        public IEnumerator UltimateLeap_InvulnerableDuringAscent_ThenSlamsLandingArea()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var gm = Object.FindObjectOfType<GameManager>();

            // 착지 앵커이자 슬램 피해자 = 가디언(근접 사거리 1 — 멀리 있는 보스를 못 때려
            // 내가 주입한 것 외의 보스 체력 변동이 없다).
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

            var boss = BattleBridgeTestAccess.SpawnEnemy(bridge, em,
                BattleBridgeTestAccess.LoadEnemy("Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset"));
            Assert.AreNotEqual(Entity.Null, boss, "짱쎈놈 스폰");
            Assert.IsTrue(em.HasBuffer<DcTriggerSlot>(boss), "mechanics 베이크됨");

            var slots = em.GetBuffer<DcTriggerSlot>(boss);
            int ultIdx = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s.trigger == DcTriggerKind.HealthThreshold && s.payload == DcPayloadKind.UltimateLeap)
                {
                    ultIdx = i;
                    continue;
                }
                // 궁극기 경계(20%)까지 한 방에 떨어뜨리면 자폭·도약 경계도 함께 관통된다.
                // 그 arm 들은 각자 자기 테스트가 지키므로, 여기서는 래치를 미리 소진시켜
                // (nextBoundaryIndex 전진 — 발화 판정이 실제로 쓰는 그 상태) 궁극기만 발동시킨다.
                if (s.trigger == DcTriggerKind.HealthThreshold)
                {
                    s.nextBoundaryIndex = 99;
                    slots[i] = s;
                }
            }
            Assert.GreaterOrEqual(ultIdx, 0, "HealthThreshold×UltimateLeap 슬롯이 베이크됐다");
            var ult = slots[ultIdx];
            Assert.Greater(ult.duration, 0f, "예고(이탈) 초 저작됨");
            Assert.Greater(ult.slamDamage, 0f, "슬램 피해 저작됨");
            Assert.Greater(ult.maxHpRef, 0f, "스폰 시점 maxHp 스냅샷");
            // 「생존당 1회」는 코드에 없다 — fraction ≥ 0.5 여야 둘째 경계가 음수가 되어
            // 재발동이 수학적으로 불가하다(DcMechanic 문서 계약). 저작이 이걸 깨면 여기서 잡는다.
            Assert.GreaterOrEqual(ult.fraction, 0.5f,
                "궁극기 fraction < 0.5 — «생존당 1회» 수학 보장이 깨지는 저작이다");
            // 격리 전제: 슬램 범위가 보스 근접 사거리(1타일)보다 넓어야, 착지 후 평타가
            // 닿지 않는 경계 셀에서 슬램 피해만 분리 측정할 수 있다.
            Assert.GreaterOrEqual(ult.slamTileRange, 2,
                "슬램 범위가 2 미만이면 착지 후 평타와 분리 측정할 수 없다 — 테스트 재설계 필요");

            var ff = GetFlowField(em);
            float tile = ff.tileSize;

            // 가디언을 스폰 지점에서 먼 경로 셀로 — 착지 링 탐색이 걷기 가능 셀에서 반드시
            // 성공하고, StartBattle 뒤 웨이브 잡몹(스폰 지점 출발)이 짧은 측정 창 동안 닿지 않는다.
            float3 bossPos0 = em.GetComponentData<LocalTransform>(boss).Position;
            int2 spawnCell = GridMath.WorldToCell(bossPos0, tile, ff.gridSize, origin: ff.origin);
            int2 anchorCell = FarthestWalkableCell(ff, spawnCell);
            Assert.GreaterOrEqual(Cheb(anchorCell, spawnCell), 4, "스폰과 격리된 앵커 셀");
            float3 defPos = em.GetComponentData<LocalTransform>(eDef).Position;
            MoveTo(em, eDef, GridMath.CellToWorldCenter(anchorCell, tile, defPos.y, origin: ff.origin));

            // 슬램은 캐리어 → **브리지 드레인** → 투사체 경로다. 드레인은 `_running` 게이트
            // 뒤라 StartBattle 이 필요하다(자폭 테스트와 같은 이유). 발동(이탈)·무적·강습
            // 텔레포트는 전부 ECS 라 이 게이트와 무관하지만, 슬램 하나 때문에 켠다.
            bridge.StartBattle();
            yield return null;

            // ── 발동: 경계 아래로 직접 주입 ──────────────────────────────────────
            var h = em.GetComponentData<Health>(boss);
            float boundary = ult.maxHpRef * (1f - ult.fraction);
            float drop = h.value - boundary + 5f;
            Assert.Greater(drop, 0f, "만피에서 궁극기 경계 아래로 떨어뜨릴 수 있다");
            em.GetBuffer<IncomingDamage>(boss).Add(new IncomingDamage { amount = drop });

            bool armed = false;
            for (int i = 0; i < 8 && !armed; i++)
            {
                yield return null;
                armed = em.HasComponent<UltimateLeapState>(boss);
            }
            Assert.IsTrue(armed, "경계 관통 후 이탈 시퀀스가 시작된다 (UltimateLeapState 부착)");
            Assert.IsTrue(em.HasComponent<LeapFlight>(boss),
                "잠금(LeapFlight)과 무적은 함께 붙는다 — 레이어는 갈리지만 수명은 하나 (README 6)");
            var leap = em.GetComponentData<UltimateLeapState>(boss);
            Assert.AreEqual(ult.duration, leap.remaining, 0.25f,
                "예고 잔여가 저작된 이탈 초에서 시작해 sim 시계로 흐른다");

            // ── 1) 이탈 중 피격 불가 — 체력 무변으로 단언 ─────────────────────────
            // 무적이 깨져 있어도 보스가 죽지 않을 소량을 넣는다(죽으면 이후 단언이
            // 연쇄로 무너져 원인이 흐려진다).
            float hpAscent = em.GetComponentData<Health>(boss).value;
            em.GetBuffer<IncomingDamage>(boss).Add(new IncomingDamage { amount = 30f });
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(hpAscent, em.GetComponentData<Health>(boss).value, 0.01f,
                "이탈 중 들어온 피해는 실제로 버려진다 (적립됐다 착지에 터지는 것도 아니다)");

            // ── 슬램 피해자 배치: 발동 프레임에 고정된 착지 셀 기준 ────────────────
            // landingCell 은 발동 프레임에 고정됐다(예고 = 약속). 가디언을 그 셀에서
            // Chebyshev == slamTileRange(판정은 ≤ 포함) 셀로 옮긴다 — 슬램 범위 안이면서
            // 착지한 보스의 평타(1타일) 밖이라 첫 피해는 슬램일 수밖에 없다.
            int dir = leap.landingCell.x > ff.gridSize.x / 2 ? -1 : 1;
            int2 victimCell = new int2(leap.landingCell.x + dir * ult.slamTileRange, leap.landingCell.y);
            MoveTo(em, eDef, GridMath.CellToWorldCenter(victimCell, tile, defPos.y, origin: ff.origin));
            float defHp0 = em.GetComponentData<Health>(eDef).value;

            // ── 예고 압축: sim 이 소유한 카운트다운을 직접 줄인다 ──────────────────
            leap.remaining = 0.05f;
            em.SetComponentData(boss, leap);

            bool landedState = false;
            for (int i = 0; i < 25 && !landedState; i++)
            {
                yield return null;
                landedState = !em.HasComponent<UltimateLeapState>(boss);
            }
            Assert.IsTrue(landedState, "예고가 끝나면 이탈 상태가 해제된다");
            Assert.IsFalse(em.HasComponent<LeapFlight>(boss),
                "착지 시 잠금도 함께 떨어진다 (붙을 때와 대칭)");

            // ── 2) 강습 = 고정된 착지점으로 실제 이동 ────────────────────────────
            float3 bossPos = em.GetComponentData<LocalTransform>(boss).Position;
            Assert.Less(
                math.distance(new float2(bossPos.x, bossPos.z),
                              new float2(leap.landingWorld.x, leap.landingWorld.z)),
                0.75f * tile,
                "착지 후 보스 위치가 발동 프레임에 고정된 landingWorld 다 — 예고 타일이 거짓말이 아니다");

            // ── 3) 착지 슬램이 저작된 피해를 실제로 넣는다 ───────────────────────
            float defHp = defHp0;
            for (int i = 0; i < 15 && defHp >= defHp0; i++)
            {
                yield return null;
                defHp = em.GetComponentData<Health>(eDef).value;
            }
            Assert.AreEqual(ult.slamDamage, defHp0 - defHp, 0.5f,
                "슬램 범위 경계 셀의 방어유닛이 저작된 슬램 피해를 입는다 (범위 판정은 Chebyshev ≤)");
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
