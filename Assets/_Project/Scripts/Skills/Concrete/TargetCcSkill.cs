using Unity.Mathematics;

namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3a — 이번 공격의 대상 하나에게 CC 를 건다(서리화살·돌풍).
    //
    // ⚠ **대상은 이 스킬이 고르지 않는다.** 「누구를 때리나」는 이미 공격이 정했고
    // (최근접 → 힐러 재랭킹 → priority → 적 락 → 어그로 → frontmost → 지속 락 →
    //  커밋 유지 → facing 9단계), 그 결과가 발화 시점 값으로 실려 온다.
    // 여기서 다시 고르면 **같은 규칙을 두 벌 갖게 되고 둘이 갈린다.**
    //
    // ⚠ **밀쳐냄은 방향이 있어야 성립한다.** 공격자와 대상이 같은 칸이면 방향이 0 이라
    // 「어디로도 안 밀리는 밀쳐냄」이 되는데, 그건 CC 슬롯만 잡아먹고 아무 일도 안 한다.
    // 그 경우엔 아예 안 건다(레거시와 같은 판정).
    public sealed class TargetCcSkill : ISkill
    {
        public const int Id = 16;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new TargetCcParams(p);
            if (!target.Unit.IsValid) return;
            if (a.Duration <= 0f && a.Cc != SkillCcKind.Impulse) return;

            var intent = new SimIntent
            {
                Kind = SimIntentKind.ApplyCc,
                Target = target.Unit,
                Source = caster.Unit,
                Selector = (int)a.Cc,
                Duration = a.Duration,
            };

            if (a.Cc == SkillCcKind.Impulse)
            {
                // 방향은 **발사 시점의 것**이다 — 유도탄이 다른 데 맞아도 밀리는 방향은
                // 쏜 방향이다(계약 6). 그래서 재계산하지 않고 실려 온 값을 쓴다.
                if (math.lengthsq(target.DirectionXZ) <= 1e-6f) return;
                intent.DirectionXZ = target.DirectionXZ;
                intent.Amount = a.Magnitude;   // 넉백 속도
            }

            ctx.Emit(intent);
        }
    }
}
