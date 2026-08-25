using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Skills
{
    // 드레인 지점의 이름. 그물이 「어느 seam 이 살아 있나」를 물을 수 있어야
    // 라우팅이 한 곳에서만 끊긴 상태를 잡는다.
    public enum SkillSeam : byte
    {
        Periodic = 0,   // 주기·배치 — BossPeriodicTriggerSystem 뒤
        Attack = 1,     // 공격 해결 — AttackSystem 뒤
        Threshold = 2,  // 체력 경계 — HealthThresholdSystem 뒤
        Count = 3,
    }

    // skill-layer-foundation unit 4 — 드레인 지점 셋. **로직은 base 에만 있다.**
    // 이 파일의 세 타입은 어트리뷰트와 격자 파라미터뿐이다.
    //
    // 각 파생이 「그 감지자 뒤 + 그 하류 앞」 구간에 정확히 꽂힌다. 이 순서가 곧 계약이라
    // 어트리뷰트를 옮기면 arm 이 1프레임 밀리고 자장가·도발·오라·blink 가 달라진다.
    //
    // ⚠ 이 순서를 바꾸면 `battle-sim-extraction` 의 order-capture 를 다시 떠야 한다 —
    // arm 실행 위치를 옮기는 것은 **생산자 위치를 옮기는 것**과 등가다.

    // 격자 파라미터를 FlowField 에서 읽는 공용 부분. 셋이 같은 값을 쓴다.
    public abstract partial class SkillDispatchSeamBase : SkillDispatchSystemBase
    {
        // ⚠ **OnUpdate 당 1회만 읽는다**(리뷰 L6). 셋이 각자 `Grid()` 를 부르면
        // 같은 싱글턴을 세 번 조회하고, 세 값이 **서로 다른 프레임의 격자**일 여지도
        // 생긴다(사이에 맵이 갈리면). 한 번 읽어 셋이 나눠 쓴다.
        private FlowFieldSingleton _grid;
        private bool _gridRead;

        protected override float TileSize => Grid().tileSize;
        protected override int2 GridSize => Grid().gridSize;
        protected override float3 Origin => Grid().origin;

        protected override void OnUpdate()
        {
            _gridRead = false;   // 프레임 경계 — 다음 접근이 새로 읽는다
            base.OnUpdate();
        }

        private FlowFieldSingleton Grid()
        {
            if (!_gridRead)
            {
                _grid = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var ff) ? ff : default;
                _gridRead = true;
            }
            return _grid;
        }
    }

    // ① 주기 감지 뒤 — 채찍질·자장가·가호·발사 명세.
    //    하류: ProjectileEmitter(패턴 같은 프레임) · ModifierApply · AggroState.
    // ⚠ **발사 명세는 같은 프레임에 나가야 한다.** `ProjectileEmitterSystem` 이
    // 스테이징된 `EmitterInstance` 를 보기 전에 seam 이 돌아야 한다 — 순서를 안 박으면
    // 정렬이 빌드마다 달라져 1프레임 지연이 오락가락한다(은퇴한 arm 이 같은 계약을
    // 갖고 있었다). 세 seam 전부에 거는 이유: 어느 트리거가 패턴을 쏘든 같아야 한다.
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Combat.BossPeriodicTriggerSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Effects.ModifierApplySystem))]
    [UpdateBefore(typeof(Wassup.Battle.Effects.AggroStateSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Combat.Projectile.Emission.ProjectileEmitterSystem))]
    public partial class SkillDispatchPeriodicSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Periodic;
    }

    // ② 공격 해결 뒤 — AttackN 계열.
    //    하류: DamageApplication(피해 정산) · ProjectileEmitter(발사).
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Combat.AttackSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Units.DamageApplicationSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Combat.Projectile.Emission.ProjectileEmitterSystem))]
    public partial class SkillDispatchAttackSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Attack;
    }

    // ③ 체력 경계 뒤 — 궁극기·도약·경계 자폭.
    //    하류: UltimateLeapSystem(카운트다운) · BlinkApplySystem(텔레포트).
    //    ⚠ HealthThreshold 는 UnitLifecycle(#44)보다 **뒤**(#45)다 — 그 순서 덕에
    //    이번 프레임 사망자가 이미 파괴돼 후보 풀을 오염시키지 않는다. 그 관계도 여기서
    //    같이 박제된다(unit 0 미결 2 종결 기록).
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Combat.HealthThresholdSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Combat.UltimateLeapSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Combat.Projectile.Emission.ProjectileEmitterSystem))]
    public partial class SkillDispatchThresholdSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Threshold;
    }
}
