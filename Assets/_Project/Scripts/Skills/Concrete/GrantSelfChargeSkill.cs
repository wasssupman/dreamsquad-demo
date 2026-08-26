namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3d‴ — **다음 공격을 예약한다**(가시 갑옷).
    //
    // N회 맞으면 다음 한 방이 두 번 나간다. 이 스킬이 하는 일은 «충전 1발을 나에게
    // 얹는다»뿐이고, **그 충전을 어떻게 쓰는지는 공격 해결이 소유한다** — 그쪽이
    // 「두 번 쏜다」를 알고 여기는 모른다. 그래서 이름에 DoubleFire 가 없다.
    //
    // ⚠ **자기 자신이 대상이다.** 조준도 후보 선별도 없어서 이 concrete 는 판단이 없는데,
    // 그래도 존재하는 이유는 「N회 피격」이라는 사건과 「충전을 얹는다」는 결과 사이를
    // arm 이 아니라 어휘가 잇게 하기 위해서다(그게 이 레이어의 전부다).
    public sealed class GrantSelfChargeSkill : ISkill
    {
        public const int Id = 23;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            // 저작이 0 이거나 음수면 안 얹는다 — 「얹은 척」하는 조용한 no-op 을 만들지 않는다.
            // 레거시는 항상 1발이었고(v1), 저작 필드가 없어 여기서도 1이 기본이다.
            int charges = p.Magnitude > 0f ? (int)p.Magnitude : 1;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.GrantCharge,
                Source = caster.Unit,
                Target = caster.Unit,   // 나에게
                Amount = charges,
            });
        }
    }
}
