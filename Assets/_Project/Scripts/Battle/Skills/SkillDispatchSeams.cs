using Unity.Entities;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Skills
{
    // 드레인 지점의 이름. 그물이 「어느 seam 이 살아 있나」를 물을 수 있어야
    // 라우팅이 한 곳에서만 끊긴 상태를 잡는다.
    public enum SkillSeam : byte
    {
        // ⚠ **0 은 「아무도 아니다」여야 한다**(unit 3e). 이 값이 이벤트 필드의 기본값이라,
        // 실제 seam 하나를 0 에 두면 생산자가 seam 을 안 채웠을 때 **조용히 그 seam 으로**
        // 흘러간다 — 이 spec 이 반복해서 당한 fail-open 모양 그대로다. 드레인이 loud 하게 버린다.
        None = 0,
        Periodic = 1,   // 주기·배치 — BossPeriodicTriggerSystem 뒤
        Attack = 2,     // 공격 해결 — AttackSystem 뒤
        Threshold = 3,  // 체력 경계 — HealthThresholdSystem 뒤
        // skill-layer-migration unit 3c — 처치(그리고 앞으로 피격 N회·실드 파열).
        // 감지가 `DamageApplicationSystem` **안**에서 나므로 공격 seam(그 앞)이 못 받는다.
        Death = 4,
        // skill-layer-migration unit 3d″ — **내가 죽었을 때**(작별 선물).
        // 위 `Death` 와 다르다: 저건 「내가 죽였다」, 이건 「내가 죽는다」다.
        // 감지가 `UnitLifecycleSystem`(파괴 지점) 안이라 그 앞의 seam 은 못 받는다.
        Lifecycle = 5,
        // skill-layer-migration unit 4a — **부착되는 순간**. 위 다섯과 성격이 다르다:
        // 저 다섯은 시뮬이 프레임 안에서 감지하는 사건이고, 이건 **사용자 입력이 부르는
        // 브리지 호출**이다. 그래서 프레임을 기다리지 않고 브리지가 그 자리에서 돌린다.
        Immediate = 6,
        // skill-layer-migration unit 5a — **캐스트 성사.** 캐스터는 `attackRange` 0 이라
        // RESOLVE 에 못 가고, 캐스트가 곧 그 host 의 공격 사건이다. 그 사건을
        // `AttackSystem` 이 **같은 프레임**에 소비하는 것이 계약이라 여기가 따로 난다.
        Cast = 7,
        Count = 8,
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

    // ④ 피해 정산 뒤 — `OnKill` · `OnDamagedN` · `OnShieldBreak`.
    //
    // ⚠ 이 주석은 한때 「`OnDamagedN`·`OnShieldBreak` 는 아직 `skillId` 를 보지도
    // 않는다」고 적혀 있었다(3d‴/3e 전). 지금은 셋 다 라우팅한다 — 그리고 unit 8 에서
    // **적에게도 열렸다.** 옛 문면을 남기면 「배선이 없다」로 읽혀 다음 사람이
    // 없는 구멍을 메우려 든다.
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

    // ⑤ 파괴 뒤 — **자기 죽음**(작별 선물).
    //
    // ⚠ **왜 ④ 로 못 받나.** ④ 는 「내가 죽였다」를 `DamageApplicationSystem` 안에서
    // 잡는데, 자기 죽음의 정본 감지 지점은 `UnitLifecycleSystem` 이다. 거기가
    // **모든** 사망 경로(피해·치명 타이머·순찰 수명)가 합류하는 유일한 지점이라,
    // ④ 로 앞당기면 피해로 죽은 경우만 작별 선물이 나오고 나머지는 조용히 빠진다.
    //
    // ⚠ **드레인 시점엔 시전자가 이미 없다.** `UnitLifecycleSystem` 은 자기 ECB 를
    // 자기 OnUpdate 끝에서 재생하므로, 이 seam 이 도는 순간 그 엔티티는 파괴돼 있다.
    // 그래서 이 seam 을 타는 스킬은 **값만으로 완결돼야 한다** — 자리·피해·반경·층을
    // 감지자가 실어 보내고, concrete 는 시전자를 다시 묻지 않는다.
    // (그게 `SkillFiredEvent` 값 스냅샷 계약이 존재하는 이유다.)
    // ⚠ 아래 순서 제약은 이제 **지연**만 정한다(unit 3e). 예전엔 소유까지 정했다 —
    // 큐가 공유고 각 seam 이 「자기 순서에 있는 것 전부」를 가져가서, 경계 시스템(#45)이
    // 파괴(#44)보다 뒤인 탓에 경계 seam 이 자기 죽음 이벤트를 집어갔다. 지금은 이벤트가
    // 자기 seam 을 말하므로 소유는 안전하고, 이 제약은 「같은 프레임에 터진다」를 지킨다.
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Units.UnitLifecycleSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Combat.HealthThresholdSystem))]
    public partial class SkillDispatchLifecycleSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Lifecycle;

        // ⚠ 여기서만 false — 이 seam 의 전제가 「시전자가 이미 없다」이기 때문이다.
        // 기본 가드를 그대로 두면 작별 선물이 **매번** 드레인에서 버려진다(실제로 그랬다).
        protected override bool RequiresLiveCaster => false;
    }

    // ⑥ 부착 즉시 — **브리지가 그 자리에서 돌린다.**
    //
    // ⚠ **왜 다섯으로 안 되나.** 위 다섯은 전부 시뮬 프레임 안의 사건이라 「다음 틱에
    // 드레인」이 성립한다. 부착은 아니다 — `ApplyDreamcatcherCardToUnit` 이 **동기 트랜잭션**
    // 이라, preflight 로 가부를 정하고 쓰기를 하고 핸들(또는 −1, 무차감 거절)을 **그 호출에서**
    // 돌려준다. 큐에 넣고 프레임을 기다리면 그 결정 뒤에 쓰기가 도착한다.
    //
    // 그래서 이 seam 은 **자기 순서를 갖지 않는다.** 그룹에 있는 것은 안전망일 뿐이고
    // (브리지 밖에서 누가 이 seam 으로 넣었을 때), 실제 실행은 브리지가
    // `Update()` 를 직접 불러 자기 콜스택 안에서 끝낸다.
    //
    // ⚠ 이 seam 을 시뮬 사건에 쓰지 말 것. 프레임 중간에 임의로 도는 드레인이라
    // 시스템 간 순서 계약(emitter 같은 프레임 · 파괴 전 등)을 하나도 보장하지 않는다.
    [UpdateInGroup(typeof(BattleSimGroup))]
    public partial class SkillDispatchImmediateSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Immediate;
    }

    // ⑦ 해저드 캐스트 뒤 — **같은 프레임 소비가 계약이다.**
    //
    // ⚠ **주기 seam 으로 못 받는다.** `BossPeriodicTriggerSystem` 은 `AttackSystem` 과
    // 순서 계약이 없어서, 거기로 옮기면 캐스트 사건이 정렬기 tie-break 에 따라 한 프레임
    // 밀린다 — `HazardCastSystem` 이 `[UpdateBefore(AttackSystem)]` 을 명시한 이유가
    // 정확히 그것이었다(attack-decoupling unit 4).
    //
    // 주기 seam 에 그 제약을 **거는 것도 안 된다.** 그러면 모든 주기 스킬(오라·자장가·
    // 궤도 화염구)의 순서가 같이 움직이고, emitter 를 뒤로 미는 전이 간선이 생긴다
    // (ECS 리뷰 H-1 이 같은 이유로 경계 seam 의 emitter 제약을 뺐다).
    // seam 하나가 그 전부보다 싸다.
    [UpdateInGroup(typeof(BattleSimGroup))]
    [UpdateAfter(typeof(Wassup.Battle.Effects.HazardCastSystem))]
    [UpdateBefore(typeof(Wassup.Battle.Combat.AttackSystem))]
    public partial class SkillDispatchCastSystem : SkillDispatchSeamBase
    {
        protected override SkillSeam Seam => SkillSeam.Cast;
    }
}
