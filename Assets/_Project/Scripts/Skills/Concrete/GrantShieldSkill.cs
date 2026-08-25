namespace Wassup.Skills.Concrete
{
    // skill-layer-migration unit 0 — 악몽의 가호. host 와 **같은 진영**에 실드를 나눠준다.
    //
    // ⚠ **host 자신은 제외가 계약이다.** 실드 병합 키가 source 라서, 두 능력이 같은
    // host 에서 나와 자기 자신에게 겹치면 한 슬롯을 공유한다 — 이쪽이 매 주기 그 잔량을
    // max 로 재충전해 「경계에 생기는 벽」이 「상시 실드」로 붕괴한다.
    // 자기 실드는 경계 arm 의 꿈의 장막이 소유하고, bake 가 그 조합을 가른다.
    //
    // ⚠ **만충이면 건너뛴다.** 같은 출처의 잔량이 이미 저작량 이상이면 병합이 max 로
    // no-op 이라 헛 VFX 만 남는다(가디언 선례).
    public sealed class GrantShieldSkill : ISkill
    {
        public const int Id = 3;
        public int SkillId => Id;

        private const int MaxTargets = 64;

        public void Execute(CasterRef caster, in SkillTarget target, in SkillParams p, ISkillContext ctx)
        {
            float amount = p.Magnitude;
            int radius = p.TileRange;
            if (amount <= 0f) return;

            // ⚠ **`tileRange` 가 두 능력을 가른다**(bake 가 조합을 거절한다):
            //   · 0  = 꿈의 장막 — 경계마다 **자기에게**. host 제외 계약의 예외이고,
            //          그래서 위 「병합 키 붕괴」 경고가 여기엔 해당되지 않는다
            //          (능력이 하나뿐이면 슬롯을 공유할 상대가 없다).
            //   · >0 = 악몽의 가호 — 반경 내 **같은 편**, host 제외.
            // 한 concrete 가 둘을 다 맡는 이유: 같은 payload 이고 bake 가 이미 갈랐다.
            // 여기서 self 를 안 받으면 경계 arm 이 이전될 때 실드가 조용히 사라진다.
            if (radius <= 0)
            {
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.GrantShield,
                    Target = caster.Unit,
                    Source = caster.Unit,   // 같은 출처 = max 갱신(누적 아님)
                    Amount = amount,
                });
                return;
            }

            var hostPos = ctx.Position(caster.Unit);
            var buf = new SkillEntityId[MaxTargets];
            int n = ctx.Allies(caster, hostPos, radius,
                CandidateFilter.ExcludeSelf | CandidateFilter.ExcludeDead,
                RangeMetric.Chebyshev, buf);

            for (int i = 0; i < n; i++)
            {
                var id = buf[i];
                if (!ctx.Has(id, UnitPredicate.HasShieldBuffer)) continue;
                if (ctx.ShieldValueFrom(id, caster.Unit) >= amount) continue;   // 만충 — 헛 VFX 금지

                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.GrantShield,
                    Target = id,
                    Source = caster.Unit,   // 같은 출처 = max 갱신 → 깎인 만큼만 다시 찬다
                    Amount = amount,
                });

                // ⚠ **대상 위치에 대상 수만큼** 쏜다 — 가디언이 그렇게 한다.
                // host 에서 한 번만 쏘면 "보스가 반짝하고 호위 실드는 소리 없이 생긴다"가
                // 되어, 같은 채널을 재사용한 이유(「같은 사건은 같은 그림」)가 깨진다.
                ctx.Emit(new SimIntent
                {
                    Kind = SimIntentKind.PlayVisual,
                    Selector = (int)SkillVisualKind.ShieldGranted,
                    Target = id,
                    Position = ctx.Position(id),
                    DataIndex = 0,   // 이 채널은 dataIndex 를 안 읽는다 — 저작 없이 고정 연출
                });
            }
        }
    }
}
