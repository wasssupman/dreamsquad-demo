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
    // skill-layer-foundation unit 1 — 도약 특성화 (짱쎈놈 r1·r2:
    // HealthThreshold × SelfBlink ×2). 이전(port) 전의 동작을 박제한다.
    //
    // 이 arm 의 관측 가능한 결과는 「경계를 지나면 **위치가 실제로 순간이동**하고, 목적지가
    // **방어유닛 밀집 셀의 착지 링 안**」이라는 것이다(boss-jjangssen unit 4 정책). 그래서
    // BlinkRequest 큐나 컴포넌트가 아니라 LocalTransform 자체를 본다 — Combat 의 목적지
    // 계산(DefenderDensity→BlinkMath)과 Movement 의 적용(BlinkApplySystem)이 모두 살아
    // 있어야 초록이다. 브리지 드레인이 필요 없는 순수 ECS seam 이라 StartBattle 은 안 한다
    // (웨이브 소음 0).
    //
    // ⚠ 착지 슬램(slamDamage)은 **브리지 비행 코루틴(뷰 시계)** 소유라 여기서 단언하지
    // 않는다 — 뷰 도착 타이밍에 걸면 emergent 타이밍 단언이 된다(BossLullabyLiveTest 의
    // 사인). 심이 소유한 결과(텔레포트·목적지)만 박제한다.
    public class BossSelfBlinkTest
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
        public IEnumerator SelfBlink_TeleportsToDefenderCluster_WhenBoundaryCrossed()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var gm = Object.FindObjectOfType<GameManager>();

            // 착지 앵커 = 방어유닛 밀집 셀. 실제 배치 경로로 1기를 세운다(1기면 밀집 셀 = 그 셀).
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
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss), "mechanics 베이크됨");

            // r1·r2 — 도약이 **두 슬롯**(서로 다른 경계) 저작된 것을 박제하고, 먼저 오는
            // 경계(fraction 최소) 쪽을 발동시킨다. 두 번째 슬롯(10% 부근)은 궁극기 경계(20%)를
            // 먼저 지나야만 닿는 깊이라 단독 발동이 불가능하다 — 동작 특성화는 첫 슬롯으로 한다.
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            int blinkCount = 0, blinkIdx = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].trigger != DcTriggerKind.HealthThreshold
                    || slots[i].payload != DcPayloadKind.SelfBlink) continue;
                blinkCount++;
                if (blinkIdx < 0 || slots[i].fraction < slots[blinkIdx].fraction) blinkIdx = i;
            }
            Assert.AreEqual(2, blinkCount, "도약(HealthThreshold×SelfBlink)은 두 경계로 저작돼 있다 (r1·r2)");
            var blink = slots[blinkIdx];
            Assert.Greater(blink.fraction, 0f, "경계 간격 저작됨");
            Assert.Greater(blink.maxHpRef, 0f, "스폰 시점 maxHp 스냅샷");
            Assert.Greater(blink.tileRange, 0, "착지 링 상한 저작됨");

            var ff = GetFlowField(em);
            float tile = ff.tileSize;

            // 가디언을 보스에서 가장 먼 경로 셀로 옮긴다. 걷기 가능 셀이어야 착지 링 탐색
            // (BlinkMath — 거리장 연결 셀만 허용)이 반드시 성공하고, 멀어야 순간이동이
            // 걸음과 구분 가능한 크기로 관측된다.
            float3 bossPos0 = em.GetComponentData<LocalTransform>(boss).Position;
            int2 bossCell0 = GridMath.WorldToCell(bossPos0, tile, ff.gridSize, origin: ff.origin);
            int2 anchorCell = FarthestWalkableCell(ff, bossCell0);
            // 전제: 출발-앵커 거리가 (링 상한 + 6) 이상 — 착지가 링 어느 셀이어도 점프가
            // 2타일 임계를 압도한다. 이게 깨지면 맵이 너무 작아 측정 자체가 무효다(loud).
            Assert.Greater(Cheb(bossCell0, anchorCell), blink.tileRange + 6,
                "전제: 보스 출발점과 밀집 앵커가 충분히 멀어야 순간이동을 걸음과 구분한다");
            float3 defPos = em.GetComponentData<LocalTransform>(eDef).Position;
            MoveTo(em, eDef, GridMath.CellToWorldCenter(anchorCell, tile, defPos.y, origin: ff.origin));
            yield return null;

            // 첫 경계 아래로 — 한 방에 관통시켜도 발동은 1회다(HealthThresholdEval 계약).
            // 이 낙하로 r0(자폭)도 함께 발동하지만 폭심은 보스 자리(앵커에서 멀다)라
            // 이 테스트의 관측(위치)에는 관여하지 않는다. 자폭은 자기 테스트가 지킨다.
            var h = em.GetComponentData<Health>(boss);
            float boundary = blink.maxHpRef * (1f - blink.fraction);
            float drop = h.value - boundary + 5f;
            Assert.Greater(drop, 0f, "만피에서 첫 도약 경계 아래로 떨어뜨릴 수 있다");
            em.GetBuffer<IncomingDamage>(boss).Add(new IncomingDamage { amount = drop });

            // 프레임 간 이동량으로 순간이동을 잡는다. 행군은 프레임당 speed·dt(≪1타일)라
            // 2타일 임계는 텔레포트만 넘을 수 있다.
            bool jumped = false;
            float3 prev = em.GetComponentData<LocalTransform>(boss).Position;
            float3 landed = default;
            for (int i = 0; i < 10 && !jumped; i++)
            {
                yield return null;
                Assert.IsTrue(em.Exists(boss), "도약은 생존 스킬이다 — 보스가 사라지면 측정 무효");
                float3 cur = em.GetComponentData<LocalTransform>(boss).Position;
                if (math.distance(new float2(cur.x, cur.z), new float2(prev.x, prev.z)) > 2f * tile)
                {
                    jumped = true;
                    landed = cur;
                }
                prev = cur;
            }

            Assert.IsTrue(jumped, "경계 관통 후 보스 위치가 실제로 순간이동한다 (걸음이 아니라 점프)");
            // 목적지 정책의 박제: 착지는 밀집 셀(=가디언 셀)에서 링 상한 안이다.
            // 어디로든 점프가 아니라 «밀집을 응징하러» 점프라는 것이 이 arm 의 정체성이다.
            int2 landedCell = GridMath.WorldToCell(landed, tile, ff.gridSize, origin: ff.origin);
            Assert.LessOrEqual(Cheb(landedCell, anchorCell), blink.tileRange,
                "착지 셀이 방어유닛 밀집 셀의 링 상한 안이다 (unit 4 의 밀집 응징 정책)");
            Assert.Greater(em.GetComponentData<Health>(boss).value, 0f, "도약 후 보스 생존");
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
