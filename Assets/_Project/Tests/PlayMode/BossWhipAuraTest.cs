using System.Collections;
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
    // skill-layer-foundation unit 1 — 채찍질 특성화 (나이트메어 r1:
    // PeriodicTimer × AllyMoveSpeedAura). 이전(port) 전의 동작을 박제한다.
    //
    // 이 arm 의 관측 가능한 결과는 「반경 안 **같은 진영** 유닛의 **이동 속도가 실제로
    // 오른다**」이다. StatModifier 버퍼에 엔트리가 있는지가 아니라, 같은 적이 버프
    // 전/후에 걷는 **경로 길이 비율**을 잰다 — Combat 의 펄스(BossPeriodicTriggerSystem),
    // Effects 의 모디파이어 적용, Movement 의 속도 소비까지 전 구간이 살아야 초록이다.
    //
    // 측정 설계:
    // - 같은 개체의 전/후 비교(within-subject)라 경로 굴곡·프레임 dt 편차가 비율에서 상쇄된다.
    //   경로 길이(프레임별 |Δpos| 합)를 재므로 코너 회전도 속도를 왜곡하지 않는다.
    // - 펄스는 위치 의존이라 호스트를 매 프레임 동행 고정한다(BossLullabyTest 의 위치 고정 선례).
    // - 주기만 테스트 길이로 줄이고 배율·TTL·반경은 에셋 값 그대로 둔다 — 저작이 틀리면
    //   빨개져야 한다.
    // - StartBattle 안 함: 이 seam 은 브리지 드레인이 필요 없고(전 구간 ECS), 웨이브 소음이
    //   측정 창을 오염시키지 않는다. (전투 시작 전에도 적이 행군하는 것은 BossLullabyTest 의
    //   마메모가 이미 증명한 전제 — 아래 v0 > 0 단언이 그 전제의 감시자다.)
    public class BossWhipAuraTest
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
        public IEnumerator WhipAura_SpeedsUpNearbySameFactionEnemy()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // 수혜자 = 실제 스폰 경로의 일반 적. 더미를 손으로 조립하면 모디파이어 수신에
            // 필요한 부품이 빠져도 조용히 무발동이라, 실스폰이 곧 계측기 검증이다.
            var ally = BattleBridgeTestAccess.SpawnEnemy(bridge, em,
                BattleBridgeTestAccess.LoadEnemy("Assets/_Project/Data/Enemies/Enemy_Basic.asset"));
            Assert.AreNotEqual(Entity.Null, ally, "수혜자 스폰");
            yield return Frames(8); // 경로 진입 안정화

            var ff = GetFlowField(em);
            float tile = ff.tileSize;

            // ── 기준 속도 (버프 없음) ─────────────────────────────────────────
            float baseDist = 0f, baseDt = 0f;
            yield return Measure(em, ally, tile, 30, v => { baseDist += v.x; baseDt += v.y; });
            float v0 = baseDist / math.max(1e-4f, baseDt);
            Assert.Greater(v0, 0.05f,
                "전제: 적이 행군 중이어야 «빨라졌다» 가 의미를 갖는다 — 0 이면 전투 전 이동 전제가 깨진 것");

            // ── 나이트메어 투입 + 채찍질만 즉발화 ────────────────────────────────
            var boss = BattleBridgeTestAccess.SpawnEnemy(bridge, em,
                BattleBridgeTestAccess.LoadEnemy("Assets/_Project/Data/Enemies/Enemy_Boss_Nightmare.asset"));
            Assert.AreNotEqual(Entity.Null, boss, "나이트메어 스폰");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss), "mechanics 베이크됨");

            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            int whipIdx = -1;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].trigger == DcTriggerKind.PeriodicTimer
                    && slots[i].payload == DcPayloadKind.AllyMoveSpeedAura) { whipIdx = i; break; }
            Assert.GreaterOrEqual(whipIdx, 0, "PeriodicTimer×AllyMoveSpeedAura 슬롯이 베이크됐다");
            var whip = slots[whipIdx];
            Assert.Greater(whip.magnitude, 0f, "가속 % 저작됨");
            // authoring 계약(nightmare-whip-aura): 펄스 오라는 TTL 이 주기보다 길어야
            // 다음 펄스가 오기 전에 안 꺼진다. 이게 무너지면 화면에서 가속이 깜빡인다.
            Assert.Greater(whip.duration, whip.periodSeconds,
                "TTL(duration) > 주기(periodSeconds) — 펄스 사이 유지가 이 오라의 저작 계약이다");
            // 동행 고정 오프셋(2타일)이 반경 안이어야 한다. 저작이 반경을 1로 줄이면 loud.
            Assert.GreaterOrEqual(whip.tileRange, 2,
                "오라 반경이 2 미만이면 동행 고정 오프셋(2타일)이 밖으로 나간다 — 테스트 재설계 필요");
            float expectedMul = 1f + whip.magnitude / 100f;

            // 주기만 즉발로 (배율·TTL·반경은 에셋 그대로 — BossLullabyTest 선례).
            var w = slots[whipIdx];
            w.periodSeconds = 0.01f;
            w.elapsed = 0f;
            slots[whipIdx] = w;

            // 워밍업: 펄스 발화 + 모디파이어 적용이 몇 프레임 걸린다. 호스트를 수혜자 옆
            // 2타일에 동행 고정(호스트 자신 제외 규칙이 있으므로 겹치지 않게, 분리 밀침이
            // 안 닿게 1타일보다 멀리).
            for (int i = 0; i < 10; i++)
            {
                PinBossNear(em, boss, ally, tile);
                yield return null;
            }

            // ── 버프 속도 ────────────────────────────────────────────────────
            float buffDist = 0f, buffDt = 0f;
            yield return Measure(em, ally, tile, 30, v => { buffDist += v.x; buffDt += v.y; },
                perFrame: () => PinBossNear(em, boss, ally, tile));
            Assert.IsTrue(em.Exists(ally), "측정 중 수혜자가 골에 도달해 사라지면 창이 무효다");
            float v1 = buffDist / math.max(1e-4f, buffDt);

            // 저작 배율 그대로 빨라졌는가. 위로도 조이는 단언이다 — 펄스가 중첩 누적되면
            // (merge-refresh 계약 파괴) 비율이 배율² 이상으로 튀어 빨개진다.
            Assert.AreEqual(expectedMul, v1 / v0, 0.12f,
                $"반경 안 같은 진영 적의 실측 속도비(v1/v0)가 저작 배율(×{expectedMul:F2})과 다르다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // frames 동안 프레임별 (경로 길이 증가분, dt) 를 콜백으로 넘긴다.
        // 프레임당 2타일 초과 점프(포탈 등)는 경로 길이 측정을 오염시키므로 loud 하게 끊는다.
        private static IEnumerator Measure(
            EntityManager em, Entity e, float tile, int frames,
            System.Action<float2> add, System.Action perFrame = null)
        {
            float3 prev = em.GetComponentData<LocalTransform>(e).Position;
            for (int i = 0; i < frames; i++)
            {
                perFrame?.Invoke();
                yield return null;
                Assert.IsTrue(em.Exists(e), "측정 대상 소멸 — 창이 무효다");
                float3 cur = em.GetComponentData<LocalTransform>(e).Position;
                float d = math.distance(new float2(cur.x, cur.z), new float2(prev.x, prev.z));
                Assert.Less(d, 2f * tile, "측정 창에 순간이동이 섞였다(포탈?) — 속도 측정 무효");
                add(new float2(d, Time.deltaTime));
                prev = cur;
            }
        }

        private static void PinBossNear(EntityManager em, Entity boss, Entity ally, float tile)
        {
            if (!em.Exists(ally) || !em.Exists(boss)) return;
            float3 a = em.GetComponentData<LocalTransform>(ally).Position;
            var t = em.GetComponentData<LocalTransform>(boss);
            t.Position = new float3(a.x + 2f * tile, t.Position.y, a.z);
            em.SetComponentData(boss, t);
        }

        private static FlowFieldSingleton GetFlowField(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            return q.GetSingleton<FlowFieldSingleton>();
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }
    }
}
