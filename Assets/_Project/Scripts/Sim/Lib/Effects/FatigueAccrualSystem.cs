using System.Collections.Generic;
using Wassup.Sim.Units;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/5 — 야근 기믹 저작값. 구 `BurnoutGimmickConfig` 이식.
    ///
    /// ⚠ **존재 = 기믹 활성**이다. 구 sim 의 `RequireForUpdate&lt;BurnoutGimmickConfig&gt;` 가
    /// 그 의미를 지고 있었고(부재면 시스템이 아예 안 돈다), 신 sim 에서는 싱글턴 엔티티의
    /// 컴포넌트 유무가 같은 역할을 한다. `SimConfig` 로 옮기지 않은 이유가 이것이다 —
    /// 거기 넣으면 "활성/비활성" 을 별도 플래그로 표현해야 하고, 그 순간 부재라는 상태가
    /// 사라져 특성화(`NoGimmickConfig_SelfGate_NeitherAttachesNorEnqueues`)가 표현 불가해진다.
    /// </summary>
    public struct BurnoutGimmickConfig
    {
        public float fatigueInterval;
        public byte fatigueAmount;
        public byte fatigueMaxStack;
        public float fatiguePerAppDuration;
    }

    /// 피로도 누적 타이머(Effects 소유). 구 `FatigueAccrual` 이식.
    public struct FatigueAccrual
    {
        public float elapsed;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-C/5 — 캡처 #28 · <see cref="SimPhase.ModifierTick"/>(P7).
    /// 구 `Wassup.Battle.Effects.FatigueAccrualSystem` 이식.
    ///
    /// 배치된 방어 유닛이 `fatigueInterval` 마다 피로도 스택을 쌓는다. 임계 도달 시의 번아웃은
    /// <see cref="StackModifierTickSystem"/> + 임계 규칙이 처리한다 — 이 시스템은 **누적 소스일 뿐**이다.
    ///
    /// ⚠ **여기서 발행한 스택은 다음 틱에 적용된다.** 소비자 `ModifierApplySystem` 이 P2 고
    /// 이 시스템은 P7 이라, 생산이 소비 뒤다 — 구조적 1틱 지연이다(<see cref="SimChannel{T}"/>
    /// 주석의 "지연은 phase 순서에서 파생된다"). 선언이 아니라 배치가 보장한다.
    /// </summary>
    public sealed class FatigueAccrualSystem
    {
        private readonly SimChannel<StackModifierApplyEvent> _stackChannel;
        private readonly List<SimEntityId> _toAttach = new List<SimEntityId>();

        public FatigueAccrualSystem(SimChannel<StackModifierApplyEvent> stackChannel)
            => _stackChannel = stackChannel;

        public void Run(SimWorld world)
        {
            // self-gate — 기믹 비활성이면 **아무것도 하지 않는다**(부착조차).
            if (!TryGetSingleton(world, out BurnoutGimmickConfig config)) return;

            // 잘못 저작된 데이터에 대한 무한 루프 방어. ⚠ 이 return 은 Pass 1(부착)보다 **앞**이다 —
            // 뒤로 옮기면 타이머가 붙어 상태(컴포넌트 유무)가 갈린다.
            if (config.fatigueInterval <= 0f) return;

            // ── Pass 1 — lazy attach ──────────────────────────────────────────
            // 스폰 경로를 건드리지 않고 배치된 defender 에 타이머를 붙인다.
            _toAttach.Clear();
            foreach (SimEntityId e in world.With<DefenderUnitTag>())
                if (!world.Has<FatigueAccrual>(e)) _toAttach.Add(e);

            for (int i = 0; i < _toAttach.Count; i++)
                world.Set(_toAttach[i], new FatigueAccrual { elapsed = 0f });

            // ── Pass 2 — 주기마다 피로도 스택 발행 ────────────────────────────
            // 구 sim 의 중간 Playback 보존: 위에서 방금 붙은 타이머도 **이번 프레임에** 누적한다.
            float dt = world.DeltaTime;
            foreach (SimEntityId e in world.With<FatigueAccrual>())
            {
                if (!world.Has<DefenderUnitTag>(e)) continue;

                FatigueAccrual accrual = world.Get<FatigueAccrual>(e);
                accrual.elapsed += dt;

                // ⚠ `while` 이다. 한 틱이 여러 주기를 건너뛰면 건너뛴 만큼 전부 발행하고
                // 나머지는 이월한다. `if` 나 `elapsed = 0` 으로 바꾸면 저프레임·슬로모 복귀
                // 구간에서 피로도가 조용히 유실된다.
                while (accrual.elapsed >= config.fatigueInterval)
                {
                    accrual.elapsed -= config.fatigueInterval;
                    _stackChannel.Enqueue(new StackModifierApplyEvent
                    {
                        target = e,
                        kind = StackKind.Fatigue,
                        countDelta = config.fatigueAmount,
                        maxStack = config.fatigueMaxStack,
                        perAppDuration = config.fatiguePerAppDuration,
                        source = e,   // 자기 자신 — 병합 키의 한 축이다
                    });
                }

                world.Set(e, accrual);
            }
        }

        /// <summary>
        /// 구 `SystemAPI.GetSingleton&lt;T&gt;` 대응. 생성 순서 첫 보유자를 싱글턴으로 본다.
        /// 지금은 호출처가 하나라 여기 둔다 — 18-B 가 게이트 53건을 옮기며 같은 모양이 반복되면
        /// 그때 <see cref="SimWorld"/> 로 올린다(반복이 생긴 뒤에 추출한다).
        /// </summary>
        private static bool TryGetSingleton<T>(SimWorld world, out T value) where T : struct
        {
            foreach (SimEntityId e in world.With<T>())
            {
                value = world.Get<T>(e);
                return true;
            }
            value = default;
            return false;
        }
    }
}
