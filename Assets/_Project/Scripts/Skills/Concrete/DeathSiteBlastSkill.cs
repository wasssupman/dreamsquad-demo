namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 3d — **죽은 자리**에서 터진다(시체폭발).
    //
    // ⚠ **자기 자리 폭발과 자리가 다르다.** `SelfAreaBlastSkill` 은 시전자 발밑에서
    // 터지는데 이쪽은 **피해자가 쓰러진 곳**이다. 같은 「광역 폭발」이라도 그 한 축이
    // 게임에서 완전히 다른 그림이라(내가 맞은 자리 ↔ 내가 죽인 자리) 별도 concrete 다.
    //
    // ⚠ **그 자리는 지금 아니면 알 수 없다.** 드레인 시점엔 피해자가 파괴돼 있어
    // 재질의가 불가능하고, 그래서 감지자가 발화 시점 좌표를 실어 보낸다.
    //
    // ⚠ **owner 는 시전자다.** 폭발 킬이 죽인 쪽에 귀속돼야 연쇄(OnKill)가 이어진다 —
    // 여기서 owner 를 비우면 시체폭발 연쇄가 조용히 한 단계에서 멈춘다.
    //
    // ⚠ **호출처가 둘이고 자리의 주인이 다르다**(unit 3d″). `OnKill` 은 「내가 죽인 자리」,
    // `OnDeath`(작별 선물)는 「내가 죽은 자리」다. 이 스킬은 그 차이를 모른다 — 실려 온
    // 자리에서 터질 뿐이고, **누구의 자리인가는 감지자가 정한다.** 그래서 concrete 를
    // 나누지 않았다(같은 규칙, 다른 입력).
    // 부수 효과로 작별 선물은 owner 가 비게 된다 — 죽은 시전자는 드레인 때 이미 없어
    // `CasterRef.Player` 로 접히고, 그것이 레거시(`owner = Entity.Null`)와 같은 값이다.
    public sealed class DeathSiteBlastSkill : ISkill
    {
        public const int Id = 20;
        public int SkillId => Id;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            var a = new AreaDamageParams(p);
            if (a.Damage <= 0f) return;

            ctx.Emit(new SimIntent
            {
                Kind = SimIntentKind.SpawnProjectile,
                Source = caster.Unit,          // owner — 킬 귀속
                Target = SkillEntityId.None,   // 대상이 아니라 **자리**를 때린다
                Position = p.EventPosition,    // 죽은 자리(발화 시점 스냅샷)
                Amount = a.Damage,
                TileRange = a.Radius,
                DataIndex = a.VfxDataIndex,
                // 예고 시간. 0 이면 즉발이고, 퇴근 운석만 이 값을 저작한다(계약 8).
                // ⚠ **이것 때문에 concrete 를 가르지 않는다** — 즉발과 예고는 규칙이 아니라
                // 값의 차이다. 자리의 주인(죽인 자리 ↔ 죽은 자리 ↔ 비워진 칸)도 마찬가지로
                // 감지자가 정한다. 이 스킬이 아는 것은 「실려 온 자리에서 터진다」 하나다.
                Duration = p.Duration,
                // ⚠ **배율은 감지자가 정한다.** 죽음 계열 레거시는 1 로 하드코딩했고(저작
                // 필드를 안 읽었다) 퇴근 운석은 저작을 읽는다. 그래서 여기서 고정하지 않고
                // 실려 온 값을 그대로 쓴다 — 죽음 감지자 둘이 0(=어댑터가 1)을 실어 보내
                // 무회귀를 지킨다. 저작 배율을 죽음 쪽에도 열려면 그쪽 감지자 한 줄이다.
                VisualScale = a.VisualScale,
                // ⚠ **레거시는 층을 안 실었다**(= 무제한). 여기서 킬러의 공격 층을 실으면
                // 지상 전용 킬러의 시체폭발이 비행 적을 더는 못 때린다 — 그건 사양 변경이다.
                // 형제(잿불)는 반대로 층을 **실어야** 했다(그쪽 레거시가 그랬다).
                // 무회귀 쪽을 택한다: 층 게이트가 필요하면 별도 결정으로 연다.
                TargetTraversalLayers = 0,
            });
        }
    }
}
