using System.Collections.Generic;
using Wassup.Sim.Combat;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-D — 캡처 **#10** · <see cref="SimPhase.Intake"/>(P2).
    /// 구 `CcApplySystem` 이식. **#9 `ModifierApply` 바로 뒤**다.
    ///
    /// ⚠ **보스 면역이 여기 한 곳에 있다.** 모든 CC 생산자(공격·투사체·존·스택 임계)가 이 채널로
    /// 수렴하므로 **부여 시점 1곳**에서 막으면 끝난다. 판정 쪽(이동·공격 락·변위·상태FX·
    /// wake-on-hit)에 넣으면 무시 지점이 6곳 이상이라 회귀 표면이 훨씬 커진다.
    /// </summary>
    public sealed class CcApplySystem
    {
        private readonly SimChannel<EnemyCcEvent> _channel;
        public CcApplySystem(SimChannel<EnemyCcEvent> channel) => _channel = channel;

        public void Run(SimWorld world)
        {
            List<EnemyCcEvent> events = _channel.Drain();
            for (int i = 0; i < events.Count; i++)
            {
                EnemyCcEvent evt = events[i];
                if (!world.Exists(evt.target)) continue;
                if (world.Has<BossTag>(evt.target) && CcActionLock.IsBossImmune(evt.effect.kind)) continue;

                // ⚠ 구 sim 은 여기서 `HasBuffer` 를 확인하지 **않는다**(`GetBuffer` 직행) —
                // CC 대상은 스폰 시 버퍼를 갖는다는 전제다. 부재면 구 sim 은 던진다.
                // 신 sim 은 `AddBuffer`(있으면 기존 반환)로 그 전제를 흡수한다: 성공 경로의
                // 결과가 같고, 없던 크래시를 새로 만들지 않는다.
                // **DoT 쪽은 반대다** — 아래 `DotApplySystem` 은 부재를 명시적으로 건너뛴다.
                // 그 비대칭은 구 sim 의 실존 성질이라 보존한다.
                CcEffectMerge.Apply(world.AddBuffer<CcEffect>(evt.target), evt.effect);
            }
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — 캡처 **#15** · <see cref="SimPhase.PreCombat"/>(P3).
    /// 구 `DotApplySystem` 이식. 지속 피해 파이프라인 **전체**(부여 → 틱 → 감쇠)다.
    ///
    /// ⚠ **순서가 계약이다**: 먼저 틱을 지급하고 **그 다음** `remainingTime` 을 깎는다.
    /// 뒤집으면 지속 1.35s·주기 1.0s 같은 경계값의 틱 수가 달라진다.
    ///
    /// ⚠ **지급은 정방향, 만료 제거는 역순 별도 패스**다. 지급을 역순으로 돌면 여러 도트가 걸린
    /// 대상의 **데미지 숫자 표시 순서**가 조용히 뒤집힌다.
    ///
    /// ⚠ P3 이므로 소비자 `DamageApplication`(#34, P9)보다 **앞**이다 — 이번 프레임에 기록한
    /// `IncomingDamage` 가 **같은 프레임에** 정산된다.
    /// </summary>
    public sealed class DotApplySystem
    {
        private readonly SimChannel<DotApplyEvent> _channel;
        private readonly SimChannel<HazardRuntimeEvent> _runtimeLog;

        public DotApplySystem(SimChannel<DotApplyEvent> channel, SimChannel<HazardRuntimeEvent> runtimeLog)
        {
            _channel = channel;
            _runtimeLog = runtimeLog;
        }

        public void Run(SimWorld world)
        {
            // 1. 부여 — (origin, element) 로 병합.
            List<DotApplyEvent> events = _channel.Drain();
            for (int i = 0; i < events.Count; i++)
            {
                DotApplyEvent evt = events[i];
                if (!world.Exists(evt.target)) continue;
                List<DotEffect> buffer = world.GetBuffer<DotEffect>(evt.target);
                if (buffer == null) continue;   // ⚠ 부재는 건너뛴다(CC 와 비대칭 — 위 참조)
                DotEffectMerge.Apply(buffer, evt.effect);
            }

            // 2. 틱 + 3. 감쇠
            //
            // 구 sim 은 로그 채널 유무로 **두 job 변형**을 갈랐다. 그 분기의 이유는 Burst 였고
            // (쓰지 않는 `NativeQueue.ParallelWriter` 필드가 스케줄 안전성 검사에 걸린다),
            // **피해 계산은 두 변형이 완전히 동일**하다. 관리 코드엔 그 제약이 없으므로 분기 없이
            // 항상 로그를 싣는다 — 로그는 상태 해시에 실리지 않는다.
            float dt = world.DeltaTime;
            foreach (SimEntityId e in world.WithBuffer<DotEffect>())
            {
                List<DotEffect> dots = world.GetBuffer<DotEffect>(e);
                List<IncomingDamage> damage = world.GetBuffer<IncomingDamage>(e);
                // 구 job 은 두 버퍼를 모두 요구한다 — 피해 버퍼가 없으면 도트가 **틱조차 하지 않는다**.
                if (damage == null) continue;

                for (int i = 0; i < dots.Count; i++)
                {
                    DotEffect dot = dots[i];

                    if (dot.tickInterval <= 0f)
                    {
                        // 레거시 연속: scalar = DPS. 프레임당 피해 1 + 로그 1.
                        float amount = dot.scalar * dt;
                        damage.Add(new IncomingDamage { amount = amount });
                        _runtimeLog.Enqueue(new HazardRuntimeEvent
                        {
                            eventType = HazardRuntimeEventType.DotDamage,
                            kind = CcKind.DoT,
                            target = e,
                            scalar = dot.scalar,
                            amount = amount,
                        });
                    }
                    else
                    {
                        // 이산 tick: scalar = 틱당 피해. 청크 1개 = IncomingDamage 1개 = 폰트 1개.
                        int ticks = DotTick.Advance(ref dot.tickTimer, dot.tickInterval, dt);
                        for (int t = 0; t < ticks; t++)
                        {
                            damage.Add(new IncomingDamage { amount = dot.scalar });
                            _runtimeLog.Enqueue(new HazardRuntimeEvent
                            {
                                eventType = HazardRuntimeEventType.DotDamage,
                                kind = CcKind.DoT,
                                target = e,
                                scalar = dot.scalar,
                                amount = dot.scalar,
                            });
                        }
                    }

                    dot.remainingTime -= dt;
                    dots[i] = dot;   // tickTimer · remainingTime 되쓰기
                }

                for (int i = dots.Count - 1; i >= 0; i--)
                    if (dots[i].remainingTime <= 0f) RemoveAtSwapBack(dots, i);
            }
        }

        internal static void RemoveAtSwapBack<T>(List<T> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — 캡처 **#37** · <see cref="SimPhase.DeathWindow"/>(P10).
    /// 구 `CcClearSystem` 이식. wake-on-hit 소비자다.
    ///
    /// `DamageApplication`(#34, P9)이 넣은 해제 요청을 **같은 프레임에** 소비한다 —
    /// 다음 프레임으로 밀리면 잠든 적이 한 틱 더 자고 맞는다.
    /// </summary>
    public sealed class CcClearSystem
    {
        private readonly SimChannel<CcClearRequest> _channel;
        public CcClearSystem(SimChannel<CcClearRequest> channel) => _channel = channel;

        public void Run(SimWorld world)
        {
            List<CcClearRequest> reqs = _channel.Drain();
            for (int i = 0; i < reqs.Count; i++)
            {
                CcClearRequest req = reqs[i];
                if (!world.Exists(req.entity)) continue;      // 치명타로 파괴됐을 수 있다
                List<CcEffect> buf = world.GetBuffer<CcEffect>(req.entity);
                if (buf == null) continue;
                for (int k = buf.Count - 1; k >= 0; k--)
                    if (buf[k].kind == req.kind) DotApplySystem.RemoveAtSwapBack(buf, k);
            }
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-D — 캡처 **#40** · <see cref="SimPhase.PostProcess"/>(P11).
    /// 구 `CcDecaySystem` 이식.
    ///
    /// ⚠ **사망 창(P10) 뒤**다. 직관으로 "감쇠는 이동 직후" 라고 옮기고 싶어지는 자리이고,
    /// 캡처가 정본이다(`SimPhase` 주석의 3지점 중 하나).
    /// ⚠ DoT 감쇠는 여기가 아니다 — <see cref="DotApplySystem"/> 이 자기 수명을 관리한다.
    /// </summary>
    public sealed class CcDecaySystem
    {
        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;
            foreach (SimEntityId e in world.WithBuffer<CcEffect>())
            {
                List<CcEffect> buf = world.GetBuffer<CcEffect>(e);
                for (int i = buf.Count - 1; i >= 0; i--)
                {
                    CcEffect entry = buf[i];
                    entry.remainingTime -= dt;
                    if (entry.remainingTime <= 0f) DotApplySystem.RemoveAtSwapBack(buf, i);
                    else buf[i] = entry;
                }
            }
        }
    }
}
