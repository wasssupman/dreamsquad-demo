using Unity.Mathematics;

namespace Wassup.Skills
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
        // Burst 코드가 managed 레지스트리를 안 읽고도 라우팅할 수 있다. 0 = legacy arm.
        int SkillId { get; }

        void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx);
    }

    // 대상 축. 액티브가 요구하는 두 가지 때문에 이 모양이다:
    //   · 시전자가 판 위에 없다 → `CasterRef.Unit` 이 무효일 수 있다
    //   · Portal 은 **타일 2개**를 받는다(입구/출구). 「입구==출구 거절」은 arm 이 아니라
    //     **창구 규칙**이라 여기가 아니라 디스패처/검증층이 본다.
    public readonly struct SkillTarget
    {
        public readonly SkillEntityId Unit;   // 무효 = 유닛 대상이 아니다
        public readonly int2 CellA;
        public readonly int2 CellB;
        public readonly bool HasCellB;
        // skill-layer-migration unit 3a — **겨눈 방향**. 대상과 같은 축이라 여기 산다.
        //
        // ⚠ **발사 시점의 값이고 재계산하면 안 된다.** 유도탄이 다른 데 맞아도 밀리는
        // 방향은 «쏜 방향» 이고(계약 6), 드레인 시점엔 둘 다 이미 움직였다.
        // 0 = 방향 없음(공격자와 대상이 같은 칸) — 그 판정은 concrete 가 한다.
        public readonly float2 DirectionXZ;

        public SkillTarget(SkillEntityId unit, int2 cellA, int2 cellB, bool hasCellB,
                           float2 directionXZ = default)
        {
            Unit = unit; CellA = cellA; CellB = cellB; HasCellB = hasCellB;
            DirectionXZ = directionXZ;
        }

        public static SkillTarget OfUnit(SkillEntityId unit, int2 cell)
            => new SkillTarget(unit, cell, default, false);
        public static SkillTarget OfUnit(SkillEntityId unit, int2 cell, float2 directionXZ)
            => new SkillTarget(unit, cell, default, false, directionXZ);
        public static SkillTarget OfCell(int2 cell)
            => new SkillTarget(SkillEntityId.None, cell, default, false);
        public static SkillTarget OfCellPair(int2 a, int2 b)
            => new SkillTarget(SkillEntityId.None, a, b, true);
        public static readonly SkillTarget None
            = new SkillTarget(SkillEntityId.None, default, default, false);

        public bool HasUnit => Unit.IsValid;
    }
}
