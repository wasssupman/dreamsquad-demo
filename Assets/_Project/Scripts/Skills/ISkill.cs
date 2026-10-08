using Unity.Mathematics;

namespace Somnia.Battle.Skills
{
    // skill-layer-foundation unit 3 — 스킬 하나.
    //
    // **`Execute` 를 호출하는 주체가 곧 이 스킬의 소유자다.** concrete 는 진영도
    // host 종류도 갖지 않는다 — 보스가 쓰던 스킬을 잡몹이 부르면 코드 0줄로 동작한다.
    // 그게 이 인터페이스의 존재 이유 전부다.
    //
    // 계약:
    //   · **무상태**(계약 5). 필드를 갖지 않는다. 진행형 상태(도약 비행·수면 완주)는
    //     컴포넌트+시스템 소유이고 스킬은 **개시와 수치**까지다.
    //   · **ECS 를 모른다**(계약 1). 이 어셈블리가 Entities 를 참조하지 않아
    //     컴파일러가 강제한다.
    //   · **상태를 바꾸지 않는다**(계약 3). `ctx.Emit` 으로 의도를 방출한다.
    public interface ISkill
    {
        // 레지스트리 키. 슬롯에 **unmanaged 로 베이크**되는 값이라(계약 12) 감지측
        // Burst 코드가 managed 레지스트리를 안 읽고도 라우팅할 수 있다. 0 = 스킬 아님.
        int SkillId { get; }

        void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx);
    }

    // unified-effect-layer unit 2 — **원점 두 값**(README 계약 1). 드레인이 한 번 채우고 concrete 는 **이것만** 읽는다.
    //
    // 예전엔 concrete 가 원점을 셋으로 제각각 읽었다 — `ctx.Position(caster)`(14) · `target.CellA`(4) ·
    // `p.EventPosition`(3). 산출은 이미 한 곳(감지자 + 드레인)이었는데 **읽기**가 흩어져 있어서, 같은 효과가
    // 출처에 따라 다른 자리를 원점으로 삼을 수 있었다(H1). 이 struct 는 새 산출기가 아니라 그 읽기를 모은 것이다.
    //
    //   ① 발사 자리(`LaunchSite ↔ LaunchBody`) = **발동 주체**(사건 주체). 판 위에 있으면 드레인 시점의 자리,
    //      없으면(자기 죽음 · 퇴근) 발화 시점 스냅샷. 비수·부메랑은 여기서 날고, 브레스는 여기서 편다.
    //   ② 효과 좌표(`EffectSite ↔ EffectBody`) = 사건이 실은 자리(맞은 적 · 죽은 자리 · 비워진 칸) 또는 지정 칸의
    //      중심. 운석은 여기에 떨어진다. 사건이 자리를 안 실었으면 ①의 스냅샷이다(옛 `TargetPosition == 0` 폴백).
    //
    // ⚠ **자리↔몸은 짝으로 다닌다**(제약 13). 원점 항은 «효과의 형» 이 정한다(계약 2):
    //   몸에서 나오는 것 = 그 자리 **주인**의 몸 — 자폭이면 ①의 몸, 시체 폭발이면 ②의 몸(죽은 «적» — 시전자인 킬러가
    //   아니다). 자리에 떨어지는 것 = **`EffectBody == 0`**(칸 반폭). 형을 고르는 것은 concrete 이고, 몸의 값은
    //   감지자가 스냅샷한 것이다 — `CasterRef.BodyRadius` 로 ②의 몸을 대신하지 않는다.
    public readonly struct SkillOrigin
    {
        public readonly float3 LaunchSite;
        public readonly float LaunchBody;
        public readonly float3 EffectSite;
        // **0 = 그 자리는 «칸» 이다**(자리에 떨어지는 것 — 퇴근 운석 · 지정 칸).
        public readonly float EffectBody;
        // ②가 **칸으로 지정됐을 때**(액티브 커맨드) 그 칸. 장판·포탈은 칸을 싣는다 — `EffectSite` 에서 되짚지
        // 않는다(격자 밖 칸은 좌표→칸 변환이 경계로 접혀 다른 칸이 된다). 칸 지정이 아닌 사건에서는 기본값이다.
        public readonly int2 EffectCell;

        public SkillOrigin(float3 launchSite, float launchBody, float3 effectSite, float effectBody, int2 effectCell)
        {
            LaunchSite = launchSite; LaunchBody = launchBody;
            EffectSite = effectSite; EffectBody = effectBody;
            EffectCell = effectCell;
        }

        // 사건이 자리를 안 실었다 — ① = ② = 주인(자기 사건). 드레인의 「`HasSite` 없으면 주인 자리」와 같은 모양.
        public static SkillOrigin OfSubject(float3 site, float body)
            => new SkillOrigin(site, body, site, body, default);

        // 칸 지정(액티브) — 판 위의 발동 주체가 없다(①은 비고 몸도 없다) · ②는 그 칸의 중심 · 자리형.
        public static SkillOrigin AtCell(int2 cell, float3 cellCenter)
            => new SkillOrigin(default, 0f, cellCenter, 0f, cell);
    }

    // 대상 축. 액티브가 요구하는 두 가지 때문에 이 모양이다:
    //   · 시전자가 판 위에 없다 → `CasterRef.Unit` 이 무효일 수 있다
    //   · Portal 은 **타일 2개**를 받는다(입구/출구). 「입구==출구 거절」은 arm 이 아니라
    //     **창구 규칙**이라 여기가 아니라 디스패처/검증층이 본다.
    //
    // unified-effect-layer unit 2 — 원점은 `Origin` 하나로 온다. 옛 `CellA`(지정 칸 = 입구)는 `Origin.EffectCell` 이
    // 됐다 — 칸 조준도 원점 읽기의 하나라서다. `CellB`(출구)는 원점이 아니라 두 번째 조준이라 여기 남는다.
    public readonly struct SkillTarget
    {
        public readonly SkillEntityId Unit;   // 무효 = 유닛 대상이 아니다
        public readonly SkillOrigin Origin;
        public readonly int2 CellB;
        public readonly bool HasCellB;
        // skill-layer-migration unit 3a — **겨눈 방향**. 대상과 같은 축이라 여기 산다.
        //
        // ⚠ **발사 시점의 값이고 재계산하면 안 된다.** 유도탄이 다른 데 맞아도 밀리는
        // 방향은 «쏜 방향» 이고(계약 6), 드레인 시점엔 둘 다 이미 움직였다.
        // 0 = 방향 없음(공격자와 대상이 같은 칸) — 그 판정은 concrete 가 한다.
        public readonly float2 DirectionXZ;

        public SkillTarget(SkillEntityId unit, in SkillOrigin origin, int2 cellB, bool hasCellB,
                           float2 directionXZ = default)
        {
            Unit = unit; Origin = origin; CellB = cellB; HasCellB = hasCellB;
            DirectionXZ = directionXZ;
        }

        public static SkillTarget OfUnit(SkillEntityId unit, in SkillOrigin origin, float2 directionXZ = default)
            => new SkillTarget(unit, in origin, default, false, directionXZ);
        // 대상 엔티티 없이 원점만 — 자기 사건 · 자리 사건 · 칸 지정.
        public static SkillTarget At(in SkillOrigin origin)
            => new SkillTarget(SkillEntityId.None, in origin, default, false);
        public static readonly SkillTarget None
            = new SkillTarget(SkillEntityId.None, default, default, false);

        public bool HasUnit => Unit.IsValid;
    }
}
