using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E — 환경 클러스터(수명·필드·존).
    ///
    /// **7/8 이다.** #18 `HazardCast` 는 **18-I 로 이관**했다 — 그 시스템의 캐스트-이벤트 arm 이
    /// `DcTriggerSlot`(25필드 + `Wassup.Data` enum 4개) 의 **버퍼 존재**를 보는데, 그 타입의
    /// 쓰기 소유자가 `AttackSystem` 이다. 존재 확인 하나 때문에 18-I 의 타입을 추측으로 먼저
    /// 옮기면 필드 하나만 틀려도 parity 가 깨진다(`AttackState` 를 9필드 전부 옮긴 것과 같은 이유).
    /// 나머지 의존(`HazardCastState`·`HazardSpawnRequest`)은 Effects 것이므로 18-I 가 함께 가져간다.
    ///
    /// ⚠ **이 클러스터는 P1 을 독점하지 않는다.** #4 `BossPeriodicTrigger` 가 18-J 소속으로
    /// 같은 phase 에 끼어든다 — 그래서 클러스터가 등록하지 않고 신고하고
    /// <see cref="SimPipeline"/> 이 캡처 번호로 정렬한다(18-D 가 세운 규율).
    /// </summary>
    public sealed class EnvironmentCluster
    {
        public LastRunSystem LastRun { get; }
        public HazardLifetimeSystem HazardLifetime { get; }
        public AllyBuffFieldSystem AllyBuffField { get; }
        public ZoneApplySystem ZoneApply { get; }
        public ObstacleLifetimeSystem ObstacleLifetime { get; }
        public DefenderFieldSystem DefenderField { get; }
        public PatrolFieldSystem PatrolField { get; }

        public EnvironmentCluster(SimChannels channels)
        {
            LastRun = new LastRunSystem();
            HazardLifetime = new HazardLifetimeSystem();
            AllyBuffField = new AllyBuffFieldSystem(channels.StatApply);
            ZoneApply = new ZoneApplySystem(channels.EnemyCc, channels.DotApply,
                                           channels.StatApply, channels.HazardRuntime);
            ObstacleLifetime = new ObstacleLifetimeSystem();
            DefenderField = new DefenderFieldSystem();
            PatrolField = new PatrolFieldSystem();
        }

        /// <summary>
        /// P1 여섯(#1·#2·#3·#5·#6·#7) + P3 하나(#16).
        ///
        /// **P1 안의 상대 순서가 계약이다.** 특히 #2 `HazardLifetime` 이 굽는 셀 인덱스를
        /// #5 `ZoneApply` 가 읽으므로 2 → 5 순서가 뒤집히면 존이 **한 틱 낡은 인덱스**를 본다.
        /// #6 이 #5 뒤인 것도 그대로 옮겼다 — 장애물 집합의 소비자는 이동(#17)이라 P1 안에서는
        /// 순서가 관측되지 않지만, 캡처를 따르는 것이 규율이다.
        ///
        /// #16 이 P3(이동 #17 **직전**)인 것은 "이번 틱에 구운 방향을 이번 틱에 쓴다" 는 계약이다.
        /// </summary>
        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(1, SimPhase.FieldsAndPeriodic, nameof(LastRunSystem), LastRun.Run);
            yield return new SimStep(2, SimPhase.FieldsAndPeriodic, nameof(HazardLifetimeSystem), HazardLifetime.Run);
            yield return new SimStep(3, SimPhase.FieldsAndPeriodic, nameof(AllyBuffFieldSystem), AllyBuffField.Run);
            yield return new SimStep(5, SimPhase.FieldsAndPeriodic, nameof(ZoneApplySystem), ZoneApply.Run);
            yield return new SimStep(6, SimPhase.FieldsAndPeriodic, nameof(ObstacleLifetimeSystem), ObstacleLifetime.Run);
            yield return new SimStep(7, SimPhase.FieldsAndPeriodic, nameof(DefenderFieldSystem), DefenderField.Run);
            yield return new SimStep(16, SimPhase.PreCombat, nameof(PatrolFieldSystem), PatrolField.Run);
        }
    }
}
