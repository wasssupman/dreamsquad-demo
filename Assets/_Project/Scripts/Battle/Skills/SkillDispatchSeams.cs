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
        // skill-layer-migration unit 3c — 처치(그리고 앞으로 피격 N회·실드 파열).
        // 감지가 `DamageApplicationSystem` **안**에서 나므로 공격 seam(그 앞)이 못 받는다.
        Death = 3,
        Count = 4,
    }

    // skill-layer-foundation unit 4 — 드레인 지점. **로직은 base 에만 있다.**
    // 이 파일의 타입들은 어트리뷰트와 격자 파라미터뿐이다.
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
    // 갖고 있었다).
    //
    // ⚠ **경계 seam 에는 걸지 않는다**(ECS 리뷰 H-1). 처음엔 셋 다 걸었는데, 그건
    // 「어느 트리거가 쏘든 같아야 한다」는 대칭 논리였고 **비용을 안 본 것**이었다:
    // 경계 seam 은 `HealthThresholdSystem` 뒤이고 그건 `DamageApplicationSystem` 뒤라,
    // 거기에 이 제약을 걸면 `DamageApplication → HealthThreshold → seam → emitter`
    // 라는 **전이 간선이 새로 생겨 emitter 를 프레임 뒤쪽으로 민다.** 원래 emitter 는
    // 그 둘과 상대 순서가 자유였다(관측값은 HealthThreshold 앞).
    //
    // 밀리면 `UnitLifecycleSystem`(사망 엔티티 파괴, 역시 `DamageApplication` 뒤)과
    // 가까워지고, emitter 가 그 뒤로 가면 그 프레임에 죽은 host 의 잔여 버스트가
    // **엔티티째 사라진다**(`WithNone<DeadTag>` 이 막는 게 아니라 대상이 없어진다).
    // 그 상대 순서는 어디에도 선언돼 있지 않아서, 이 제약이 tie-break 를 흔든다.
    //
    // 그리고 이 제약은 **오늘 쓰이지도 않는다** — 임계 트리거로 발사 명세를 저작한
    // 것이 0건이다. 임계 × 패턴 저작이 처음 생기는 unit 이 이 줄을 도로 넣되,
    // 그때는 emitter 의 새 자리를 **emitter 자신에게** 선언해 리뷰 대상으로 만들고
    // (`[UpdateAfter(HealthThresholdSystem)]` + `[UpdateBefore(UnitLifecycleSystem)]`)
    // order-capture 를 다시 떠야 한다.
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
    // ⚠ emitter 제약 **없음** — 위 H-1 참조. 임계 × 발사 명세 저작이 생기는 날 넣는다.
    public partial class SkillDispatchThresholdSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Threshold;
    }

    // ④ 피해 정산 뒤 — **지금은 `OnKill` 만**(ECS 리뷰 M-6).
    //
    // ⚠ 이름이 「죽음 계열」이지만 `OnDamagedN`·`OnShieldBreak` 블록은 아직 `skillId` 를
    // **보지도 않는다** — 그 감지자들은 라우팅 분기가 없어 전부 legacy arm 으로 간다.
    // 이중 발화도 조용한 죽음도 없지만(그 payload 들이 카드 화이트리스트 밖이다),
    // **주석이 없는 배선을 있다고 말하지 않게** 여기 적어 둔다. 3d‴/3e 가 채운다.
    //
    // ⚠ **emitter 제약이 없다**(주기 seam 에는 있다 — 위 H-1 노트). 오늘 `OnKill` 로
    // 발사 명세를 저작한 것이 0건이라 무해하지만, 그 저작이 처음 생기는 unit 은
    // (a) 여태 조용한 no-op 이던 조합이 **실제로 발사되고** (b) emitter 와의 상대 순서가
    // 안 박혀 **0/1 프레임 지연이 빌드마다 갈린다**는 것을 함께 처리해야 한다.
    //
    // ⚠ **네 번째다.** 토대 unit 0 이 「3 seam」이라 적은 것은 그때 조사한 payload 들의
    // 감지 지점이 셋이었다는 뜻이고, 카드가 죽음 계열을 들고 오면서 네 번째 감지 지점이
    // 생겼다. 감지자가 다른 프레임 창을 가지면 seam 도 따라 는다 — 「3」이 상한이 아니다.
    //
    // ⚠ **`AttackN` seam(#35)이 이걸 못 받는다.** 그쪽은 `DamageApplicationSystem` **앞**
    // 이고 죽음 사건은 그 **안**에서 난다 — 같은 큐를 쓰더라도 한 프레임 밀린다.
    //
    // ⚠ **`UnitLifecycleSystem` 앞이어야 한다.** 그게 사망 엔티티를 파괴하므로, 뒤로 가면
    // 죽은 대상을 참조하는 효과가 대상째 사라진다.
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Units.DamageApplicationSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Units.UnitLifecycleSystem))]
    public partial class SkillDispatchDeathSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Death;
    }
}
