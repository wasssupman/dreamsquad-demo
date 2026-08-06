// battle-sim-extraction unit 18-C/7 — **성능 프로브**(계획서 중단 기준 ④).
//
// 왜 지금인가: 신 sim 은 Burst/네이티브 컬렉션을 잃고 관리 컬렉션으로 간다. 그 비용이 감당
// 불가면 되돌릴 반경이 **S11 에서는 7,000줄**이다. 그래서 첫 이식 조각 직후에 잰다.
//
// ⚠ **이것은 unit 20 의 성능 게이트가 아니다.** 여기는 에디터 · x64 · Mono 이고, 진짜 게이트는
// ARM64 IL2CPP p95/p99 다(`20_ab_parity_swap.md`). 여기서 보는 것은 **자릿수**와
// **틱당 관리 할당량**뿐이다 — 후자는 기기 성능과 무관하게 구조가 정한다.
using System;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Wassup.Sim;
using Legacy = Wassup.Battle.Effects;
using Fresh = Wassup.Sim.Effects;

namespace Wassup.Tests.EditMode
{
    public class SimModifierPerfProbeTests
    {
        private const int EntityCount = 100;
        private const int TickCount = 10_000;
        private const float Dt = 1f / 60f;

        /// 매 N 틱마다 모디파이어를 재적용해 Apply·Aggregate 가 실제로 일하게 한다
        /// (슬롯이 굳어 있으면 dirty 가 꺼진 채로 Aggregate 가 놀아 측정이 무의미해진다).
        private const int ReapplyEvery = 50;

        [Test]
        public void ModifierTick_ManagedCost_IsWithinOrderOfMagnitude_AndAllocationIsPerTickBounded()
        {
            // ⚠ **계측기부터 검증한다.** `GC.GetAllocatedBytesForCurrentThread` 는 런타임에 따라
            // 구현이 없어 **항상 0** 을 돌려줄 수 있고, 그러면 게이트 ①이 조용한 no-op 이 된다
            // (이 spec 이 `SimConfig` 에서 구조로 막았던 바로 그 실패 모양이다). 알려진 크기를
            // 할당해 카운터가 실제로 움직이는지 먼저 본다.
            const int ControlBytes = 1 << 20;
            long ctrlBefore = GC.GetAllocatedBytesForCurrentThread();
            var control = new byte[ControlBytes];
            control[0] = 1;
            long ctrlDelta = GC.GetAllocatedBytesForCurrentThread() - ctrlBefore;
            GC.KeepAlive(control);
            bool allocCounterWorks = ctrlDelta >= ControlBytes;

            double legacyMs = MeasureLegacy();
            (double freshMs, long freshBytes) = MeasureFresh();

            double perTickBytes = freshBytes / (double)TickCount;
            double ratio = freshMs / Math.Max(legacyMs, 0.001);
            string allocLine = allocCounterWorks
                ? $"{freshBytes / 1024.0:F1} KB 총 · {perTickBytes:F0} B/tick"
                : $"**계측 불가**(카운터 control delta={ctrlDelta} B, 기대 >= {ControlBytes})";

            UnityEngine.Debug.Log(
                $"[18-C 성능 프로브] 엔티티 {EntityCount} × 틱 {TickCount}\n" +
                $"  구 sim(ECS)  : {legacyMs:F1} ms  ({legacyMs * 1000.0 / TickCount:F2} µs/tick)\n" +
                $"  신 sim(관리) : {freshMs:F1} ms  ({freshMs * 1000.0 / TickCount:F2} µs/tick)\n" +
                $"  비율         : ×{ratio:F2}\n" +
                $"  신 sim 할당  : {allocLine}");

            // ── 게이트 ① 틱당 관리 할당이 **엔티티 수에 비례하지 않는다** ──────────
            // 이것이 구조 게이트다. 엔티티당 무엇이든 할당하면 100개에서 이미 KB 단위가 되고,
            // 실제 판(수백 유닛)에서 모바일 GC 를 때린다. 반복자·재사용 버퍼로 상수에 묶여 있어야 한다.
            //
            // 계측이 불가하면 **통과시키지 않고 명시적으로 비운다** — 0 을 사실로 보고하는 것이
            // 이 프로브가 막으려는 바로 그 거짓 신호다. 시간 게이트(②)는 그대로 판정한다.
            if (allocCounterWorks)
            {
                Assert.Less(perTickBytes, 1024,
                    $"틱당 관리 할당이 {perTickBytes:F0} B — 엔티티당 할당이 생겼는지 확인할 것. " +
                    "시스템 루프 안의 new/람다/박싱이 흔한 원인이다.");
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[18-C 성능 프로브] 할당 게이트를 판정하지 못했다 — 이 런타임에서 " +
                    "GC.GetAllocatedBytesForCurrentThread 가 동작하지 않는다. " +
                    "틱당 할당량은 unit 20 의 기기 프로파일에서 확인할 것.");
            }

            // ── 게이트 ② 자릿수 ──────────────────────────────────────────────────
            // 등가는 기대하지 않는다(구 sim 은 Burst + 네이티브 청크다). 자릿수가 갈리면
            // 그건 구현이 아니라 **표현**의 문제이고, 그때가 18-A 를 재설계할 시점이다.
            Assert.Less(ratio, 10.0,
                $"신 sim 이 구 sim 의 ×{ratio:F1} — 자릿수가 갈렸다. 중단 기준 ③ 검토 대상.");
        }

        /// <summary>
        /// 18-N2 (3렌즈 리뷰 F4) — **`SimCommandBuffer` 를 실제로 쓰는 경로**의 할당 게이트.
        ///
        /// ⚠ 위 프로브는 좋은 게이트인데 **엉뚱한 클러스터를 겨누고 있었다** — 돌리는 대상이
        /// `ModifierCluster` 하나뿐이고, 그 클러스터의 `SimCommandBuffer` 사용은 **0** 이다.
        /// 실사용 6개 시스템(`ProjectileHit`·`ProjectileMove`·`ProjectileEmitter`·
        /// `DamageApplication`·`LifecycleSystems`·`ResignationDrop`)이 전부 게이트 밖이었다.
        ///
        /// 여기서 겨누는 것은 착탄 해결이다 — 틱당 피격자 수만큼 `_ecb.Set`(피격 플래시)과
        /// 바운스 상태 갱신이 나간다. 초판 구현(클로저 기록)이면 **op 당 힙 객체 2개**라
        /// 엔티티 수에 비례해 늘어난다.
        ///
        /// 합성 시나리오임을 밝혀 둔다 — 매 틱 `impactReached` 를 다시 세워 해결을 강제한다.
        /// 실제 판보다 착탄 밀도가 높고, 그게 의도다(게이트는 최악에 가까워야 한다).
        /// </summary>
        [Test]
        public void ImpactResolution_AllocationIsPerTickBounded()
        {
            const int Victims = 100;
            const int Ticks = 200;

            var world = new SimWorld(new SimConfig(1u, 1u));
            var channels = new SimChannels();
            var hit = new Wassup.Sim.Combat.ProjectileHitSystem(channels);
            world.SetDeltaTime(0.016f);

            var shots = new System.Collections.Generic.List<SimEntityId>(Victims);
            for (int i = 0; i < Victims; i++)
            {
                var pos = new SimVec3(i % 10, 0f, i / 10);

                var victim = world.Create();
                world.Set(victim, new Wassup.Sim.Units.AttackUnitTag());
                world.Set(victim, Wassup.Sim.Movement.SimTransform.FromPosition(pos));
                world.AddBuffer<Wassup.Sim.Units.IncomingDamage>(victim);

                var shot = world.Create();
                world.Set(shot, new Wassup.Sim.Combat.ProjectileTag());
                world.Set(shot, Wassup.Sim.Movement.SimTransform.FromPosition(pos));
                world.Set(shot, new Wassup.Sim.Combat.ProjectileState
                {
                    payload = Wassup.Sim.Combat.PayloadKind.SingleSplash,
                    target = victim, damage = 1f, impactReached = true,
                    // 매 틱 다시 튕겨 살아남게 한다 — 정상 상태를 유지해야 틱당 할당이 보인다.
                    bounceRemaining = Ticks + 1, bounceTileRange = 8, bounceDamageMul = 1f,
                });
                shots.Add(shot);
            }

            // ⚠ **계측기부터 검증한다.** 이 런타임(Mono)에서 `GC.GetAllocatedBytesForCurrentThread`
            //   는 **항상 0** 을 돌려준다 — 실측 확인했다. 그러면 `0 < 1024` 가 자명 통과라
            //   게이트가 조용한 no-op 이 된다. 위 프로브가 이미 이 함정을 해결해 뒀는데
            //   초판이 그대로 다시 밟았다(실제로 "0 B/tick" 을 통과로 보고했다).
            const int ControlBytes = 1 << 20;
            long ctrlBefore = GC.GetAllocatedBytesForCurrentThread();
            var control = new byte[ControlBytes];
            control[0] = 1;
            long ctrlDelta = GC.GetAllocatedBytesForCurrentThread() - ctrlBefore;
            GC.KeepAlive(control);
            bool allocCounterWorks = ctrlDelta >= ControlBytes;

            // 워밍업 — 타입별 저장소·리스트 용량이 자리를 잡은 뒤 계측한다.
            for (int t = 0; t < 5; t++) { Arm(); hit.Run(world); Drain(); }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int t = 0; t < Ticks; t++) { Arm(); hit.Run(world); Drain(); }
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            double perTick = bytes / (double)Ticks;

            UnityEngine.Debug.Log(
                $"[18-N2 착탄 할당 프로브] 피격자 {Victims} × 틱 {Ticks} → " +
                (allocCounterWorks
                    ? $"{perTick:F0} B/tick"
                    : $"**계측 불가**(카운터 control delta={ctrlDelta} B, 기대 >= {ControlBytes})"));

            if (allocCounterWorks)
            {
                Assert.Less(perTick, 1024,
                    $"착탄 해결의 틱당 할당이 {perTick:F0} B (피격자 {Victims}) — 피격자당 할당이 생겼다. " +
                    "SimCommandBuffer 의 값 기록이 클로저로 되돌아갔는지 먼저 볼 것.");
            }
            else
            {
                // **통과시키지 않고 명시적으로 비운다** — 0 을 사실로 보고하는 것이 이 프로브가
                // 막으려는 바로 그 거짓 신호다.
                Assert.Inconclusive(
                    "[18-N2] 할당 게이트를 판정하지 못했다 — 이 런타임에서 " +
                    "GC.GetAllocatedBytesForCurrentThread 가 동작하지 않는다. " +
                    "SimCommandBuffer 의 값 기록 전환은 **코드 리뷰로만** 확인된 상태이고, " +
                    "수치 검증은 unit 20 의 기기 프로파일로 이관된다.");
            }

            void Arm()
            {
                for (int i = 0; i < shots.Count; i++)
                {
                    var s = world.Get<Wassup.Sim.Combat.ProjectileState>(shots[i]);
                    s.impactReached = true;
                    world.Set(shots[i], s);
                }
            }

            // 채널이 무한히 자라면 그게 할당으로 잡혀 게이트가 엉뚱한 것을 잡는다.
            void Drain()
            {
                channels.ProjectileHit.Drain();
                channels.ThreatHit.Drain();
            }
        }

        // ── 구 sim ────────────────────────────────────────────────────────────────

        private static double MeasureLegacy()
        {
            var world = new World("PerfProbe_Legacy");
            var em = world.EntityManager;
            var group = world.CreateSystemManaged<SimulationSystemGroup>();
            group.AddSystemToUpdateList(world.CreateSystem<Legacy.ModifierApplySystem>());
            group.AddSystemToUpdateList(world.CreateSystem<Legacy.StatModifierTickSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<Legacy.ModifierStatsAggregateSystem>());
            group.AddSystemToUpdateList(world.CreateSystem<Legacy.StackModifierTickSystem>());

            var statQ = new NativeQueue<Legacy.StatModifierApplyEvent>(Allocator.Persistent);
            var stackQ = new NativeQueue<Legacy.StackModifierApplyEvent>(Allocator.Persistent);
            var ccQ = new NativeQueue<Legacy.EnemyCcEvent>(Allocator.Persistent);
            var dotQ = new NativeQueue<Legacy.DotApplyEvent>(Allocator.Persistent);
            Singleton(em, new Legacy.StatModifierApplyEventsSingleton { queue = statQ });
            Singleton(em, new Legacy.StackModifierApplyEventsSingleton { queue = stackQ });
            Singleton(em, new Legacy.EnemyCcEventsSingleton { queue = ccQ });
            Singleton(em, new Legacy.DotApplyEventsSingleton { queue = dotQ });
            Legacy.StackThresholdRegistry.Clear();

            var entities = new Entity[EntityCount];
            for (int i = 0; i < EntityCount; i++)
            {
                var e = em.CreateEntity();
                em.AddComponentData(e, new Legacy.ModifierStats
                {
                    damageMul = 1f, attackSpeedMul = 1f, dmgTakenMul = 1f,
                    regenPerSec = 0f, moveSpeedMul = 1f, damageVsCcMul = 1f, maxHealthMul = 1f,
                });
                em.AddComponent<Legacy.ModifierStatsDirty>(e);
                em.SetComponentEnabled<Legacy.ModifierStatsDirty>(e, false);
                entities[i] = e;
            }

            var sw = Stopwatch.StartNew();
            for (int t = 0; t < TickCount; t++)
            {
                if (t % ReapplyEvery == 0)
                    for (int i = 0; i < EntityCount; i++)
                        statQ.Enqueue(new Legacy.StatModifierApplyEvent
                        {
                            target = entities[i], stat = Legacy.StatKind.DamageMul,
                            op = Legacy.CombineOp.Additive, magnitude = 0.1f,
                            duration = ReapplyEvery * Dt * 0.5f, source = entities[i], stackId = 0,
                        });

                world.SetTime(new TimeData(world.Time.ElapsedTime + Dt, Dt));
                group.Update();
            }
            sw.Stop();

            statQ.Dispose(); stackQ.Dispose(); ccQ.Dispose(); dotQ.Dispose();
            Legacy.StackThresholdRegistry.Clear();
            world.Dispose();
            return sw.Elapsed.TotalMilliseconds;
        }

        private static void Singleton<T>(EntityManager em, T value) where T : unmanaged, IComponentData
            => em.AddComponentData(em.CreateEntity(), value);

        // ── 신 sim ────────────────────────────────────────────────────────────────

        private static (double ms, long bytes) MeasureFresh()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            var channels = new SimChannels();
            var cluster = new Fresh.ModifierCluster(channels);

            var tick = new SimPipeline().Add(cluster.Steps()).Build();

            var entities = new SimEntityId[EntityCount];
            for (int i = 0; i < EntityCount; i++)
            {
                var e = world.Create();
                world.Set(e, Fresh.ModifierStats.Identity);
                entities[i] = e;
            }

            // 워밍업 — 지연 초기화(스토어 딕셔너리 생성 등)를 측정에서 뺀다.
            for (int t = 0; t < 100; t++) tick.Run(world, Dt);

            long before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int t = 0; t < TickCount; t++)
            {
                if (t % ReapplyEvery == 0)
                    for (int i = 0; i < EntityCount; i++)
                        channels.StatApply.Enqueue(new Fresh.StatModifierApplyEvent
                        {
                            target = entities[i], stat = Fresh.StatKind.DamageMul,
                            op = Fresh.CombineOp.Additive, magnitude = 0.1f,
                            duration = ReapplyEvery * Dt * 0.5f, source = entities[i], stackId = 0,
                        });

                tick.Run(world, Dt);
            }
            sw.Stop();
            long after = GC.GetAllocatedBytesForCurrentThread();

            return (sw.Elapsed.TotalMilliseconds, after - before);
        }
    }
}
