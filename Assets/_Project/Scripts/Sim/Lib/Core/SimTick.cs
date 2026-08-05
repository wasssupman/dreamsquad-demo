using System;
using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — 틱 phase 슬롯. **정본은 `order-capture.md` 의 44 총순서**
    /// 이고 청사진 ③ §1 이 그것을 P1~P12 로 접은 것이다. 이 enum 은 그 접기를 코드로 옮긴 것뿐이며
    /// **재배치 결정은 0** 이다(청사진 ③ §7).
    ///
    /// 스케치와 어긋나 캡처를 따른 3지점 — 직관으로 고치고 싶어지는 자리라 여기 적어 둔다:
    /// **투사체(P6)가 공격(P8)보다 앞** · **DotApply(P3)가 이동(P4) 앞** · **CC 감쇠(P11)는 사망 창 뒤**.
    /// </summary>
    public enum SimPhase
    {
        /// #1~7 필드·존 재구축 + 주기 효과. 매 틱 재빌드 계열("갱신이 곧 회수").
        FieldsAndPeriodic = 1,
        /// #8~10 큐 반입(어그로·모디파이어·CC).
        Intake = 2,
        /// #11~16 사망 보완·자폭·전투 준비.
        PreCombat = 3,
        /// #17 이동 — 위치 갱신 **단일 권한**.
        Movement = 4,
        /// #18~25 이동-후 캐스트·기믹·픽업.
        PostMoveCast = 5,
        /// #26~27 투사체. **공격보다 앞**(캡처 정본).
        Projectiles = 6,
        /// #28~32 모디파이어 tick·집계.
        ModifierTick = 7,
        /// #33 공격 통합 루프.
        Attack = 8,
        /// #34 피해 정산 — 피해 유래 DeadTag **마킹**(파괴 아님).
        DamageResolve = 9,
        /// #35~37 "죽었지만 아직 있는" 창 관찰. 파괴 전 정보를 읽는 유일한 자리.
        DeathWindow = 10,
        /// #38~40 발사 명세·후처리. CC 감쇠가 여기(사망 창 **뒤**).
        PostProcess = 11,
        /// #41~44 파괴·임계·도약. **유일한 파괴자**가 여기 산다.
        Destruction = 12,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-A — 틱 골격.
    ///
    /// 18-A 시점에는 **모든 슬롯이 비어 있다.** 조각 18-C~18-J 가 자기 클러스터의 규칙을 해당
    /// phase 에 등록한다. 골격이 먼저 서는 이유는 **채널의 같은틱/1틱-지연이 phase 순서에서
    /// 파생**되기 때문이다(`SimChannel` 주석) — 순서가 없으면 지연 계약을 표현할 수 없다.
    ///
    /// ⚠ **phase 안의 등록 순서도 계약이다.** P1 은 #1~#7 순이고 그 안에서도 캡처 순서를 따른다.
    /// 등록 순서를 바꾸면 매 틱 재발행 계열(#3·#5)의 발행↔소비 관계가 흔들린다.
    ///
    /// P0/P13 은 여기 없다 — 커맨드 반입·읽기 모델 스탬프는 **18-K** 가 흡수한다(계획서).
    /// </summary>
    public sealed class SimTick
    {
        private readonly Dictionary<SimPhase, List<Action<SimWorld>>> _slots =
            new Dictionary<SimPhase, List<Action<SimWorld>>>();

        /// 캡처 순서 — 이 배열이 곧 phase 실행 순서다.
        private static readonly SimPhase[] Order =
        {
            SimPhase.FieldsAndPeriodic, SimPhase.Intake, SimPhase.PreCombat, SimPhase.Movement,
            SimPhase.PostMoveCast, SimPhase.Projectiles, SimPhase.ModifierTick, SimPhase.Attack,
            SimPhase.DamageResolve, SimPhase.DeathWindow, SimPhase.PostProcess, SimPhase.Destruction,
        };

        public static IReadOnlyList<SimPhase> PhaseOrder => Order;

        /// <summary>
        /// 등록 순서가 실행 순서다(같은 phase 안). 조각들이 자기 시스템을 캡처 번호 순으로 넣는다.
        /// </summary>
        public void Register(SimPhase phase, Action<SimWorld> step)
        {
            if (!_slots.TryGetValue(phase, out var list)) _slots[phase] = list = new List<Action<SimWorld>>();
            list.Add(step);
        }

        public int StepCount(SimPhase phase) => _slots.TryGetValue(phase, out var l) ? l.Count : 0;

        /// <summary>
        /// 델타를 월드에 실은 뒤 phase 순서로 돌린다 — 구 sim 하네스의
        /// `world.SetTime(...)` → `simGroup.Update()` 와 같은 배치다. 시간의 writer 를 여기
        /// 하나로 두어 phase 중간에 시계가 바뀌는 경로를 만들지 않는다.
        /// </summary>
        public void Run(SimWorld world, float deltaTime)
        {
            world.SetDeltaTime(deltaTime);
            for (int p = 0; p < Order.Length; p++)
            {
                if (!_slots.TryGetValue(Order[p], out var list)) continue;
                for (int i = 0; i < list.Count; i++) list[i](world);
            }
        }
    }
}
